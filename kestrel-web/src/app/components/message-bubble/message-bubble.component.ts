import { Component, input, output } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { ChatMessage, Citation } from '../../core/models';
import { FeedbackButtonsComponent } from '../feedback-buttons/feedback-buttons.component';

@Component({
  selector: 'app-message-bubble',
  standalone: true,
  imports: [MatChipsModule, FeedbackButtonsComponent],
  template: `
    <article
      class="bubble"
      [class.user]="message().role === 'user'"
      [class.assistant]="message().role === 'assistant'"
    >
      <header class="meta">
        <span class="role">{{ message().role === 'user' ? 'Operator' : 'Kestrel' }}</span>
      </header>
      <p class="content">{{ message().content }}</p>
      @if (message().citations?.length) {
        <mat-chip-set class="citations" aria-label="Citations">
          @for (c of message().citations; track c.index) {
            <mat-chip (click)="citationClick.emit(c)">[{{ c.index }}]</mat-chip>
          }
        </mat-chip-set>
      }
      @if (message().role === 'assistant') {
        <app-feedback-buttons
          [messageId]="message().id"
          [conversationId]="message().conversationId ?? null"
        />
      }
    </article>
  `,
  styles: `
    .bubble {
      max-width: min(640px, 100%);
      padding: 0.75rem 1rem;
      border-radius: 4px;
      border: 1px solid var(--kestrel-border);
      background: var(--kestrel-surface-raised);
    }
    .bubble.user {
      align-self: flex-end;
      border-color: var(--kestrel-accent-dim);
      background: var(--kestrel-user-bg);
    }
    .bubble.assistant {
      align-self: flex-start;
    }
    .meta {
      margin-bottom: 0.35rem;
    }
    .role {
      font-family: var(--kestrel-font-mono);
      font-size: 0.6875rem;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: var(--kestrel-text-muted);
    }
    .content {
      margin: 0;
      white-space: pre-wrap;
      line-height: 1.55;
      font-size: 0.9375rem;
    }
    .citations {
      margin-top: 0.65rem;
    }
    .citations mat-chip {
      cursor: pointer;
      font-family: var(--kestrel-font-mono);
      font-size: 0.75rem;
    }
  `,
})
export class MessageBubbleComponent {
  readonly message = input.required<ChatMessage>();
  readonly citationClick = output<Citation>();
}
