import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject as Stream, of, startWith, switchMap } from 'rxjs';
import { ApiService, Project, Session, Subject } from './api.service';
import { ProjectCardsComponent } from './project-cards.component';
import { SubjectSettingsComponent } from './subject-settings.component';
import { BreadcrumbsComponent } from './breadcrumbs.component';
import { loadSubjectPage, PageResult, SubjectPage } from './route-pages';
import { SessionsPanelComponent } from './sessions-panel.component';
import { SubjectDeleteComponent } from './subject-delete.component';
import { NoteContentComponent } from './note-content.component';

@Component({
  imports: [RouterLink, BreadcrumbsComponent, ProjectCardsComponent, SubjectSettingsComponent, SubjectDeleteComponent, SessionsPanelComponent, NoteContentComponent],
  styles: `
    .subject-heading { padding: 16px 24px; background: #e9eddf; border: 1px solid #bdcbb6; border-radius: 14px; flex-direction: row; flex-wrap: wrap; align-items: center; gap: 16px 24px; }
    .subject-title { min-width: 0; flex: 1 1 240px; }
    .subject-kind { margin: 0 0 6px; font-size: 14px; line-height: 1.5; color: #66766e; }
    .subject-title h1 { margin: 0; font-size: clamp(30px, 4vw, 46px); }
    .subject-controls { display: flex; flex-direction: column; align-items: flex-end; gap: 10px; min-width: 0; }
    .subject-work-time { display: flex; align-items: center; gap: 8px; margin: 0; color: #496642; }
    .subject-work-time svg { width: 20px; height: 20px; flex-shrink: 0; }
    .subject-work-time strong { color: #214f46; font-size: 20px; font-variant-numeric: tabular-nums; }
    .subject-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; align-items: center; gap: 8px; }
    .subject-action { width: 42px; height: 42px; padding: 10px; }
    .subject-action svg { width: 20px; height: 20px; }
    .delete-action { color: #a43e30; margin-left: 8px; }
    .delete-action:hover { background: #fbe9e5; }
    .subject-projects { margin-top: 32px; }
    .subject-description { margin-bottom: 28px; }
    @media (max-width: 680px) { .subject-heading { padding: 14px 18px; gap: 14px; } }
  `,
  template: `
    <app-breadcrumbs [items]="[{ label: 'Предметы', link: ['/subjects'] }, { label: subject()?.name ?? 'Предмет' }]" />
    <div class="page-heading subject-heading">
      <div class="subject-title">
        <p class="subject-kind">Предмет</p><h1>{{ subject()?.name ?? 'Предмет' }}</h1>
      </div>
      <div class="subject-controls">
      <aside class="subject-actions" aria-label="Действия с предметом">
        @if (subject(); as subject) {
          <button class="subject-action" type="button" (click)="sessionsPanel()?.startSession()" [disabled]="loading() || !sessionsPanel() || sessionsPanel()!.sessionBusy() || !!currentSession()" [attr.aria-label]="sessionsPanel()?.starting() ? 'Начинаем сессию…' : 'Начать сессию'" title="Начать сессию" [attr.aria-busy]="!!sessionsPanel()?.starting()">
            <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M8 4.5a1 1 0 0 1 1.5-.86l11 6.5a1 1 0 0 1 0 1.72l-11 6.5A1 1 0 0 1 8 17.5Z" /></svg>
          </button>
          <a class="button secondary subject-action" [routerLink]="['/subjects', subject.id, 'projects']" aria-label="Проекты" title="Проекты">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 7V5h6l2 2h10v12H3Z" /></svg>
          </a>
          <a class="button secondary subject-action" [routerLink]="['/subjects', subject.id, 'notes']" [attr.aria-label]="'Заметки предмета, ' + subject.noteCount" [title]="'Заметки предмета (' + subject.noteCount + ')'">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M14 3H5v18h14V8ZM14 3v5h5M8 12h8M8 16h6" /></svg>
          </a>
        }
        <button class="secondary subject-action" type="button" (click)="settingsOpen.set(true)" [disabled]="loading() || !subject() || !!error()" aria-label="Настройки предмета" title="Настройки предмета">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m9 3-.6 3-2.5 1.5L3 7l-1 3 2.3 2L4 15l2 2 2.8-.5 2.2 2 3-.5 1-2.5 2.8-1 1-3-2-2 .2-3-3-1L12 3Z" /><circle cx="10.5" cy="10.5" r="3" /></svg>
        </button>
        <button class="secondary subject-action delete-action" type="button" (click)="deleteOpen.set(true)" [disabled]="loading() || !subject() || !!error()" aria-label="Удалить предмет" title="Удалить предмет">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 6h18M9 6V4h6v2M5 6l1 15h12l1-15M10 10v7M14 10v7" /></svg>
        </button>
      </aside>
      @if (!loading() && !error() && subject()) {
        <p class="subject-work-time" title="Суммарное время всех завершённых сессий предмета, его проектов и задач" [attr.aria-label]="'Общее время работы: ' + (sessionsPanel()?.totalWorkTime() ?? '0 ч 00 мин')">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M19 4H5l8 8-8 8h14" /></svg>
          <strong>{{ sessionsPanel()?.totalWorkTime() ?? '0 ч 00 мин' }}</strong>
        </p>
      }
      </div>
    </div>
    @if (error()) { <p class="error" role="alert">{{ error() }} <button class="text-button" (click)="reload()">Повторить</button></p> }
    @if (loading()) { <p role="status">Загружаем предмет…</p> }
    @else if (subject() && !error()) {
      @if (settingsOpen()) {
        <app-subject-settings [subject]="subject()!" (saved)="onSettingsSaved()" (cancelled)="settingsOpen.set(false)" />
      }
      @if (deleteOpen()) {
        <app-subject-delete [subject]="subject()!" (deleted)="onDeleted()" (cancelled)="deleteOpen.set(false)" />
      }
      @if (subject()!.description) {
        <section class="panel subject-description" aria-label="Описание предмета">
          <h2>Описание предмета</h2>
          <app-note-content [text]="subject()!.description" />
        </section>
      }
      <app-sessions-panel [subject]="subject()!" [projects]="projects()" #sessionsPanelView [subjectId]="subject()!.id" [initialSessions]="sessions()" [showTimeSummary]="true" (message)="message.set($event)" />
      <section class="subject-projects" aria-label="Последние проекты в работе">
        <div class="section-heading"><h2>Проекты в работе</h2><a class="text-button" [routerLink]="['/subjects', subject()!.id, 'projects']">Все проекты →</a></div>
        <app-project-cards [projects]="recentProjects()" />
        @if (!recentProjects().length) { <p class="muted">Проектов в работе пока нет.</p> }
      </section>
    }
    @if (message()) { <p class="toast" role="status">{{ message() }}</p> }
  `
})
export class SubjectComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly refresh = new Stream<void>();
  readonly subject = signal<Subject | null>(null);
  readonly projects = signal<Project[]>([]);
  readonly recentProjects = computed(() => this.projects().filter(project => project.status === 0).sort((a, b) => b.id - a.id).slice(0, 10));
  readonly sessions = signal<Session[]>([]);
  readonly currentSession = this.api.activeSession;
  readonly sessionsPanel = viewChild<SessionsPanelComponent>('sessionsPanelView');
  readonly settingsOpen = signal(false);
  readonly deleteOpen = signal(false);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly message = signal('');

  constructor() {
    this.route.data.pipe(
      switchMap(routeData => this.refresh.pipe(startWith(undefined), switchMap((_, index) => {
        this.loading.set(true); this.error.set(''); this.settingsOpen.set(false); this.deleteOpen.set(false);
        return index === 0 ? of(routeData['page'] as PageResult<SubjectPage>)
          : loadSubjectPage(this.api, Number(this.route.snapshot.paramMap.get('id')));
      }))), takeUntilDestroyed()
    ).subscribe(result => {
      this.error.set(result.error);
      this.subject.set(result.data?.subject ?? null); this.projects.set(result.data?.projects ?? []);
      this.sessions.set(result.data?.sessions ?? []);
      this.loading.set(false);
    });
  }
  reload() { this.refresh.next(); }
  onDeleted() { this.deleteOpen.set(false); void this.router.navigate(['/subjects']); }
  onSettingsSaved() { this.settingsOpen.set(false); this.message.set('Настройки предмета сохранены.'); this.reload(); }
}
