import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject as Stream, of, startWith, switchMap } from 'rxjs';
import { Project, ProjectStatus, projectStatuses, ProjectsApi } from './projects-api';
import { Subject, SubjectsApi } from '../subjects/subjects-api';
import { BreadcrumbsComponent } from '../../shared/breadcrumbs.component';
import { ProjectCardsComponent } from './project-cards.component';
import { ProjectCreateComponent } from './project-create.component';
import { loadSubjectProjectsPage, type SubjectProjectsPage } from './project-pages';
import type { PageResult } from '../../shared/page-result';
import { readStatusFilter, writeStatusFilter } from '../../shared/status-filter-storage';

@Component({
  imports: [FormsModule, BreadcrumbsComponent, ProjectCardsComponent, ProjectCreateComponent],
  styles: `
    .project-filters { display: flex; flex-wrap: wrap; align-items: end; gap: 16px; padding: 16px; margin-bottom: 24px; background: #e9eddf; border: 1px solid #dbe1ce; border-radius: 12px; }
    .project-filters > div { flex: 1 1 200px; }
    .project-filters label { margin-top: 0; }
    .project-filters select { margin-bottom: 0; }
  `,
  template: `
    @if (subject(); as subject) {
      <app-breadcrumbs [items]="[{ label: 'Предметы', link: ['/subjects'] }, { label: subject.name, link: ['/subjects', subject.id] }, { label: 'Проекты' }]" />
    } @else { <app-breadcrumbs [items]="[{ label: 'Предметы', link: ['/subjects'] }, { label: 'Проекты' }]" /> }
    <div class="page-heading">
      <h1>Проекты предмета</h1>
      <button type="button" (click)="createOpen.set(true)" [disabled]="loading() || !subject() || !!error()">+ Новый проект</button>
    </div>
    @if (error()) { <p class="error" role="alert">{{ error() }} <button class="text-button" type="button" (click)="reload()">Повторить</button></p> }
    @if (loading()) { <p role="status">Загружаем проекты…</p> }
    @else if (subject() && !error()) {
      <section class="project-filters" aria-label="Фильтры проектов">
        <div>
          <label for="project-month-filter">Месяц создания</label>
          <select id="project-month-filter" [ngModel]="month()" (ngModelChange)="month.set($event)">
            <option value="all">Все месяцы</option>
            @for (item of months(); track item.value) { <option [value]="item.value">{{ item.label }}</option> }
            @if (hasUndatedProjects()) { <option value="unknown">Без даты создания</option> }
          </select>
        </div>
        <div>
          <label for="project-status-filter">Статус</label>
          <select id="project-status-filter" [ngModel]="status()" (ngModelChange)="changeStatus($event)">
            <option [ngValue]="'all'">Все статусы</option>
            @for (item of statuses; track item.value) { <option [ngValue]="item.value">{{ item.label }}</option> }
          </select>
        </div>
        <button class="secondary" type="button" (click)="resetFilters()">Сбросить</button>
      </section>
      <section aria-label="Все проекты предмета">
        <div class="section-heading"><h2>Проекты</h2><span class="count">{{ filteredProjects().length }} из {{ projects().length }}</span></div>
        <app-project-cards [projects]="filteredProjects()" />
        @if (!filteredProjects().length) { <p class="muted">{{ projects().length ? 'Проектов с выбранными фильтрами нет.' : 'В предмете пока нет проектов.' }}</p> }
      </section>
      @if (createOpen()) { <app-project-create [subject]="subject()!" (created)="onCreated($event)" (cancelled)="createOpen.set(false)" /> }
    }
    @if (message()) { <p class="toast" role="status">{{ message() }}</p> }
  `
})
export class ProjectsComponent {
  private readonly projectsApi = inject(ProjectsApi);
  private readonly subjectsApi = inject(SubjectsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly refresh = new Stream<void>();
  readonly subject = signal<Subject | null>(null);
  readonly projects = signal<Project[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly message = signal('');
  readonly createOpen = signal(false);
  readonly month = signal('all');
  readonly status = signal<ProjectStatus | 'all'>(0);
  readonly statuses = projectStatuses;
  readonly hasUndatedProjects = computed(() => this.projects().some(project => !project.createdAt));
  readonly months = computed(() => [...new Set(this.projects().map(project => this.projectMonth(project)))]
    .filter(month => month !== 'unknown').sort().reverse().map(value => ({ value,
      label: new Date(`${value}-15T12:00:00`).toLocaleDateString('ru-RU', { month: 'long', year: 'numeric' }) })));
  readonly filteredProjects = computed(() => this.projects()
    .filter(project => (this.status() === 'all' || project.status === this.status())
      && (this.month() === 'all' || this.projectMonth(project) === this.month()))
    .sort((a, b) => b.id - a.id));

  constructor() {
    this.route.data.pipe(
      switchMap(data => this.refresh.pipe(startWith(undefined), switchMap((_, index) => {
        this.loading.set(true); this.error.set(''); this.createOpen.set(false);
        if (index === 0) this.status.set(readStatusFilter(this.statusStorageKey()));
        return index === 0 ? of(data['page'] as PageResult<SubjectProjectsPage>)
          : loadSubjectProjectsPage(this.projectsApi, this.subjectsApi, Number(this.route.snapshot.paramMap.get('id')));
      }))), takeUntilDestroyed()
    ).subscribe(result => {
      this.subject.set(result.data?.subject ?? null); this.projects.set(result.data?.projects ?? []);
      this.error.set(result.error); this.loading.set(false);
    });
  }
  private projectMonth(project: Project) {
    if (!project.createdAt) return 'unknown';
    const date = new Date(project.createdAt);
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
  }
  private statusStorageKey() { return `jchrono:subjects:${this.route.snapshot.paramMap.get('id')}:projects:status`; }
  changeStatus(status: ProjectStatus | 'all') {
    this.status.set(status);
    writeStatusFilter(this.statusStorageKey(), status);
  }
  resetFilters() { this.month.set('all'); this.changeStatus(0); }
  reload() { this.refresh.next(); }
  onCreated(project: Project) {
    this.projects.update(projects => [...projects, project]); this.createOpen.set(false);
    this.month.set('all'); this.message.set('Проект создан.');
  }
}
