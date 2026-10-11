import { Injectable, inject, signal } from '@angular/core';
import { AuthService } from './auth.service';
import { getKestrelConfig } from './config';
import {
  ChatMessage,
  ChatRequest,
  Citation,
  ToolEvent,
} from './models';

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly apiBaseUrl = getKestrelConfig().apiBaseUrl;
  private readonly auth = inject(AuthService);

  readonly messages = signal<ChatMessage[]>([]);
  readonly answerText = signal('');
  readonly citations = signal<Citation[]>([]);
  readonly toolEvents = signal<ToolEvent[]>([]);
  readonly safetyNotice = signal<string | null>(null);
  readonly streaming = signal(false);
  readonly error = signal<string | null>(null);
  readonly activeModel = signal<string>('local');
  readonly conversationId = signal<string | null>(null);
  readonly lastMessageId = signal<string | null>(null);

  private abortController: AbortController | null = null;

  async loadModelInfo(): Promise<void> {
    try {
      const res = await fetch(`${this.apiBaseUrl}/api/model`, {
        headers: {
          ...this.auth.authorizationHeader(),
        },
      });
      if (res.status === 401) {
        this.auth.handleUnauthorized();
        return;
      }
      if (!res.ok) {
        return;
      }
      const info = (await res.json()) as {
        chatProfile?: string;
        chatModel?: string;
      };
      if (info.chatModel) {
        this.activeModel.set(info.chatModel);
      } else if (info.chatProfile) {
        this.activeModel.set(info.chatProfile);
      }
    } catch {
      // API may not be up yet during UI-only work.
    }
  }

  async send(request: ChatRequest): Promise<void> {
    this.abortController?.abort();
    this.abortController = new AbortController();

    const history = this.messages()
      .slice(-8)
      .map((m) => ({ role: m.role, content: m.content }));

    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: request.message,
    };
    this.messages.update((m) => [...m, userMessage]);

    this.streaming.set(true);
    this.error.set(null);
    this.answerText.set('');
    this.citations.set([]);
    this.toolEvents.set([]);
    this.safetyNotice.set(null);

    const assistantId = crypto.randomUUID();
    let conversationId =
      request.conversationId ?? this.conversationId() ?? undefined;

    try {
      const response = await fetch(`${this.apiBaseUrl}/api/chat`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'text/event-stream',
          ...this.auth.authorizationHeader(),
        },
        body: JSON.stringify({
          message: request.message,
          history,
          equipmentId: request.equipmentId,
          conversationId,
        }),
        signal: this.abortController.signal,
      });

      if (response.status === 401) {
        this.auth.handleUnauthorized();
        throw new Error('Signed out — please sign in again.');
      }

      if (!response.ok) {
        throw new Error(`Chat request failed (${response.status})`);
      }

      if (!response.body) {
        throw new Error('No response body from chat stream');
      }

      await this.consumeSseStream(response.body);

      conversationId = this.conversationId() ?? conversationId;
      const assistantMessage: ChatMessage = {
        id: this.lastMessageId() ?? assistantId,
        role: 'assistant',
        content: this.answerText(),
        citations: [...this.citations()],
        conversationId,
      };
      this.messages.update((m) => [...m, assistantMessage]);
    } catch (err: unknown) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        return;
      }
      const message =
        err instanceof Error ? err.message : 'Unknown chat error';
      this.error.set(message);
    } finally {
      this.streaming.set(false);
      this.abortController = null;
    }
  }

  cancel(): void {
    this.abortController?.abort();
  }

  private async consumeSseStream(body: ReadableStream<Uint8Array>): Promise<void> {
    const reader = body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        break;
      }
      buffer += decoder.decode(value, { stream: true });
      const blocks = buffer.split(/\r?\n\r?\n/);
      buffer = blocks.pop() ?? '';
      for (const block of blocks) {
        if (block.trim()) {
          this.handleSseBlock(block);
        }
      }
    }

    if (buffer.trim()) {
      this.handleSseBlock(buffer);
    }
  }

  private handleSseBlock(block: string): void {
    let eventName = 'message';
    const dataLines: string[] = [];

    for (const line of block.split(/\r?\n/)) {
      if (line.startsWith('event:')) {
        eventName = line.slice(6).trim();
      } else if (line.startsWith('data:')) {
        dataLines.push(line.slice(5).trim());
      }
    }

    const rawData = dataLines.join('\n');
    if (!rawData) {
      return;
    }

    let payload: Record<string, unknown> = {};
    try {
      payload = JSON.parse(rawData) as Record<string, unknown>;
    } catch {
      payload = { text: rawData };
    }

    switch (eventName) {
      case 'token': {
        const chunk =
          (payload['text'] as string | undefined) ??
          (payload['content'] as string | undefined) ??
          '';
        if (chunk) {
          this.answerText.update((t) => t + chunk);
        }
        break;
      }
      case 'citation': {
        const normalized = this.normalizeCitation(payload);
        if (normalized) {
          this.citations.update((list) => {
            const next = [...list];
            const existing = next.findIndex((c) => c.index === normalized.index);
            if (existing >= 0) {
              next[existing] = { ...next[existing], ...normalized };
            } else {
              next.push(normalized);
            }
            return next.sort((a, b) => a.index - b.index);
          });
        }
        break;
      }
      case 'model': {
        const model =
          (payload['model'] as string | undefined) ??
          (payload['profile'] as string | undefined);
        if (model) {
          this.activeModel.set(model);
        }
        break;
      }
      case 'tool': {
        const name =
          (payload['name'] as string | undefined) ??
          (payload['tool'] as string | undefined) ??
          'tool';
        this.toolEvents.update((events) => [
          ...events,
          {
            name,
            status: payload['status'] as string | undefined,
            detail: payload['detail'] as string | undefined,
            timestamp: Date.now(),
          },
        ]);
        break;
      }
      case 'safety': {
        const notice =
          (payload['message'] as string | undefined) ??
          (payload['text'] as string | undefined) ??
          'Safety notice';
        this.safetyNotice.set(notice);
        break;
      }
      case 'error': {
        const errMsg =
          (payload['message'] as string | undefined) ??
          (payload['error'] as string | undefined) ??
          'Stream error';
        this.error.set(errMsg);
        break;
      }
      case 'done': {
        const messageId = payload['messageId'] as string | undefined;
        const conversationId = payload['conversationId'] as string | undefined;
        if (messageId) {
          this.lastMessageId.set(messageId);
        }
        if (conversationId) {
          this.conversationId.set(conversationId);
        }
        break;
      }
      default:
        break;
    }
  }

  private normalizeCitation(payload: Record<string, unknown>): Citation | null {
    const index =
      (payload['index'] as number | undefined) ??
      (payload['Index'] as number | undefined);
    if (typeof index !== 'number') {
      return null;
    }

    const sourcePath =
      (payload['sourcePath'] as string | undefined) ??
      (payload['SourcePath'] as string | undefined) ??
      (payload['source'] as string | undefined);

    return {
      index,
      documentId:
        (payload['documentId'] as string | undefined) ??
        (payload['DocumentId'] as string | undefined),
      title:
        (payload['title'] as string | undefined) ??
        (payload['Title'] as string | undefined) ??
        'Source',
      sourcePath,
      source: sourcePath,
      section:
        (payload['section'] as string | undefined) ??
        (payload['Section'] as string | undefined),
      excerpt:
        (payload['excerpt'] as string | undefined) ??
        (payload['Excerpt'] as string | undefined),
      equipmentId:
        (payload['equipmentId'] as string | undefined) ??
        (payload['EquipmentId'] as string | undefined),
      docType:
        (payload['docType'] as string | undefined) ??
        (payload['DocType'] as string | undefined),
    };
  }
}
