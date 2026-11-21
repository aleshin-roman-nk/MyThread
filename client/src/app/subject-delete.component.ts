import { afterNextRender, Component, DestroyRef, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { ApiService, errorMessage, Subject } from './api.service';

@Component({
  selector: 'app-subject-delete',
  imports: [FormsModule],
  styles: `
    .delete-confirmation { display: flex; align-items: start; gap: 10px; margin: 20px 0; }
    .delete-confirmation input { width: auto; margin-top: 5px; flex-shrink: 0; }
    .delete-button { background: #a43e30; color: white; }
  `,
  template: `
    <dialog #dialog class="panel subject-settings-dialog" aria-labelledby="subject-delete-title" (cancel)="onCancel($event)">
      <div class="dialog-heading">
        <h2 id="subject-delete-title">Удалить предмет</h2>
        <button class="dialog-close secondary" type="button" aria-label="Закрыть окно удаления" (click)="cancelled.emit()" [disabled]="busy()">×</button>
      </div>
      <p>Предмет «{{ subject().name }}» будет удалён навсегда вместе со всеми проектами, задачами, сессиями и заметками, включая архивные данные.</p>
      <p class="error">Восстановить данные после удаления нельзя. Открытая сессия этого предмета также будет удалена.</p>
      <form autocomplete="off" (ngSubmit)="remove()">
        <label for="delete-subject-code">Введите короткое имя предмета: <strong>{{ subject().shortName }}</strong></label>
        <input id="delete-subject-code" name="confirmation" autocomplete="off" [(ngModel)]="confirmation" [disabled]="busy()" autofocus>
        <label class="delete-confirmation">
          <input type="checkbox" name="acknowledged" [(ngModel)]="acknowledged" [disabled]="busy()">
          Я понимаю, что предмет и все связанные данные будут удалены безвозвратно.
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
export class SubjectDeleteComponent {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly subject = input.required<Subject>();
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

  canDelete() { return !this.busy() && this.acknowledged && this.confirmation === this.subject().shortName; }
  onCancel(event: Event) {
    event.preventDefault();
    if (!this.busy()) this.cancelled.emit();
  }
  remove() {
    if (!this.canDelete()) return;
    this.busy.set(true); this.error.set('');
    this.api.deleteSubject(this.subject().id, this.confirmation)
      .pipe(finalize(() => this.busy.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: () => this.deleted.emit(), error: error => this.error.set(errorMessage(error)) });
  }
}
