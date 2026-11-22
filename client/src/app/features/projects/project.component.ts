import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject as Stream, of, startWith, switchMap } from 'rxjs';
import { Project, projectStatuses, ProjectsApi } from './projects-api';
import { Subject, SubjectsApi } from '../subjects/subjects-api';
import { TaskCardsComponent } from '../tasks/task-cards.component';
import { ProjectSettingsComponent } from './project-settings.component';
import { BreadcrumbsComponent } from '../../shared/breadcrumbs.component';
import { loadProjectSessionsPage, type ProjectSessionsPage } from './project-pages';
import type { PageResult } from '../../shared/page-result';
import { SessionState } from '../sessions/session-state';
import { Session, SessionsApi } from '../sessions/sessions-api';
import { SessionsPanelComponent } from '../sessions/sessions-panel.component';
import { ProjectDeleteComponent } from './project-delete.component';

@Component({
  imports: [TaskCardsComponent, ProjectSettingsComponent, BreadcrumbsComponent, SessionsPanelComponent, RouterLink, ProjectDeleteComponent],
  styles: `
    .project-tasks { margin-top: 32px; }
    .project-description { margin-bottom: 28px; }
    .project-heading { padding: 16px 24px; background: #e9eddf; border: 1px solid #bdcbb6; border-radius: 14px; flex-direction: row; flex-wrap: wrap; align-items: center; gap: 16px 24px; }
    .project-title { min-width: 0; flex: 1 1 240px; }
    .project-kind { display: flex; flex-wrap: wrap; align-items: baseline; gap: 12px; margin: 0 0 6px; font-size: 14px; line-height: 1.5; color: #66766e; }
    .project-kind .project-code { margin: 0; font: inherit; }
    .project-title h1 { margin: 0; font-size: clamp(30px, 4vw, 46px); }
    .project-meta { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 16px; margin-top: 10px; }
    .project-status { display: inline-block; }
    .project-controls { display: flex; flex-direction: column; align-items: flex-end; gap: 10px; min-width: 0; }
    .project-work-time { display: flex; align-items: center; gap: 8px; margin: 0; color: #496642; }
    .project-work-time svg { width: 20px; height: 20px; flex-shrink: 0; }
    .project-work-time strong { color: #214f46; font-size: 20px; font-variant-numeric: tabular-nums; }
    .project-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; align-items: center; gap: 8px; }
    .project-action { width: 42px; height: 42px; padding: 10px; }
    .project-action svg { width: 20px; height: 20px; }
    .delete-action { color: #a43e30; margin-left: 8px; }
    .delete-action:hover { background: #fbe9e5; }
    @media (max-width: 680px) { .project-heading { padding: 14px 18px; gap: 14px; } }
  `,
  template: `
    @if (subject() && project()) {
      <app-breadcrumbs [items]="[{ label: 'Предметы', link: ['/subjects'] }, { label: subject()!.name, link: ['/subjects', subject()!.id] }, { label: project()!.name }]" />
    } @else { <app-breadcrumbs [items]="[{ label: 'Предметы', link: ['/subjects'] }, { label: 'Проект' }]" /> }
    @if (loading()) { <p role="status">Загружаем проект…</p> }
    @else if (error()) { <p class="error" role="alert">{{ error() }} <button class="text-button" (click)="reload()">Повторить</button></p> }
    @else if (project(); as project) {
      <div class="page-heading project-heading">
        <div class="project-title">
          <p class="project-kind">Проект <span class="project-code">{{ project.fullCode }}</span></p>
          <h1>{{ project.name }}</h1>
          <div class="project-meta">
            <span class="tag project-status">{{ statusLabel(project) }}</span>
          </div>
        </div>
        <div class="project-controls">
        <aside class="project-actions" aria-label="Действия с проектом">
          <button class="project-action" type="button" (click)="sessionsPanel()?.startSession()" [disabled]="!sessionsPanel() || sessionsPanel()!.sessionBusy() || !!currentSession()" [attr.aria-label]="sessionsPanel()?.starting() ? 'Начинаем сессию…' : 'Начать сессию'" title="Начать сессию" [attr.aria-busy]="!!sessionsPanel()?.starting()">
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M8 4.5a1 1 0 0 1 1.5-.86l11 6.5a1 1 0 0 1 0 1.72l-11 6.5A1 1 0 0 1 8 17.5Z" /></svg>
          </button>
          <a class="button secondary project-action" [routerLink]="['/projects', project.id, 'tasks']" [attr.aria-label]="'Задачи проекта, ' + project.tasks.length" [title]="'Задачи (' + project.tasks.length + ')'">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="4" y="3" width="16" height="18" rx="2" /><path d="m8 12 3 3 5-6" /></svg>
          </a>
          <a class="button secondary project-action" [routerLink]="['/projects', project.id, 'notes']" aria-label="Заметки проекта" title="Заметки проекта">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M14 3H5v18h14V8ZM14 3v5h5M8 12h8M8 16h6" /></svg>
          </a>
          <button class="secondary project-action" type="button" (click)="settingsOpen.set(true)" aria-label="Настройка проекта" title="Настройка проекта">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m9 3-.6 3-2.5 1.5L3 7l-1 3 2.3 2L4 15l2 2 2.8-.5 2.2 2 3-.5 1-2.5 2.8-1 1-3-2-2 .2-3-3-1L12 3Z" /><circle cx="10.5" cy="10.5" r="3" /></svg>
          </button>
          <button class="secondary project-action delete-action" type="button" (click)="deleteOpen.set(true)" aria-label="Удалить проект" title="Удалить проект">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 6h18M9 6V4h6v2M5 6l1 15h12l1-15M10 10v7M14 10v7" /></svg>
          </button>
        </aside>
        <p class="project-work-time" title="Суммарное время всех завершённых сессий проекта и его задач" [attr.aria-label]="'Общее время работы: ' + (sessionsPanel()?.totalWorkTime() ?? '0 ч 00 мин')">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M19 4H5l8 8-8 8h14" /></svg>
          <strong>{{ sessionsPanel()?.totalWorkTime() ?? '0 ч 00 мин' }}</strong>
        </p>
        </div>
      </div>
      @if (settingsOpen()) { <app-project-settings [project]="project" (saved)="onProjectUpdated($event)" (cancelled)="settingsOpen.set(false)" /> }
      @if (deleteOpen()) { <app-project-delete [project]="project" (deleted)="onDeleted()" (cancelled)="deleteOpen.set(false)" /> }
      @if (project.description) {
        <section class="panel project-description" aria-label="Описание проекта">
          <h2>Описание проекта</h2><p class="task-body">{{ project.description }}</p>
        </section>
      }
      <app-sessions-panel [subject]="subject()!" [projects]="[project]" #sessionsPanelView [subjectId]="project.subjectId" [projectId]="project.id" [initialSessions]="sessions()" [showTimeSummary]="true" (message)="message.set($event)" />
      <section class="project-tasks" aria-label="Последние задачи в работе">
        <div class="section-heading"><h2>Задачи в работе</h2><a class="text-button" [routerLink]="['/projects', project.id, 'tasks']">Все задачи →</a></div>
        <app-task-cards [tasks]="recentTasks()" [showPin]="true" />
        @if (!recentTasks().length) { <p class="muted">Задач в работе пока нет.</p> }
      </section>
    }
    @if (message()) { <p class="toast" role="status">{{ message() }}</p> }
  `
})
export class ProjectComponent {
  private readonly projectsApi = inject(ProjectsApi);
  private readonly subjectsApi = inject(SubjectsApi);
  private readonly sessionsApi = inject(SessionsApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly refresh = new Stream<void>();
  readonly project = signal<Project | null>(null);
  readonly recentTasks = computed(() => (this.project()?.tasks ?? []).filter(task => task.status === 0).sort((a, b) => b.id - a.id).slice(0, 10));
  readonly sessions = signal<Session[]>([]);
  readonly currentSession = inject(SessionState).activeSession;
  readonly sessionsPanel = viewChild<SessionsPanelComponent>('sessionsPanelView');
  readonly subject = signal<Subject | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly message = signal('');
  readonly settingsOpen = signal(false);
  readonly deleteOpen = signal(false);

  constructor() {
    this.route.data.pipe(
      switchMap(routeData => this.refresh.pipe(startWith(undefined), switchMap((_, index) => {
        this.loading.set(true); this.error.set(''); this.message.set('');
        this.settingsOpen.set(false); this.deleteOpen.set(false);
        return index === 0 ? of(routeData['page'] as PageResult<ProjectSessionsPage>)
          : loadProjectSessionsPage(this.projectsApi, this.subjectsApi, this.sessionsApi, Number(this.route.snapshot.paramMap.get('id')));
      }))), takeUntilDestroyed()
    ).subscribe(result => {
      this.error.set(result.error);
      this.project.set(result.data?.project ?? null); this.subject.set(result.data?.subject ?? null);
      this.sessions.set(result.data?.sessions ?? []);
      this.loading.set(false);
    });
  }

  reload() { this.refresh.next(); }
  onDeleted() { this.deleteOpen.set(false); void this.router.navigate(['/subjects', this.project()!.subjectId]); }
  onProjectUpdated(project: Project) {
    this.project.set(project); this.settingsOpen.set(false); this.message.set('Настройки проекта сохранены.');
  }
  statusLabel(project: Project) { return projectStatuses.find(status => status.value === project.status)!.label; }
}
