import { Component, input, output } from '@angular/core';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Note, ProjectNote, TaskNote } from './api.service';
import { NoteContentComponent } from './note-content.component';
import { DesktopPinComponent } from './desktop-pin.component';
import { formatNoteTime } from './time-format';

@Component({
  selector: 'app-note-list',
  imports: [DatePipe, NgTemplateOutlet, RouterLink, NoteContentComponent, DesktopPinComponent],
  styles: `
    .note-card.compact { padding: 11.5px 12.5px 7.5px; }
    .compact .note-meta { justify-content: flex-start; flex-wrap: wrap; border-bottom: 1px solid #c4cebd; padding-bottom: 5px; }
    .compact .note-meta time { white-space: nowrap; }
    .compact app-note-content { margin: 5px 0 0; }
    .compact .note-actions { padding: 0; border: 0; gap: 12px; }
  `,
  template: `
    <div class="note-list">
      @for (note of notes(); track note.id) {
        <article class="note-card" [class.compact]="compact()">
          <ng-template #noteActions>
            <app-desktop-pin kind="note" [targetId]="note.id" [inlineError]="true" />
            <button class="text-button" (click)="edited.emit(note)" [disabled]="busy()">Изменить</button>
            @if (confirmId === note.id) {
              <span>Удалить заметку?</span>
              <button class="text-button danger" (click)="removed.emit(note); confirmId = null" [disabled]="busy()">Да, удалить</button>
              <button class="text-button" (click)="confirmId = null">Отмена</button>
            } @else {
              <button class="text-button danger" (click)="confirmId = note.id" [disabled]="busy()">Удалить</button>
            }
          </ng-template>
          <div class="note-meta">
            @if (showContext()) { <a class="tag" [routerLink]="noteContextLink(note)">{{ noteContextLabel(note) }}</a> }
            <time [attr.datetime]="note.date + 'T' + note.time">{{ note.date | date:'dd.MM.yyyy' }} · {{ formatNoteTime(note.time) }}</time>
            <span>Оценка {{ note.rating }}/5</span>
            @if (compact()) { <div class="note-actions"><ng-container [ngTemplateOutlet]="noteActions" /></div> }
          </div>
          <div class="note-preview" role="button" tabindex="0" aria-label="Открыть полный текст заметки"
            (click)="viewed.emit(note)" (keydown.enter)="viewed.emit(note)"
            (keydown.space)="$event.preventDefault(); viewed.emit(note)">
            <app-note-content [text]="note.text" [preview]="true" [previewLines]="previewLines()" />
          </div>
          @if (!compact()) {
            <div class="note-actions"><ng-container [ngTemplateOutlet]="noteActions" /></div>
          }

        </article>
      } @empty {
        <div class="empty-state">
          <span class="empty-symbol" aria-hidden="true">✎</span>
          <h2>Место для ваших мыслей</h2>
          <p>Пока нет заметок. Нажмите «Новая заметка», чтобы сохранить первую мысль.</p>
        </div>
      }
    </div>
  `
})
export class NoteListComponent<T extends Note | ProjectNote | TaskNote = Note> {
  readonly formatNoteTime = formatNoteTime;
  readonly notes = input.required<T[]>();
  readonly contextLabel = input('');
  readonly contextLink = input<(string | number)[]>([]);
  readonly busy = input(false);
  readonly showContext = input(true);
  readonly compact = input(false);
  readonly previewLines = input(3);
  readonly edited = output<T>();
  readonly viewed = output<T>();
  readonly removed = output<T>();
  confirmId: number | null = null;
  noteContextLabel(note: T) { return 'subjectName' in note ? note.subjectName : this.contextLabel(); }
  noteContextLink(note: T) { return 'subjectId' in note ? ['/subjects', note.subjectId] : this.contextLink(); }
}

