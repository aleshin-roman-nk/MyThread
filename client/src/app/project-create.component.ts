import { afterNextRender, Component, DestroyRef, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { ApiService, errorMessage, Project, Subject } from './api.service';

@Component({
  selector: 'app-project-create',
  imports: [FormsModule],
  template: `
    <dialog #dialog class="panel project-create-dialog" aria-labelledby="project-create-title" (cancel)="onCancel($event)">
      <div class="dialog-heading">
        <div><h2 id="project-create-title">Новый проект</h2><p class="muted">{{ subject().name }}</p></div>
        <button class="dialog-close secondary" type="button" aria-label="Закрыть создание проекта" (click)="cancelled.emit()" [disabled]="busy()">×</button>
      </div>
      <form autocomplete="off" (ngSubmit)="save()" #form="ngForm">
        <label for="project-name">Полное название проекта</label>
        <input autocomplete="off" id="project-name" name="name" [(ngModel)]="name" required maxlength="200" placeholder="Конспекты и практика" autofocus [disabled]="busy()">
        <p class="field-hint">Название — до 200 символов. Код присваивается автоматически после создания.</p>
        <label for="project-description">Описание проекта</label>
        <textarea autocomplete="off" id="project-description" name="description" [(ngModel)]="description" maxlength="4000" rows="4" [disabled]="busy()"></textarea>
        <p class="field-hint">Необязательно. До 4000 символов.</p>
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <div class="actions">
          <button type="submit" [disabled]="form.invalid || !name.trim() || busy()">{{ busy() ? 'Сохраняем…' : 'Создать проект' }}</button>
          <button class="secondary" type="button" (click)="cancelled.emit()" [disabled]="busy()">Отмена</button>
        </div>
      </form>
    </dialog>
  `
})
export class ProjectCreateComponent {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly subject = input.required<Subject>();
  readonly created = output<Project>();
  readonly cancelled = output<void>();
  readonly busy = signal(false);
  readonly error = signal('');
  name = '';
  description = '';

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
    if (this.busy() || !this.name.trim() || this.description.length > 4000) return;
    this.busy.set(true); this.error.set('');
    this.api.createProject(this.subject().id, this.name, this.description)
      .pipe(finalize(() => this.busy.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: project => this.created.emit(project), error: error => this.error.set(errorMessage(error)) });
  }
}
