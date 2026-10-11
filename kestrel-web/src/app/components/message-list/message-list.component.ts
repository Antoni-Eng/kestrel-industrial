import { Component, inject, output } from '@angular/core';
import { ChatService } from '../../core/chat.service';
import { Citation } from '../../core/models';
import { MessageBubbleComponent } from '../message-bubble/message-bubble.component';

@Component({
  selector: 'app-message-list',
  standalone: true,
  imports: [MessageBubbleComponent],
  template: `
    <div class="messages" role="log" aria-live="polite">
      @for (msg of chat.messages(); track msg.id) {
        <app-message-bubble
          [message]="msg"
          (citationClick)="citationClick.emit($event)"
        />
      }
      @if (chat.streaming()) {
        <app-message-bubble
          [message]="{
            id: 'streaming',
            role: 'assistant',
            content: chat.answerText() || 'Generating answer…',
            citations: chat.citations(),
          }"
          (citationClick)="citationClick.emit($event)"
        />
      }
      @if (chat.error()) {
        <p class="error" role="alert">{{ chat.error() }}</p>
      }
      @if (chat.safetyNotice()) {
        <p class="safety" role="alert">{{ chat.safetyNotice() }}</p>
      }
    </div>
  `,
  styles: `
    .messages {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      flex: 1;
      overflow-y: auto;
      padding: 0.25rem 0.125rem 1rem;
      min-height: 200px;
    }
    .error {
      color: var(--kestrel-danger);
      font-size: 0.875rem;
      margin: 0;
    }
    .safety {
      color: var(--kestrel-warning);
      font-size: 0.875rem;
      margin: 0;
      padding: 0.5rem 0.75rem;
      border: 1px solid var(--kestrel-warning);
      border-radius: 4px;
      background: color-mix(in srgb, var(--kestrel-warning) 12%, transparent);
    }
  `,
})
export class MessageListComponent {
  protected readonly chat = inject(ChatService);
  readonly citationClick = output<Citation>();
}
