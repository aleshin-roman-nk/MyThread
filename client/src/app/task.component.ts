import { Component, inject, signal, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject as Stream, of, startWith, switchMap } from 'rxjs';
import { ApiService, Project, ProjectTask, Session, Subject, TaskNote, taskStatuses } from './api.service';
import { BreadcrumbsComponent } from './breadcrumbs.component';
import { TaskEditComponent } from './task-edit.component';
import { loadTaskPage, PageResult, TaskPage } from './route-pages';
import { SessionsPanelComponent } from './sessions-panel.component';
import { TaskDeleteComponent } from './task-delete.component';
import { NoteContentComponent } from './note-content.component';
import { DesktopPinComponent } from './desktop-pin.component';
import { formatNoteTime } from './time-format';

@Component({
  imports: [BreadcrumbsComponent, TaskEditComponent, SessionsPanelComponent, TaskDeleteComponent, RouterLink, DatePipe, NoteContentComponent, DesktopPinComponent],
  styles: `
    .task-description { margin-bottom: 28px; }
    .task-notes { margin-top: 34px; }
    .task-notes app-note-content { margin: 10px 0 4px; }
    .task-heading { padding: 24px; background: #e9eddf; border: 1px solid #bdcbb6; border-radius: 14px; flex-direction: row; flex-wrap: wrap; }
    .task-title { min-width: 0; flex: 1 1 240px; }
    .task-kind { display: flex; flex-wrap: wrap; align-items: baseline; gap: 12px; margin: 0 0 6px; font-size: 14px; line-height: 1.5; color: #66766e; }
    .task-kind .task-code { margin: 0; font: inherit; }
    .task-title h1 { margin: 0; font-size: clamp(30px, 4vw, 46px); }
    .task-status { display: inline-block; margin-top: 12px; }
    .task-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; flex-shrink: 0; }
    .task-action { width: 42px; height: 42px; padding: 10px; }
    .task-action svg { width: 20px; height: 20px; }
    .delete-action { color: #a43e30; margin-left: 8px; }
    .delete-action:hover { background: #fbe9e5; }
    @media (max-width: 680px) { .task-heading { padding: 18px; gap: 18px; } }
  `,
  template: `
    @if (subject() && project() && task()) {
      <app-breadcrumbs [items]="[{ label: 'Предметы', link: ['/subjects'] }, { label: subject()!.name, link: ['/subjects', subject()!.id] }, { label: project()!.name, link: ['/projects', project()!.id] }, { label: task()!.name }]" />
    } @else { <app-breadcrumbs [items]="[{ label: 'Предметы', link: ['/subjects'] }, { label: 'Задача' }]" /> }
    @if (loading()) { <p role="status">Загружаем задачу…</p> }
    @else if (error()) { <p class="error" role="alert">{{ error() }} <button class="text-button" (click)="reload()">Повторить</button></p> }
    @else if (task(); as task) {
      <div class="page-heading task-heading">
        <div class="task-title">
          <p class="task-kind">Задача <span class="task-code">{{ task.fullCode }}</span></p>
          <h1>{{ task.name }}</h1><span class="tag task-status">{{ statusLabel(task) }}</span>
        </div>
        <aside class="task-actions" aria-label="Действия с задачей">
          <button class="task-action" type="button" (click)="sessionsPanel()?.startSession()" [disabled]="!sessionsPanel() || sessionsPanel()!.sessionBusy() || !!currentSession()" [attr.aria-label]="sessionsPanel()?.starting() ? 'Начинаем сессию…' : 'Начать сессию'" title="Начать сессию" [attr.aria-busy]="!!sessionsPanel()?.starting()">
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M8 4.5a1 1 0 0 1 1.5-.86l11 6.5a1 1 0 0 1 0 1.72l-11 6.5A1 1 0 0 1 8 17.5Z" /></svg>
          </button>
          <a class="button secondary task-action" [routerLink]="['/projects', task.projectId, 'tasks', task.id, 'notes']" aria-label="Заметки задачи" title="Заметки задачи">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M14 3H5v18h14V8ZM14 3v5h5M8 12h8M8 16h6" /></svg>
          </a>
          <app-desktop-pin [targetId]="task.id" (changed)="onPinChanged($event)" (errorChanged)="pinError.set($event)" />
          <button class="secondary task-action" type="button" (click)="editing.set(true)" aria-label="Изменить задачу" title="Изменить задачу">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m9 3-.6 3-2.5 1.5L3 7l-1 3 2.3 2L4 15l2 2 2.8-.5 2.2 2 3-.5 1-2.5 2.8-1 1-3-2-2 .2-3-3-1L12 3Z" /><circle cx="10.5" cy="10.5" r="3" /></svg>
          </button>
          <button class="secondary task-action delete-action" type="button" (click)="deleteOpen.set(true)" aria-label="Удалить задачу" title="Удалить задачу">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 6h18M9 6V4h6v2M5 6l1 15h12l1-15M10 10v7M14 10v7" /></svg>
          </button>
        </aside>
      </div>
      <section class="panel task-description" aria-label="Описание задачи"><h2>Описание задачи</h2><p class="task-body">{{ task.body }}</p></section>
      @if (pinError()) { <p class="error" role="alert">{{ pinError() }}</p> }
      <app-sessions-panel [subject]="subject()!" [projects]="[project()!]" #sessionsPanelView [subjectId]="project()!.subjectId" [projectId]="task.projectId" [taskId]="task.id" [initialSessions]="sessions()" (message)="message.set($event)" />
      @let noteList = notes();
      <section class="task-notes" aria-labelledby="task-notes-heading">
        <div class="section-heading">
          <h2 id="task-notes-heading">Заметки</h2>
          @if (noteList?.data; as entries) { <span class="count">{{ entries.length }}</span> }
        </div>
        @if (noteList?.error; as error) {
          <p class="error" role="alert">{{ error }} <button class="text-button" type="button" (click)="reload()">Повторить</button></p>
        } @else {
          <div class="note-list">
            @for (note of noteList?.data ?? []; track note.id) {
              <article class="note-card">
                <div class="note-meta">
                  <time [attr.datetime]="note.date + 'T' + note.time">{{ note.date | date:'dd.MM.yyyy' }} · {{ formatNoteTime(note.time) }}</time>
                  <span>Оценка {{ note.rating }}/5</span>
                  <app-desktop-pin kind="note" [targetId]="note.id" [inlineError]="true" />
                </div>
                <app-note-content [text]="note.text" />
              </article>
            } @empty { <div class="empty-state"><p>У задачи пока нет заметок.</p></div> }
          </div>
        }
      </section>
      @if (deleteOpen()) { <app-task-delete [task]="task" (deleted)="onDeleted()" (cancelled)="deleteOpen.set(false)" /> }
      @if (editing()) { <app-task-edit [task]="task" (saved)="onSaved($event)" (cancelled)="editing.set(false)" /> }
    }
    @if (message()) { <p class="toast" role="status">{{ message() }}</p> }
  `
})
export class TaskComponent {
  readonly formatNoteTime = formatNoteTime;
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly refresh = new Stream<void>();
  readonly subject = signal<Subject | null>(null);
  readonly project = signal<Project | null>(null);
  readonly task = signal<ProjectTask | null>(null);
  readonly sessions = signal<Session[]>([]);
  readonly notes = signal<PageResult<TaskNote[]> | null>(null);
  readonly currentSession = this.api.activeSession;
  readonly sessionsPanel = viewChild<SessionsPanelComponent>('sessionsPanelView');
  readonly loading = signal(true);
  readonly error = signal('');
  readonly editing = signal(false);
  readonly deleteOpen = signal(false);
  readonly message = signal('');
  readonly pinError = signal('');

  constructor() {
    this.route.data.pipe(
      switchMap(routeData => this.refresh.pipe(startWith(undefined), switchMap((_, index) => {
        this.loading.set(true); this.error.set(''); this.message.set(''); this.pinError.set(''); this.editing.set(false); this.deleteOpen.set(false);
        return index === 0 ? of(routeData['page'] as PageResult<TaskPage>)
          : loadTaskPage(this.api, Number(this.route.snapshot.paramMap.get('projectId')), Number(this.route.snapshot.paramMap.get('taskId')));
      }))), takeUntilDestroyed()
    ).subscribe(result => {
      this.error.set(result.error);
      this.subject.set(result.data?.subject ?? null); this.project.set(result.data?.project ?? null); this.task.set(result.data?.task ?? null);
      this.sessions.set(result.data?.sessions ?? []);
      this.notes.set(result.data?.notes ?? null);
      this.loading.set(false);
    });
  }

  reload() { this.refresh.next(); }
  onPinChanged(pinned: boolean) {
    this.message.set(pinned ? 'Задача закреплена на рабочем столе.' : 'Задача откреплена от рабочего стола.');
  }
  onDeleted() { this.deleteOpen.set(false); void this.router.navigate(['/projects', this.task()!.projectId]); }
  statusLabel(task: ProjectTask) { return taskStatuses.find(status => status.value === task.status)!.label; }
  onSaved(task: ProjectTask) { this.task.set(task); this.editing.set(false); this.message.set('Задача сохранена.'); }
}
