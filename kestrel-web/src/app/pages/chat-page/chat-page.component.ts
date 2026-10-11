import {
  AfterViewInit,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSidenavModule } from '@angular/material/sidenav';
import { ChatService } from '../../core/chat.service';
import { Citation } from '../../core/models';
import { EquipmentFilterComponent } from '../../components/equipment-filter/equipment-filter.component';
import { MessageListComponent } from '../../components/message-list/message-list.component';
import { ModelBadgeComponent } from '../../components/model-badge/model-badge.component';
import { SourcePanelComponent } from '../../components/source-panel/source-panel.component';
import { ToolActivityComponent } from '../../components/tool-activity/tool-activity.component';

@Component({
  selector: 'app-chat-page',
  standalone: true,
  imports: [
    FormsModule,
    MatSidenavModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    EquipmentFilterComponent,
    ModelBadgeComponent,
    ToolActivityComponent,
    MessageListComponent,
    SourcePanelComponent,
  ],
  templateUrl: './chat-page.component.html',
  styleUrl: './chat-page.component.scss',
})
export class ChatPageComponent implements AfterViewInit {
  protected readonly chat = inject(ChatService);

  private readonly equipmentFilter = viewChild(EquipmentFilterComponent);

  protected draft = '';
  protected readonly sourcesOpen = signal(true);
  protected readonly highlightIndex = signal<number | null>(null);

  protected readonly panelCitations = computed(() => {
    const fromStream = this.chat.citations();
    const fromMessages = this.chat
      .messages()
      .flatMap((m) => m.citations ?? []);
    const map = new Map<number, Citation>();
    for (const c of [...fromMessages, ...fromStream]) {
      map.set(c.index, c);
    }
    return [...map.values()].sort((a, b) => a.index - b.index);
  });

  ngAfterViewInit(): void {
    const filter = this.equipmentFilter();
    filter?.registerHandler(() => undefined);
    void this.chat.loadModelInfo();
  }

  onCitationClick(c: Citation): void {
    this.highlightIndex.set(c.index);
    this.sourcesOpen.set(true);
  }

  async submit(): Promise<void> {
    const text = this.draft.trim();
    if (!text || this.chat.streaming()) {
      return;
    }
    this.draft = '';
    const equipmentId = this.equipmentFilter()?.selectedEquipmentId() ?? null;
    await this.chat.send({
      message: text,
      equipmentId,
      conversationId: this.chat.conversationId() ?? undefined,
    });
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      void this.submit();
    }
  }
}
