import { Component, input, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ApiService } from '../../core/api.service';

@Component({
  selector: 'app-feedback-buttons',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatSnackBarModule],
  template: `
    <div class="feedback" [class.submitted]="submitted()">
      <button
        mat-icon-button
        type="button"
        aria-label="Helpful"
        [disabled]="submitted()"
        (click)="send('positive')"
      >
        <mat-icon>thumb_up</mat-icon>
      </button>
      <button
        mat-icon-button
        type="button"
        aria-label="Not helpful"
        [disabled]="submitted()"
        (click)="send('negative')"
      >
        <mat-icon>thumb_down</mat-icon>
      </button>
    </div>
  `,
  styles: `
    .feedback {
      display: inline-flex;
      gap: 0.125rem;
      opacity: 0.7;
    }
    .feedback.submitted {
      opacity: 0.4;
    }
    button mat-icon {
      font-size: 1.125rem;
    }
  `,
})
export class FeedbackButtonsComponent {
  readonly messageId = input.required<string>();
  readonly conversationId = input<string | null>(null);

  private readonly api = inject(ApiService);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly submitted = signal(false);

  send(rating: 'positive' | 'negative'): void {
    if (this.submitted()) {
      return;
    }
    this.api
      .submitFeedback({
        conversationId: this.conversationId() ?? 'unknown',
        messageId: this.messageId(),
        helpful: rating === 'positive',
      })
      .subscribe({
        next: () => {
          this.submitted.set(true);
          this.snackBar.open('Feedback recorded', undefined, { duration: 2000 });
        },
        error: () => {
          this.snackBar.open('Could not send feedback', undefined, {
            duration: 3000,
          });
        },
      });
  }
}
