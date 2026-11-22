import { Component, computed, input, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import type { Session } from './sessions-api';
import { SessionClockComponent } from './session-clock.component';

@Component({
  selector: 'app-session-row',
  imports: [DatePipe, RouterLink, SessionClockComponent],
  styles: `
    .session-row { display: flex; align-items: center; gap: 12px; min-width: 650px; padding: 0 20px; }
    .session-target { display: grid; grid-template-columns: max-content fit-content(70%) minmax(0, 1fr); flex: 1; align-self: stretch; align-items: stretch; min-width: 0; min-height: 48px; color: inherit; text-decoration: none; font-size: 14px; white-space: nowrap; }
    .session-target:hover .session-context { text-decoration: underline; }
    .session-times { display: inline-flex; align-items: center; gap: 6px; padding-right: 12px; color: #243c36; font-variant-numeric: tabular-nums; }
    .session-times time { color: inherit; }
    .session-code { display: inline-flex; align-items: center; gap: 5px; flex-shrink: 0; font-family: monospace; }
    .session-code svg { width: 17px; height: 17px; }
    .session-divider { color: #97a28d; flex-shrink: 0; }
    .session-code-cell { display: flex; align-items: center; gap: 8px; min-width: 0; overflow: hidden; padding: 10px 12px; border-left: 1px solid #456b58; }
    .session-context { min-width: 0; overflow: hidden; text-overflow: ellipsis; font-weight: 650; }
    .session-text-cell { display: flex; align-items: center; min-width: 0; padding: 10px 0 10px 12px; }
    .session-text-cell.has-text { border-left: 1px solid #456b58; }
    .session-text { min-width: 0; overflow: hidden; text-overflow: ellipsis; color: #64745e; }
    .session-status { display: inline-grid; place-items: center; flex: 0 0 28px; width: 28px; height: 28px; color: #52794b; }
    .session-status svg { width: 20px; height: 20px; }
    .session-status.session-running { padding: 0; border-radius: 50%; color: #b83b32; background: #fbe9e5; }
    .session-status.session-running::before, .session-status.session-running::after { inset-inline-start: 6px; }
    `,
  template: `
            <article class="panel session-row">
              <a class="session-target" [routerLink]="sessionLink(session())">
                <span class="session-times">
                  <time [attr.datetime]="session().startTime" [attr.title]="session().startTime | date:'dd.MM.yyyy HH:mm'">{{ session().startTime | date:'HH:mm' }}</time>
                  <span>–</span>
                  <span [class.session-end-time]="!session().endTime">
                    @if (session().endTime) { <time [attr.datetime]="session().endTime" [attr.title]="session().endTime | date:'dd.MM.yyyy HH:mm'">{{ session().endTime | date:'HH:mm' }}</time> }
                    @else { <app-session-clock /> }
                  </span>
                  <span aria-hidden="true">:</span>
                  <span title="Общее время сессии" [attr.aria-label]="'Длительность сессии ' + elapsedTime()">{{ elapsedTime() }}</span>
                </span>
                <span class="session-code-cell" [title]="fullCode() + ' [' + targetName() + ']'">
                @for (code of codes(); track $index) {
                  @if ($index > 0) { <span class="session-divider" aria-hidden="true">\\</span> }
                  <span class="session-code" [attr.title]="['Предмет', 'Проект', 'Задача'][$index]" [attr.aria-label]="['Предмет', 'Проект', 'Задача'][$index] + ' ' + code">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                      @switch ($index) {
                        @case (0) { <path d="M12 5v16M12 5C9 3 5 3 2 4v15c3-1 7-1 10 2 3-3 7-3 10-2V4c-3-1-7-1-10 1Z" /> }
                        @case (1) { <path d="M3 7V5h6l2 2h10v12H3Z" /> }
                        @case (2) { <rect x="4" y="3" width="16" height="18" rx="2" /><path d="m8 12 3 3 5-6" /> }
                      }
                    </svg>{{ code }}
                  </span>
                }
                @if (targetName().trim()) {
                  <span class="session-context" [title]="targetName()">[{{ targetName() }}]</span>
                }
                </span>
                <span class="session-text-cell" [class.has-text]="!!session().text.trim()">
                  <span class="session-text" [title]="session().text">{{ session().text }}</span>
                </span>
              </a>
              <ng-content />
              <span class="session-status" [class.session-running]="!session().endTime" role="img" [attr.aria-label]="session().endTime ? 'Сессия завершена' : 'Сессия идёт'" [attr.title]="session().endTime ? 'Сессия завершена' : 'Сессия идёт — время учитывается'">
                @if (session().endTime) { <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9" /><path d="m8 12 3 3 5-6" /></svg> }
              </span>
            </article>
  `
})
export class SessionRowComponent {
  readonly session = input.required<Session>();
  readonly fullCode = input.required<string>();
  readonly targetName = input.required<string>();
  readonly codes = computed(() => this.fullCode() ? this.fullCode().split(' \\ ') : []);
  private readonly clock = viewChild(SessionClockComponent);
  readonly elapsedTime = computed(() => {
    const session = this.session();
    const end = session.endTime ? Date.parse(session.endTime) : (this.clock()?.now().getTime() ?? Date.now());
    const minutes = Math.max(0, Math.floor((end - Date.parse(session.startTime)) / 60_000));
    const hours = String(Math.floor(minutes / 60)).padStart(2, '0');
    return `${hours}:${String(minutes % 60).padStart(2, '0')}`;
  });
  sessionLink(session: Session): (string | number)[] {
    if (session.taskId !== null && session.projectId !== null) return ['/projects', session.projectId, 'tasks', session.taskId];
    return session.projectId !== null ? ['/projects', session.projectId] : ['/subjects', session.subjectId];
  }
}
