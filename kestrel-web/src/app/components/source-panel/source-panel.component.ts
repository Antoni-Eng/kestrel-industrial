import { Component, effect, inject, input, signal } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ApiService } from '../../core/api.service';
import { Citation, SourceDocument } from '../../core/models';

@Component({
  selector: 'app-source-panel',
  standalone: true,
  imports: [MatListModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <aside class="panel">
      <header class="panel-header">
        <mat-icon>menu_book</mat-icon>
        <h2>Sources</h2>
      </header>

      @if (citations().length === 0) {
        <p class="empty">Click a citation chip like [1] to open the source document.</p>
      } @else {
        <mat-nav-list class="cite-list">
          @for (c of citations(); track c.index) {
            <a
              mat-list-item
              href="#"
              [class.active]="selectedIndex() === c.index"
              (click)="selectCitation($event, c)"
            >
              <span matListItemTitle>
                <span class="idx">[{{ c.index }}]</span> {{ c.title }}
              </span>
              @if (c.section) {
                <span matListItemLine>{{ c.section }}</span>
              }
            </a>
          }
        </mat-nav-list>

        <div class="viewer">
          @if (loading()) {
            <div class="loading">
              <mat-spinner diameter="28"></mat-spinner>
            </div>
          } @else if (error()) {
            <p class="error">{{ error() }}</p>
            @if (activeCitation()?.excerpt) {
              <pre class="body">{{ activeCitation()?.excerpt }}</pre>
            }
          } @else if (document()) {
            <div class="doc-meta">
              <h3>{{ document()!.title }}</h3>
              <p>{{ document()!.sourcePath }}</p>
              @if (activeCitation()?.section) {
                <p class="section-tag">Cited section: {{ activeCitation()!.section }}</p>
              }
            </div>
            <pre class="body">{{ document()!.content }}</pre>
          } @else if (activeCitation()?.excerpt) {
            <pre class="body">{{ activeCitation()!.excerpt }}</pre>
          }
        </div>
      }
    </aside>
  `,
  styles: `
    .panel {
      height: 100%;
      display: flex;
      flex-direction: column;
      background: var(--kestrel-surface);
      border-left: 1px solid var(--kestrel-border);
      min-width: 320px;
      max-width: 420px;
    }
    .panel-header {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 1rem 1rem 0.75rem;
      border-bottom: 1px solid var(--kestrel-border);
      flex-shrink: 0;
    }
    .panel-header h2 {
      margin: 0;
      font-size: 0.8125rem;
      font-weight: 600;
      letter-spacing: 0.06em;
      text-transform: uppercase;
      font-family: var(--kestrel-font-mono);
    }
    .panel-header mat-icon {
      color: var(--kestrel-accent);
    }
    .empty {
      padding: 1rem;
      margin: 0;
      color: var(--kestrel-text-muted);
      font-size: 0.875rem;
    }
    .cite-list {
      flex-shrink: 0;
      max-height: 30%;
      overflow: auto;
      border-bottom: 1px solid var(--kestrel-border);
    }
    .idx {
      font-family: var(--kestrel-font-mono);
      color: var(--kestrel-accent);
      margin-right: 0.25rem;
    }
    a.active {
      background: color-mix(in srgb, var(--kestrel-accent) 14%, transparent);
    }
    .viewer {
      flex: 1;
      overflow: auto;
      padding: 0.75rem 1rem 1.25rem;
    }
    .doc-meta h3 {
      margin: 0 0 0.35rem;
      font-size: 1rem;
    }
    .doc-meta p {
      margin: 0;
      font-size: 0.75rem;
      color: var(--kestrel-text-muted);
      word-break: break-all;
    }
    .section-tag {
      margin-top: 0.5rem !important;
      color: var(--kestrel-accent) !important;
      font-family: var(--kestrel-font-mono);
    }
    .body {
      margin: 0.75rem 0 0;
      white-space: pre-wrap;
      font-family: var(--kestrel-font-mono);
      font-size: 0.75rem;
      line-height: 1.5;
    }
    .loading {
      display: flex;
      justify-content: center;
      padding: 2rem;
    }
    .error {
      color: var(--kestrel-danger);
      font-size: 0.875rem;
    }
  `,
})
export class SourcePanelComponent {
  private readonly api = inject(ApiService);

  readonly citations = input<Citation[]>([]);
  readonly selectedIndex = input<number | null>(null);

  protected readonly document = signal<SourceDocument | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly activeCitation = signal<Citation | null>(null);

  private lastLoadedKey: string | null = null;

  constructor() {
    effect(() => {
      const idx = this.selectedIndex();
      const list = this.citations();
      if (idx == null) {
        return;
      }
      const citation = list.find((c) => c.index === idx);
      if (citation) {
        void this.openCitation(citation);
      }
    });
  }

  selectCitation(event: Event, citation: Citation): void {
    event.preventDefault();
    void this.openCitation(citation);
  }

  private async openCitation(citation: Citation): Promise<void> {
    this.activeCitation.set(citation);
    const docId = citation.documentId;
    if (!docId) {
      this.document.set(null);
      this.error.set('No document id on this citation.');
      return;
    }

    const key = `${docId}#${citation.index}`;
    if (this.lastLoadedKey === key && this.document()) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.api.getDocument(docId).subscribe({
      next: (doc) => {
        this.document.set(doc);
        this.lastLoadedKey = key;
        this.loading.set(false);
      },
      error: () => {
        this.document.set(null);
        this.error.set('Could not load full document; showing cited excerpt.');
        this.loading.set(false);
      },
    });
  }
}
