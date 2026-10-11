import { Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { ChatService } from '../../core/chat.service';

@Component({
  selector: 'app-tool-activity',
  standalone: true,
  imports: [MatIconModule],
  template: `
    @if (chat.toolEvents().length > 0 || chat.streaming()) {
      <div class="tool-bar" role="status" aria-live="polite">
        @if (chat.streaming() && chat.toolEvents().length === 0) {
          <mat-icon class="spin">settings</mat-icon>
          <span>Processing plant knowledge…</span>
        }
        @for (event of chat.toolEvents(); track event.timestamp) {
          <div class="tool-item">
            <mat-icon>build_circle</mat-icon>
            <span>{{ event.name }}</span>
            @if (event.status) {
              <span class="status">{{ event.status }}</span>
            }
          </div>
        }
      </div>
    }
  `,
  styles: `
    .tool-bar {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem 1rem;
      align-items: center;
      padding: 0.5rem 0.75rem;
      margin-bottom: 0.75rem;
      background: var(--kestrel-surface-raised);
      border: 1px solid var(--kestrel-border);
      border-radius: 4px;
      font-size: 0.8125rem;
      color: var(--kestrel-text-muted);
    }
    .tool-item {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
    }
    .tool-item mat-icon {
      font-size: 1rem;
      width: 1rem;
      height: 1rem;
      color: var(--kestrel-accent);
    }
    .status {
      font-family: var(--kestrel-font-mono);
      font-size: 0.6875rem;
      text-transform: uppercase;
      color: var(--kestrel-accent);
    }
    .spin {
      animation: spin 1.2s linear infinite;
      font-size: 1.125rem;
      width: 1.125rem;
      height: 1.125rem;
      color: var(--kestrel-accent);
    }
    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }
  `,
})
export class ToolActivityComponent {
  protected readonly chat = inject(ChatService);
}
