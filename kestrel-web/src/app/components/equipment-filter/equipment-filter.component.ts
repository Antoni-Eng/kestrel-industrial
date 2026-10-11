import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { ApiService } from '../../core/api.service';
import { Equipment } from '../../core/models';

@Component({
  selector: 'app-equipment-filter',
  standalone: true,
  imports: [FormsModule, MatFormFieldModule, MatSelectModule],
  template: `
    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="filter-field">
      <mat-label>Equipment</mat-label>
      <mat-select
        [(ngModel)]="selectedId"
        (ngModelChange)="onSelectionChange($event)"
        [disabled]="loading()"
      >
        <mat-option [value]="null">All plant assets</mat-option>
        @for (item of equipment(); track item.id) {
          <mat-option [value]="item.id">
            {{ item.name }}
            @if (item.location) {
              <span class="loc"> — {{ item.location }}</span>
            }
          </mat-option>
        }
      </mat-select>
    </mat-form-field>
  `,
  styles: `
    .filter-field {
      min-width: 220px;
      flex: 1;
      max-width: 360px;
    }
    .loc {
      opacity: 0.65;
      font-size: 0.875em;
    }
  `,
})
export class EquipmentFilterComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly equipment = signal<Equipment[]>([]);
  readonly loading = signal(true);
  selectedId: string | null = null;

  private selectionHandler: ((id: string | null) => void) | null = null;

  ngOnInit(): void {
    this.api.getEquipment().subscribe({
      next: (items) => {
        this.equipment.set(
          items.map((item) => ({
            ...item,
            location:
              item.location ??
              [item.plantId, item.area].filter(Boolean).join(' / '),
          })),
        );
        this.loading.set(false);
      },
      error: () => {
        this.equipment.set([]);
        this.loading.set(false);
      },
    });
  }

  onSelectionChange(id: string | null): void {
    this.selectionHandler?.(id);
  }

  /** Chat page registers to receive equipment scope changes. */
  registerHandler(handler: (id: string | null) => void): void {
    this.selectionHandler = handler;
  }

  selectedEquipmentId(): string | null {
    return this.selectedId;
  }
}
