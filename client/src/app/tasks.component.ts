import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject as Stream, of, startWith, switchMap } from 'rxjs';
import { ApiService, Project, ProjectTask, ProjectTaskStatus, taskStatuses, Subject } from './api.service';
import { BreadcrumbsComponent } from './breadcrumbs.component';
import { TaskCardsComponent } from './task-cards.component';
import { TaskCreateComponent } from './task-create.component';
import { loadProjectTasksPage, PageResult, ProjectTasksPage } from './route-pages';
import { readStatusFilter, writeStatusFilter } from './status-filter-storage';

@Component({
  imports: [FormsModule, BreadcrumbsComponent, TaskCardsComponent, TaskCreateComponent],
  styles: `
    .task-filters { display: flex; flex-wrap: wrap; align-items: end; gap: 16px; padding: 16px; margin-bottom: 24px; background: #e9eddf; border: 1px solid #dbe1ce; border-radius: 12px; }
    .task-filters > div { flex: 1 1 200px; }
    .task-filters label { margin-top: 0; }
    .task-filters select { margin-bottom: 0; }
  `,
  template: `
    @if (subject() && project()) {
      <app-breadcrumbs [items]="[{ label: 'Предметы', link: ['/subjects'] }, { label: subject()!.name, link: ['/subjects', subject()!.id] }, { label: project()!.name, link: ['/projects', project()!.id] }, { label: 'Задачи' }]" />
    } @else { <app-breadcrumbs [items]="[{ label: 'Предметы', link: ['/subjects'] }, { label: 'Задачи' }]" /> }
    <div class="page-heading">
      <h1>Задачи проекта</h1>
      <button type="button" (click)="createOpen.set(true)" [disabled]="loading() || !subject() || !!error()">+ Новая задача</button>
    </div>
    @if (error()) { <p class="error" role="alert">{{ error() }} <button class="text-button" type="button" (click)="reload()">Повторить</button></p> }
    @if (loading()) { <p role="status">Загружаем задачи…</p> }
    @else if (subject() && !error()) {
      <section class="task-filters" aria-label="Фильтры задач">
        <div>
          <label for="task-month-filter">Месяц создания</label>
          <select id="task-month-filter" [ngModel]="month()" (ngModelChange)="month.set($event)">
            <option value="all">Все месяцы</option>
            @for (item of months(); track item.value) { <option [value]="item.value">{{ item.label }}</option> }
            @if (hasUndatedTasks()) { <option value="unknown">Без даты создания</option> }
          </select>
        </div>
        <div>
          <label for="task-status-filter">Статус</label>
          <select id="task-status-filter" [ngModel]="status()" (ngModelChange)="changeStatus($event)">
            <option [ngValue]="'all'">Все статусы</option>
            @for (item of statuses; track item.value) { <option [ngValue]="item.value">{{ item.label }}</option> }
          </select>
        </div>
        <button class="secondary" type="button" (click)="resetFilters()">Сбросить</button>
      </section>
      <section aria-label="Все задачи проекта">
        <div class="section-heading"><h2>Задачи</h2><span class="count">{{ filteredTasks().length }} из {{ tasks().length }}</span></div>
        <app-task-cards [tasks]="filteredTasks()" />
        @if (!filteredTasks().length) { <p class="muted">{{ tasks().length ? 'Задач с выбранными фильтрами нет.' : 'В проекте пока нет задач.' }}</p> }
      </section>
      @if (createOpen()) { <app-task-create [project]="project()!" (created)="onCreated($event)" (cancelled)="createOpen.set(false)" /> }
    }
    @if (message()) { <p class="toast" role="status">{{ message() }}</p> }
  `
})
export class TasksComponent {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly refresh = new Stream<void>();
  readonly subject = signal<Subject | null>(null);
  readonly project = signal<Project | null>(null);
  readonly tasks = signal<ProjectTask[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly message = signal('');
  readonly createOpen = signal(false);
  readonly month = signal('all');
  readonly status = signal<ProjectTaskStatus | 'all'>(0);
  readonly statuses = taskStatuses;
  readonly hasUndatedTasks = computed(() => this.tasks().some(task => !task.createdAt));
  readonly months = computed(() => [...new Set(this.tasks().map(task => this.taskMonth(task)))]
    .filter(month => month !== 'unknown').sort().reverse().map(value => ({ value,
      label: new Date(`${value}-15T12:00:00`).toLocaleDateString('ru-RU', { month: 'long', year: 'numeric' }) })));
  readonly filteredTasks = computed(() => this.tasks()
    .filter(task => (this.status() === 'all' || task.status === this.status())
      && (this.month() === 'all' || this.taskMonth(task) === this.month()))
    .sort((a, b) => b.id - a.id));

  constructor() {
    this.route.data.pipe(
      switchMap(data => this.refresh.pipe(startWith(undefined), switchMap((_, index) => {
        this.loading.set(true); this.error.set(''); this.createOpen.set(false);
        if (index === 0) this.status.set(readStatusFilter(this.statusStorageKey()));
        return index === 0 ? of(data['page'] as PageResult<ProjectTasksPage>)
          : loadProjectTasksPage(this.api, Number(this.route.snapshot.paramMap.get('id')));
      }))), takeUntilDestroyed()
    ).subscribe(result => {
      this.subject.set(result.data?.subject ?? null); this.project.set(result.data?.project ?? null); this.tasks.set(result.data?.tasks ?? []);
      this.error.set(result.error); this.loading.set(false);
    });
  }
  private taskMonth(task: ProjectTask) {
    if (!task.createdAt) return 'unknown';
    const date = new Date(task.createdAt);
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
  }
  private statusStorageKey() { return `daily-notes:projects:${this.route.snapshot.paramMap.get('id')}:tasks:status`; }
  changeStatus(status: ProjectTaskStatus | 'all') {
    this.status.set(status);
    writeStatusFilter(this.statusStorageKey(), status);
  }
  resetFilters() { this.month.set('all'); this.changeStatus(0); }
  reload() { this.refresh.next(); }
  onCreated(task: ProjectTask) {
    this.tasks.update(tasks => [...tasks, task]); this.createOpen.set(false);
    this.month.set('all'); this.message.set('Задача создана.');
  }
}
