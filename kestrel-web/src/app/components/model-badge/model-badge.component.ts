import { Component, inject } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { ChatService } from '../../core/chat.service';

@Component({
  selector: 'app-model-badge',
  standalone: true,
  imports: [MatChipsModule],
  template: `
    <mat-chip-set aria-label="Active model">
      <mat-chip class="model-chip">
        <span class="dot" aria-hidden="true"></span>
        {{ chat.activeModel() }}
      </mat-chip>
    </mat-chip-set>
  `,
  styles: `
    .model-chip {
      --mdc-chip-label-text-size: 0.75rem;
      font-family: var(--kestrel-font-mono);
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }
    .dot {
      display: inline-block;
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: var(--kestrel-accent);
      margin-right: 0.35rem;
      box-shadow: 0 0 6px var(--kestrel-accent-glow);
    }
  `,
})
export class ModelBadgeComponent {
  protected readonly chat = inject(ChatService);
}
