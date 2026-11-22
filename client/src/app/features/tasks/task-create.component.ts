import { afterNextRender, Component, DestroyRef, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import { ProjectTask, TasksApi } from './tasks-api';
import type { Project } from '../projects/projects-api';

@Component({
  selector: 'app-task-create',
  imports: [FormsModule],
  template: `
    <dialog #dialog class="panel task-edit-dialog" aria-labelledby="task-create-title" (cancel)="onCancel($event)">
      <div class="dialog-heading">
        <div><h2 id="task-create-title">Новая задача</h2><p class="muted">{{ project().name }}</p></div>
        <button class="dialog-close secondary" type="button" aria-label="Закрыть создание задачи" (click)="cancelled.emit()" [disabled]="busy()">×</button>
      </div>
      <form autocomplete="off" (ngSubmit)="save()" #form="ngForm">
        <label for="new-task-name">Название задачи</label>
        <input autocomplete="off" id="new-task-name" name="name" [(ngModel)]="name" required maxlength="200" autofocus [disabled]="busy()">
        <p class="field-hint">Код задачи присваивается автоматически после создания.</p>
        <label for="new-task-body">Описание задачи</label>
        <textarea autocomplete="off" id="new-task-body" name="body" [(ngModel)]="body" required maxlength="20000" rows="7" [disabled]="busy()"></textarea>
        <p class="field-hint">{{ body.length }} / 20000</p>
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <div class="actions">
          <button type="submit" [disabled]="form.invalid || !name.trim() || !body.trim() || busy()">{{ busy() ? 'Сохраняем…' : 'Создать задачу' }}</button>
          <button class="secondary" type="button" (click)="cancelled.emit()" [disabled]="busy()">Отмена</button>
        </div>
      </form>
    </dialog>
  `
})
export class TaskCreateComponent {
  private readonly api = inject(TasksApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly project = input.required<Project>();
  readonly created = output<ProjectTask>();
  readonly cancelled = output<void>();
  readonly busy = signal(false);
  readonly error = signal('');
  name = '';
  body = '';

  constructor() {
    let previousOverflow: string | undefined;
    afterNextRender(() => {
      this.dialog().nativeElement.showModal();
      previousOverflow = this.document.body.style.overflow;
      this.document.body.style.overflow = 'hidden';
    });
    this.destroyRef.onDestroy(() => {
      this.dialog().nativeElement.close();
      if (previousOverflow !== undefined) this.document.body.style.overflow = previousOverflow;
    });
  }

  onCancel(event: Event) { event.preventDefault(); if (!this.busy()) this.cancelled.emit(); }
  save() {
    if (this.busy() || !this.name.trim() || !this.body.trim() || this.name.length > 200 || this.body.length > 20000) return;
    this.busy.set(true); this.error.set('');
    this.api.createTask(this.project().id, { name: this.name, body: this.body })
      .pipe(finalize(() => this.busy.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: task => this.created.emit(task), error: error => this.error.set(errorMessage(error)) });
  }
}
