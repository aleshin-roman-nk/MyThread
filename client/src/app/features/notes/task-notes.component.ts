import { Component, DestroyRef, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject as Stream, of, startWith, switchMap } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import { NotesApi, TaskNote } from './notes-api';
import type { ProjectTask } from '../tasks/tasks-api';
import { Project, ProjectsApi } from '../projects/projects-api';
import { Subject, SubjectsApi } from '../subjects/subjects-api';
import { TaskNoteEditorComponent } from './task-note-editor.component';
import { NoteListComponent } from './note-list.component';
import { BreadcrumbsComponent } from '../../shared/breadcrumbs.component';
import { loadTaskNotesPage, type TaskNotesPage } from './note-pages';
import type { PageResult } from '../../shared/page-result';

@Component({
  imports: [BreadcrumbsComponent, TaskNoteEditorComponent, NoteListComponent],
  template: `
    @if (subject() && project() && task()) {
      <app-breadcrumbs [items]="[{ label: 'Предметы', link: ['/subjects'] }, { label: subject()!.name, link: ['/subjects', subject()!.id] }, { label: project()!.name, link: ['/projects', project()!.id] }, { label: task()!.name, link: ['/projects', project()!.id, 'tasks', task()!.id] }, { label: 'Заметки' }]" />
    } @else { <app-breadcrumbs [items]="[{ label: 'Предметы', link: ['/subjects'] }, { label: 'Заметки' }]" /> }
    <div class="page-heading">
      <div><h1>Заметки задачи</h1><p class="subtitle">Идеи, наброски мыслей и то, к чему хочется вернуться позже.</p></div>
      <button type="button" (click)="openEditor()" [disabled]="loading() || deleting() || !subject()">+ Новая заметка</button>
    </div>
    @if (error()) { <p class="error" role="alert">{{ error() }} <button class="text-button" (click)="reload()">Повторить</button></p> }
    @if (loading()) { <p role="status">Загружаем заметки…</p> }
    @else if (subject() && !error()) {
      <section aria-label="Заметки задачи">
        <div class="section-heading"><h2>Все заметки</h2><span class="count">{{ notes().length }} заметок</span></div>
        <app-note-list [notes]="notes()" [showContext]="false" [compact]="true" [previewLines]="7" [busy]="deleting()" (viewed)="openEditor($event, true)" (edited)="openEditor($event)" (removed)="remove($event)" />
      </section>
      @if (editorOpen()) {
        <app-task-note-editor [task]="task()!"
          [note]="editing()" [readOnly]="viewing()" (saved)="onSaved()" (updated)="onUpdated($event)" (cancelled)="closeEditor()" />
      }
    }
    @if (message()) { <p class="toast" role="status">{{ message() }}</p> }
  `
})
export class TaskNotesComponent {
  private readonly api = inject(NotesApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly subjectsApi = inject(SubjectsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refresh = new Stream<void>();
  readonly subject = signal<Subject | null>(null);
  readonly project = signal<Project | null>(null);
  readonly task = signal<ProjectTask | null>(null);
  readonly notes = signal<TaskNote[]>([]);
  readonly editing = signal<TaskNote | null>(null);
  readonly editorOpen = signal(false);
  readonly viewing = signal(false);
  readonly loading = signal(true);
  readonly deleting = signal(false);
  readonly error = signal('');
  readonly message = signal('');

  constructor() {
    let noteToEdit = Number(this.route.snapshot.queryParamMap.get('editNote'));
    this.route.data.pipe(
      switchMap(routeData => this.refresh.pipe(startWith(undefined), switchMap((_, index) => {
        this.loading.set(true); this.error.set(''); this.closeEditor();
        return index === 0 ? of(routeData['page'] as PageResult<TaskNotesPage>)
          : loadTaskNotesPage(this.api, this.projectsApi, this.subjectsApi, Number(this.route.snapshot.paramMap.get('projectId')), Number(this.route.snapshot.paramMap.get('taskId')));
      }))), takeUntilDestroyed()
    ).subscribe(result => {
      this.error.set(result.error);
      this.subject.set(result.data?.subject ?? null);
      this.project.set(result.data?.project ?? null);
      this.task.set(result.data?.task ?? null);
      this.notes.set(result.data?.notes ?? []);
      this.loading.set(false);
      const note = this.notes().find(item => item.id === noteToEdit);
      if (note) {
        noteToEdit = 0;
        this.openEditor(note);
      }
    });
  }
  reload() { this.refresh.next(); }
  openEditor(note: TaskNote | null = null, readOnly = false) {
    this.editing.set(note); this.viewing.set(readOnly); this.editorOpen.set(true);
  }
  closeEditor() { this.editorOpen.set(false); this.editing.set(null); }
  onSaved() { this.message.set('Заметка сохранена.'); this.reload(); }
  onUpdated(note: TaskNote) {
    this.notes.update(notes => notes.map(item => item.id === note.id ? note : item)
      .filter(item => item.taskId === this.task()?.id)
      .sort((a, b) => b.date.localeCompare(a.date) || b.time.localeCompare(a.time) || b.id - a.id));
    this.message.set('Заметка сохранена.');
  }
  remove(note: TaskNote) {
    this.deleting.set(true);
    this.api.deleteTaskNote(this.project()!.id, note.taskId, note.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.deleting.set(false); this.message.set('Заметка удалена.'); this.reload(); },
      error: error => { this.deleting.set(false); this.error.set(errorMessage(error)); }
    });
  }
}
