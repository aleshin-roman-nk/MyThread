import { afterNextRender, Component, DestroyRef, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { ApiService, errorMessage, Project, ProjectStatus, projectStatuses } from './api.service';

@Component({
  selector: 'app-project-settings',
  imports: [FormsModule],
  styles: `.actions { margin-top: 20px; } .project-code { margin-top: 16px; }`,
  template: `
    <dialog #dialog class="panel subject-settings-dialog" aria-labelledby="project-settings-title" (cancel)="onCancel($event)">
      <div class="dialog-heading">
        <h2 id="project-settings-title">Настройка проекта</h2>
        <button class="dialog-close secondary" type="button" aria-label="Закрыть настройки" (click)="cancelled.emit()" [disabled]="busy()">×</button>
      </div>
      <form autocomplete="off" (ngSubmit)="save()" #form="ngForm">
        <label for="settings-project-name">Название проекта</label>
        <input autocomplete="off" id="settings-project-name" name="name" [(ngModel)]="name" required maxlength="200" autofocus [disabled]="busy()">
        <p class="project-code">{{ project().fullCode }}</p>
        <label for="settings-project-status">Статус проекта</label>
        <select id="settings-project-status" name="status" [(ngModel)]="status" [disabled]="busy()">
          @for (item of statuses; track item.value) { <option [ngValue]="item.value">{{ item.label }}</option> }
        </select>
        <label for="settings-project-description">Описание проекта</label>
        <textarea autocomplete="off" id="settings-project-description" name="description" [(ngModel)]="description" maxlength="4000" rows="4" [disabled]="busy()"></textarea>
        <p class="field-hint">Необязательно. До 4000 символов.</p>
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <div class="actions">
          <button type="submit" [disabled]="form.invalid || !name.trim() || busy()">{{ busy() ? 'Сохраняем…' : 'Сохранить изменения' }}</button>
          <button class="secondary" type="button" (click)="cancelled.emit()" [disabled]="busy()">Отмена</button>
        </div>
      </form>
    </dialog>
  `
})
export class ProjectSettingsComponent {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly project = input.required<Project>();
  readonly saved = output<Project>();
  readonly cancelled = output<void>();
  readonly busy = signal(false);
  readonly error = signal('');
  readonly statuses = projectStatuses;
  name = '';
  description = '';
  status: ProjectStatus = 0;

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
      this.name = this.project().name;
      this.status = this.project().status;
      this.description = this.project().description;
    });
  }

  onCancel(event: Event) {
    event.preventDefault();
    if (!this.busy()) this.cancelled.emit();
  }

  save() {
    if (this.busy() || !this.name.trim() || this.description.length > 4000) return;
    this.busy.set(true); this.error.set('');
    this.api.updateProject(this.project().id, this.name, this.status, this.description)
      .pipe(finalize(() => this.busy.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: project => this.saved.emit(project), error: error => this.error.set(errorMessage(error)) });
  }
}
