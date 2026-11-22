import { Component, computed, DestroyRef, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import { today } from '../../shared/today';
import type { Project } from '../projects/projects-api';
import type { Subject } from '../subjects/subjects-api';
import { SessionEditComponent } from './session-edit.component';
import { DateInputComponent } from '../../shared/date-input.component';
import { SessionRowComponent } from './session-row.component';
import { SessionState } from './session-state';
import { Session, SessionsApi } from './sessions-api';

@Component({
  selector: 'app-sessions-panel',
  imports: [FormsModule, RouterLink, SessionEditComponent, DateInputComponent, SessionRowComponent],
  styles: `
    .session-scroll { overflow-x: auto; padding: 2px; }
    .session-scroll .note-list { gap: 6px; }
    app-session-row { display: block; min-width: 850px; }
    .session-actions { display: flex; align-items: center; gap: 6px; flex-shrink: 0; white-space: nowrap; }
    .session-action { width: 28px; height: 28px; padding: 5px; }
    .session-action svg { width: 16px; height: 16px; }
    .session-action.danger { color: #8f392b; }
    .session-action.danger:hover { background: #fff1eb; }
    .session-date-bar { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; padding: 16px; margin-bottom: 24px; background: #e9eddf; border-radius: 12px; }
    .session-date-bar label { margin: 0; }
    .session-date-bar app-date-input { width: 175px; }
    .period-switch { display: inline-flex; gap: 4px; }
    .period-switch button { padding: 10px 14px; }
    .period-switch .selected { background: #214f46; color: white; border-color: #214f46; }
    .selected-month { color: #496642; font-size: 14px; }
    .date-arrow { width: 42px; height: 42px; padding: 8px; font-size: 20px; }
    .date-arrow svg { width: 24px; height: 24px; }
    .date-label-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
    .period-total { display: flex; flex-wrap: wrap; align-items: baseline; gap: 8px; margin-left: auto; color: #496642; }
    .period-total strong { color: #214f46; font-size: 22px; font-variant-numeric: tabular-nums; }
  `,
  template: `
      <section [attr.aria-label]="taskId() !== null ? 'Сессии задачи' : projectId() === null ? 'Сессии предмета' : 'Сессии проекта'">
        <div class="section-heading"><h2>Сессии</h2><span class="count">{{ visibleSessions().length }} {{ period() === 'day' ? 'за день' : 'за месяц' }}</span></div>
        <div class="session-date-bar" aria-label="Выбор даты сессий">
          <div class="period-switch" role="group" aria-label="Период сессий">
            <button class="secondary" type="button" [class.selected]="period() === 'day'" [attr.aria-pressed]="period() === 'day'" (click)="period.set('day')">За день</button>
            <button class="secondary" type="button" [class.selected]="period() === 'month'" [attr.aria-pressed]="period() === 'month'" (click)="period.set('month')">За месяц</button>
          </div>
          <button class="secondary date-arrow" type="button" [attr.aria-label]="period() === 'day' ? 'Предыдущий день' : 'Предыдущий месяц'" [title]="period() === 'day' ? 'Предыдущий день' : 'Предыдущий месяц'" (click)="shiftPeriod(-1)">←</button>
          <label for="session-date" [class.date-label-hidden]="showTimeSummary()">{{ period() === 'day' ? 'Дата' : 'Дата в месяце' }}</label>
          <app-date-input inputId="session-date" [ngModel]="selectedDate()" (ngModelChange)="changeDate($event)" required />
          <button class="secondary date-arrow" type="button" [attr.aria-label]="period() === 'day' ? 'Следующий день' : 'Следующий месяц'" [title]="period() === 'day' ? 'Следующий день' : 'Следующий месяц'" (click)="shiftPeriod(1)">→</button>
          <button class="secondary" [class.date-arrow]="showTimeSummary()" type="button" (click)="changeDate(todayValue())" aria-label="Установить сегодняшнюю дату" title="Сегодня">
            @if (showTimeSummary()) {
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M8 3v4M16 3v4M3 11h18"/><circle cx="12" cy="16" r="2" fill="currentColor" stroke="none"/></svg>
            } @else { Сегодня }
          </button>
          @if (period() === 'month') { <span class="selected-month">{{ selectedMonth() }}</span> }
          @if (showTimeSummary()) {
            <div class="period-total" title="Сумма времени завершённых сессий в выбранном периоде"><span>{{ period() === 'day' ? 'Всего за день' : 'Всего за месяц' }}</span><strong>{{ periodWorkTime() }}</strong></div>
          }
        </div>
        @if (sessionError()) { <p class="error" role="alert">{{ sessionError() }}</p> }
        @if (currentSession(); as current) {
          <p class="muted">Уже есть открытая сессия. Завершите её, чтобы начать следующую.
            @if (!matchesScope(current)) { <a class="text-button" [routerLink]="sessionLink(current)" (click)="showSession(current)">Перейти к текущей сессии →</a> }
            @else if (!matchesPeriod(current.startTime)) { <button class="text-button" type="button" (click)="changeDate(sessionDate(current.startTime))">Показать текущую сессию</button> }
          </p>
        }
        <div class="session-scroll"><div class="note-list">
          @for (session of visibleSessions(); track session.id) {
            @let context = sessionContext(session);
            <app-session-row [session]="session" [fullCode]="context.fullCode" [targetName]="context.name">
              <div class="session-actions">
                @if (!session.endTime) {
                  <button class="session-action" type="button" (click)="closeSession(session)" [disabled]="sessionBusy()"
                    [attr.aria-label]="closingId() === session.id ? 'Завершаем сессию…' : 'Завершить сессию'" title="Завершить сессию" [attr.aria-busy]="closingId() === session.id">
                    <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><rect x="5" y="5" width="14" height="14" rx="2" /></svg>
                  </button>
                }
                <button class="secondary session-action" type="button" (click)="editingSession.set(session)" [disabled]="sessionBusy()" aria-label="Изменить сессию" title="Изменить сессию">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m15 5 4 4M4 20l4.5-1 12-12a2.8 2.8 0 0 0-4-4l-12 12L4 20Z" /></svg>
                </button>
                @if (confirmDeleteId() === session.id) {
                  <span>Удалить сессию?</span>
                  <button class="text-button danger" type="button" (click)="deleteSession(session)" [disabled]="sessionBusy()">{{ deletingId() === session.id ? 'Удаляем…' : 'Да, удалить' }}</button>
                  <button class="text-button" type="button" (click)="confirmDeleteId.set(null)" [disabled]="sessionBusy()">Отмена</button>
                } @else {
                  <button class="secondary session-action danger" type="button" (click)="confirmDeleteId.set(session.id)" [disabled]="sessionBusy()" aria-label="Удалить сессию" title="Удалить сессию">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 6h18M9 6V4h6v2M5 6l1 15h12l1-15M10 10v7M14 10v7" /></svg>
                  </button>
                }
              </div>
            </app-session-row>
          } @empty { <p class="muted">{{ period() === 'day' ? 'За выбранный день сессий нет.' : 'За выбранный месяц сессий нет.' }}</p> }
        </div></div>
      </section>
      @if (editingSession(); as session) { <app-session-edit [session]="session" (saved)="onSessionUpdated($event)" (cancelled)="editingSession.set(null)" /> }
  `
})
export class SessionsPanelComponent {
  private readonly api = inject(SessionsApi);
  private readonly sessionState = inject(SessionState);
  private readonly destroyRef = inject(DestroyRef);
  readonly subjectId = input.required<number>();
  readonly subject = input.required<Subject>();
  readonly projects = input.required<Project[]>();
  readonly projectId = input<number | null>(null);
  readonly taskId = input<number | null>(null);
  readonly showTimeSummary = input(false);
  readonly initialSessions = input.required<Session[]>();
  readonly message = output<string>();
  readonly sessions = signal<Session[]>([]);
  readonly selectedDate = signal(today());
  readonly period = signal<'day' | 'month'>('day');
  readonly selectedMonth = computed(() => new Date(`${this.selectedDate()}T12:00:00`)
    .toLocaleDateString('ru-RU', { month: 'long', year: 'numeric' }));
  readonly todayValue = today;
  readonly visibleSessions = computed(() => this.sessions().filter(session => this.matchesPeriod(session.startTime)));
  readonly totalWorkTime = computed(() => this.formatWorkTime(this.closedWorkMilliseconds()));
  readonly periodWorkTime = computed(() => {
    const start = new Date(`${this.selectedDate()}T00:00:00`);
    if (this.period() === 'month') start.setDate(1);
    const end = new Date(start);
    if (this.period() === 'month') end.setMonth(end.getMonth() + 1);
    else end.setDate(end.getDate() + 1);
    return this.formatWorkTime(this.closedWorkMilliseconds(start.getTime(), end.getTime()));
  });
  readonly currentSession = this.sessionState.activeSession;
  readonly starting = signal(false);
  readonly closingId = signal<number | null>(null);
  readonly deletingId = signal<number | null>(null);
  readonly confirmDeleteId = signal<number | null>(null);
  readonly sessionBusy = computed(() => this.starting() || this.closingId() !== null || this.deletingId() !== null);
  readonly editingSession = signal<Session | null>(null);
  readonly sessionError = signal('');

  constructor() {
    effect(() => {
      this.subjectId(); this.projectId(); this.taskId();
      this.sessions.set(this.initialSessions());
      this.editingSession.set(null); this.confirmDeleteId.set(null); this.sessionError.set('');
    });
    effect(() => {
      const session = this.sessionState.sessionToShow();
      // Only the destination panel consumes the request. A subject panel also
      // lists project/task sessions, but must leave their navigation request intact.
      if (session && session.subjectId === this.subjectId()
        && session.projectId === this.projectId() && session.taskId === this.taskId()) {
        this.period.set('day');
        this.changeDate(this.sessionDate(session.startTime));
        this.sessionState.sessionToShow.set(null);
      }
    });
  }
  showSession(session: Session) { this.sessionState.sessionToShow.set(session); }
  private closedWorkMilliseconds(from = -Infinity, through = Infinity) {
    return this.sessions().reduce((total, session) => {
      if (!session.endTime) return total;
      return total + Math.max(0, Math.min(Date.parse(session.endTime), through) - Math.max(Date.parse(session.startTime), from));
    }, 0);
  }
  private formatWorkTime(milliseconds: number) {
    const minutes = Math.floor(milliseconds / 60000);
    return `${Math.floor(minutes / 60)} ч ${String(minutes % 60).padStart(2, '0')} мин`;
  }
  sessionContext(session: Session) {
    const project = this.projects().find(project => project.id === session.projectId);
    const task = project?.tasks.find(task => task.id === session.taskId);
    return { fullCode: task?.fullCode ?? project?.fullCode ?? this.subject().shortName,
      name: task?.name ?? project?.name ?? this.subject().name };
  }
  private scopeKey() { return `${this.subjectId()}/${this.projectId() ?? ''}/${this.taskId() ?? ''}`; }
  matchesScope(session: Session) {
    return session.subjectId === this.subjectId()
      && (this.projectId() === null || session.projectId === this.projectId())
      && (this.taskId() === null || session.taskId === this.taskId());
  }
  sessionLink(session: Session): (string | number)[] {
    if (session.taskId !== null && session.projectId !== null) return ['/projects', session.projectId, 'tasks', session.taskId];
    return session.projectId !== null ? ['/projects', session.projectId] : ['/subjects', session.subjectId];
  }
  sessionDate(value: string) {
    const date = new Date(value);
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
  }
  changeDate(value: string) {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || !Number.isFinite(new Date(`${value}T12:00:00`).getTime())) return;
    this.selectedDate.set(value);
  }
  matchesPeriod(startTime: string) {
    const date = this.sessionDate(startTime);
    return this.period() === 'day' ? date === this.selectedDate() : date.slice(0, 7) === this.selectedDate().slice(0, 7);
  }
  shiftPeriod(direction: number) {
    const date = new Date(`${this.selectedDate()}T12:00:00`);
    if (this.period() === 'month') {
      const day = date.getDate();
      date.setDate(1);
      date.setMonth(date.getMonth() + direction);
      const lastDay = new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate();
      date.setDate(Math.min(day, lastDay));
    } else date.setDate(date.getDate() + direction);
    this.changeDate(this.sessionDate(date.toISOString()));
  }
  startSession() {
    if (this.sessionBusy() || this.currentSession()) return;
    const scope = this.scopeKey();
    this.starting.set(true); this.sessionError.set('');
    this.api.startSession(this.subjectId(), this.projectId() ?? undefined, this.taskId() ?? undefined).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: session => {
        this.starting.set(false);
        if (this.scopeKey() !== scope) return;
        this.onSessionUpdated(session); this.changeDate(this.sessionDate(session.startTime)); this.message.emit('Сессия начата.');
      },
      error: error => {
        this.starting.set(false);
        if (this.scopeKey() !== scope) return;
        this.sessionError.set(errorMessage(error)); this.refreshSessions();
      }
    });
  }
  closeSession(session: Session) {
    if (this.sessionBusy()) return;
    const scope = this.scopeKey();
    this.closingId.set(session.id); this.sessionError.set('');
    this.api.closeSession(session.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: updated => {
        this.closingId.set(null);
        if (this.scopeKey() !== scope) return;
        this.onSessionUpdated(updated); this.message.emit('Сессия завершена.');
      },
      error: error => {
        this.closingId.set(null);
        if (this.scopeKey() !== scope) return;
        this.sessionError.set(errorMessage(error)); this.refreshSessions();
      }
    });
  }
  deleteSession(session: Session) {
    if (this.sessionBusy() || this.confirmDeleteId() !== session.id) return;
    const scope = this.scopeKey();
    this.deletingId.set(session.id); this.sessionError.set('');
    this.api.deleteSession(session.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.deletingId.set(null);
        if (this.scopeKey() !== scope) return;
        this.sessions.update(sessions => sessions.filter(item => item.id !== session.id));
        this.confirmDeleteId.set(null); this.message.emit('Сессия удалена.');
      },
      error: error => {
        this.deletingId.set(null);
        if (this.scopeKey() !== scope) return;
        this.sessionError.set(errorMessage(error)); this.refreshSessions();
      }
    });
  }
  onSessionUpdated(session: Session) {
    this.sessions.update(sessions => [...sessions.filter(item => item.id !== session.id), session]
      .sort((a, b) => Date.parse(a.startTime) - Date.parse(b.startTime) || a.id - b.id));
    this.editingSession.set(null); this.confirmDeleteId.set(null); this.sessionError.set(''); this.message.emit('Сессия сохранена.');
  }
  private refreshSessions() {
    const scope = this.scopeKey();
    const projectId = this.projectId();
    const taskId = this.taskId();
    const sessions = projectId !== null && taskId !== null ? this.api.taskSessions(projectId, taskId)
      : projectId !== null ? this.api.projectSessions(projectId) : this.api.subjectSessions(this.subjectId());
    forkJoin({ sessions, current: this.api.currentSession() })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: result => { if (this.scopeKey() === scope) this.sessions.set(result.sessions); },
        error: error => { if (this.scopeKey() === scope) this.sessionError.set(errorMessage(error)); }
      });
  }
}
