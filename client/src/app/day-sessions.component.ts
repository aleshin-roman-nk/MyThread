import { Component, input, output } from '@angular/core';
import { DaySession, Session } from './api.service';
import { SessionRowComponent } from './session-row.component';

@Component({
  selector: 'app-day-sessions',
  imports: [SessionRowComponent],
  host: { '[class.scrollable]': 'scrollable()' },
  styles: `
    :host { display: block; }
    :host(.scrollable) > section { display: flex; flex-direction: column; height: 100%; }
    :host(.scrollable) .section-heading { flex-shrink: 0; }
    :host(.movable-heading) .section-heading { cursor: grab; touch-action: none; user-select: none; }
    :host(.movable-heading) .section-heading:active { cursor: grabbing; }
    .session-heading-actions { display: flex; align-items: center; gap: 8px; }
    .session-scroll { overflow-x: auto; padding: 2px; }
    :host(.scrollable) .session-scroll {
      flex: 1;
      min-height: 0;
      overflow: auto;
      border: 1px solid #ded5b8;
      border-radius: 6px;
    }
    .session-scroll .note-list { gap: 6px; }
    .session-close { width: 28px; height: 28px; flex-shrink: 0; padding: 5px; }
    .session-close svg { width: 16px; height: 16px; }
  `,
  template: `
    <section aria-label="Сессии за выбранный день">
      <div class="section-heading" (pointerdown)="headingPointerDown.emit($event)">
        <h2>Сессии за день</h2>
        <div class="session-heading-actions">
          @if (!loading() && !unavailable()) { <span class="count">{{ sessions().length }}</span> }
          <ng-content select="[session-heading-action]" />
        </div>
      </div>
      @if (loading()) {
        <p role="status">Загружаем сессии…</p>
      } @else if (!unavailable()) {
        <div class="session-scroll"><div class="note-list">
          @for (item of sessions(); track item.session.id) {
            <app-session-row [session]="item.session" [fullCode]="item.fullCode" [targetName]="item.targetName">
              @if (allowClose() && !item.session.endTime) {
                <button class="session-close" type="button" (click)="closeSession.emit(item.session)" [disabled]="closing()"
                        aria-label="Завершить сессию" title="Завершить сессию">
                  <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><rect x="5" y="5" width="14" height="14" rx="2" /></svg>
                </button>
              }
            </app-session-row>
          } @empty {
            <div class="empty-state"><p>За выбранный день пока нет сессий.</p></div>
          }
        </div></div>
      }
    </section>
  `
})
export class DaySessionsComponent {
  readonly sessions = input.required<DaySession[]>();
  readonly loading = input(false);
  readonly unavailable = input(false);
  readonly scrollable = input(false);
  readonly allowClose = input(false);
  readonly closing = input(false);
  readonly closeSession = output<Session>();
  readonly headingPointerDown = output<PointerEvent>();
}
