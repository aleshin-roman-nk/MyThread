import { afterNextRender, Component, DestroyRef, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { ApiService, errorMessage, ProjectTask, ProjectTaskStatus, taskStatuses } from './api.service';

@Component({
  selector: 'app-task-edit',
  imports: [FormsModule],
  styles: `.project-code { margin-top: 16px; }`,
  template: `
    <dialog #dialog class="panel task-edit-dialog" aria-labelledby="task-edit-title" (cancel)="onCancel($event)">
      <div class="dialog-heading">
        <h2 id="task-edit-title">Изменить задачу</h2>
        <button class="dialog-close secondary" type="button" aria-label="Закрыть правку задачи" (click)="cancelled.emit()" [disabled]="busy()">×</button>
      </div>
      <form autocomplete="off" (ngSubmit)="save()" #form="ngForm">
        <label for="edit-task-name">Название задачи</label>
        <input autocomplete="off" id="edit-task-name" name="name" [(ngModel)]="name" required maxlength="200" autofocus [disabled]="busy()">
        <p class="project-code">{{ task().fullCode }}</p>
        <label for="edit-task-status">Статус задачи</label>
        <select id="edit-task-status" name="status" [(ngModel)]="status" [disabled]="busy()">
          @for (item of statuses; track item.value) { <option [ngValue]="item.value">{{ item.label }}</option> }
        </select>
        <label for="edit-task-body">Описание задачи</label>
        <textarea autocomplete="off" id="edit-task-body" name="body" [(ngModel)]="body" required maxlength="20000" rows="7" [disabled]="busy()"></textarea>
        <p class="field-hint">{{ body.length }} / 20000</p>
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <div class="actions">
          <button type="submit" [disabled]="form.invalid || !name.trim() || !body.trim() || busy()">{{ busy() ? 'Сохраняем…' : 'Сохранить изменения' }}</button>
          <button class="secondary" type="button" (click)="cancelled.emit()" [disabled]="busy()">Отмена</button>
        </div>
      </form>
    </dialog>
  `
})
export class TaskEditComponent {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly task = input.required<ProjectTask>();
  readonly saved = output<ProjectTask>();
  readonly cancelled = output<void>();
  readonly busy = signal(false);
  readonly error = signal('');
  readonly statuses = taskStatuses;
  name = '';
  body = '';
  status: ProjectTaskStatus = 0;

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
    effect(() => {
      const task = this.task();
      this.name = task.name; this.body = task.body; this.status = task.status;
    });
  }
  onCancel(event: Event) { event.preventDefault(); if (!this.busy()) this.cancelled.emit(); }
  save() {
    if (this.busy() || !this.name.trim() || !this.body.trim()) return;
    this.busy.set(true); this.error.set('');
    this.api.updateTask(this.task(), { name: this.name, body: this.body, status: this.status })
      .pipe(finalize(() => this.busy.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: task => this.saved.emit(task), error: error => this.error.set(errorMessage(error)) });
  }
}
