import { afterNextRender, Component, DestroyRef, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import { Project, ProjectsApi } from './projects-api';

@Component({
  selector: 'app-project-delete',
  imports: [FormsModule],
  styles: `
    .delete-confirmation { display: flex; align-items: start; gap: 10px; margin: 20px 0; }
    .delete-confirmation input { width: auto; margin-top: 5px; flex-shrink: 0; }
    .delete-button { background: #a43e30; color: white; }
  `,
  template: `
    <dialog #dialog class="panel subject-settings-dialog" aria-labelledby="project-delete-title" (cancel)="onCancel($event)">
      <div class="dialog-heading">
        <h2 id="project-delete-title">Удалить проект</h2>
        <button class="dialog-close secondary" type="button" aria-label="Закрыть окно удаления" (click)="cancelled.emit()" [disabled]="busy()">×</button>
      </div>
      <p>Проект «{{ project().name }}» будет удалён навсегда вместе со всеми задачами, сессиями и заметками проекта, включая архивные данные.</p>
      <p class="error">Восстановить данные после удаления нельзя. Открытая сессия этого проекта также будет удалена.</p>
      <form autocomplete="off" (ngSubmit)="remove()">
        <label for="delete-project-code">Введите код проекта: <strong>{{ project().code }}</strong></label>
        <input id="delete-project-code" name="confirmation" autocomplete="off" [(ngModel)]="confirmation" [disabled]="busy()" autofocus>
        <label class="delete-confirmation">
          <input type="checkbox" name="acknowledged" [(ngModel)]="acknowledged" [disabled]="busy()">
          Я понимаю, что проект и все связанные данные будут удалены безвозвратно.
        </label>
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <div class="actions">
          <button class="delete-button" type="submit" [disabled]="!canDelete()">{{ busy() ? 'Удаляем…' : 'Удалить навсегда' }}</button>
          <button class="secondary" type="button" (click)="cancelled.emit()" [disabled]="busy()">Отмена</button>
        </div>
      </form>
    </dialog>
  `
})
export class ProjectDeleteComponent {
  private readonly api = inject(ProjectsApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly project = input.required<Project>();
  readonly deleted = output<void>();
  readonly cancelled = output<void>();
  readonly busy = signal(false);
  readonly error = signal('');
  confirmation = '';
  acknowledged = false;

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

  canDelete() { return !this.busy() && this.acknowledged && this.confirmation === this.project().code; }
  onCancel(event: Event) {
    event.preventDefault();
    if (!this.busy()) this.cancelled.emit();
  }
  remove() {
    if (!this.canDelete()) return;
    this.busy.set(true); this.error.set('');
    this.api.deleteProject(this.project().id, this.confirmation)
      .pipe(finalize(() => this.busy.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.deleted.emit(), error: error => this.error.set(errorMessage(error)) });
  }
}
