import { Component, DestroyRef, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject as Stream, of, startWith, switchMap } from 'rxjs';
import { ApiService, errorMessage, Project, ProjectNote, Subject } from './api.service';
import { ProjectNoteEditorComponent } from './project-note-editor.component';
import { NoteListComponent } from './note-list.component';
import { BreadcrumbsComponent } from './breadcrumbs.component';
import { loadProjectNotesPage, PageResult, ProjectNotesPage } from './route-pages';

@Component({
  imports: [BreadcrumbsComponent, ProjectNoteEditorComponent, NoteListComponent],
  template: `
    @if (subject() && project()) {
      <app-breadcrumbs [items]="[{ label: 'Предметы', link: ['/subjects'] }, { label: subject()!.name, link: ['/subjects', subject()!.id] }, { label: project()!.name, link: ['/projects', project()!.id] }, { label: 'Заметки' }]" />
    } @else { <app-breadcrumbs [items]="[{ label: 'Предметы', link: ['/subjects'] }, { label: 'Заметки' }]" /> }
    <div class="page-heading">
      <div><h1>Заметки проекта</h1><p class="subtitle">Идеи, наброски мыслей и то, к чему хочется вернуться позже.</p></div>
      <button type="button" (click)="openEditor()" [disabled]="loading() || deleting() || !subject()">+ Новая заметка</button>
    </div>
    @if (error()) { <p class="error" role="alert">{{ error() }} <button class="text-button" (click)="reload()">Повторить</button></p> }
    @if (loading()) { <p role="status">Загружаем заметки…</p> }
    @else if (subject() && !error()) {
      <section aria-label="Заметки проекта">
        <div class="section-heading"><h2>Все заметки</h2><span class="count">{{ notes().length }} заметок</span></div>
        <app-note-list [notes]="notes()" [showContext]="false" [compact]="true" [previewLines]="7" [busy]="deleting()" (viewed)="openEditor($event, true)" (edited)="openEditor($event)" (removed)="remove($event)" />
      </section>
      @if (editorOpen()) {
        <app-project-note-editor [project]="project()!"
          [note]="editing()" [readOnly]="viewing()" (saved)="onSaved()" (updated)="onUpdated($event)" (cancelled)="closeEditor()" />
      }
    }
    @if (message()) { <p class="toast" role="status">{{ message() }}</p> }
  `
})
export class ProjectNotesComponent {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refresh = new Stream<void>();
  readonly subject = signal<Subject | null>(null);
  readonly project = signal<Project | null>(null);
  readonly notes = signal<ProjectNote[]>([]);
  readonly editing = signal<ProjectNote | null>(null);
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
        return index === 0 ? of(routeData['page'] as PageResult<ProjectNotesPage>)
          : loadProjectNotesPage(this.api, Number(this.route.snapshot.paramMap.get('id')));
      }))), takeUntilDestroyed()
    ).subscribe(result => {
      this.error.set(result.error);
      this.subject.set(result.data?.subject ?? null);
      this.project.set(result.data?.project ?? null);
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
  openEditor(note: ProjectNote | null = null, readOnly = false) {
    this.editing.set(note); this.viewing.set(readOnly); this.editorOpen.set(true);
  }
  closeEditor() { this.editorOpen.set(false); this.editing.set(null); }
  onSaved() { this.message.set('Заметка сохранена.'); this.reload(); }
  onUpdated(note: ProjectNote) {
    this.notes.update(notes => notes.map(item => item.id === note.id ? note : item)
      .filter(item => item.projectId === this.project()?.id)
      .sort((a, b) => b.date.localeCompare(a.date) || b.time.localeCompare(a.time) || b.id - a.id));
    this.message.set('Заметка сохранена.');
  }
  remove(note: ProjectNote) {
    this.deleting.set(true);
    this.api.deleteProjectNote(note.projectId, note.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.deleting.set(false); this.message.set('Заметка удалена.'); this.reload(); },
      error: error => { this.deleting.set(false); this.error.set(errorMessage(error)); }
    });
  }
}
