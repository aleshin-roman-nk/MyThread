import { afterNextRender, Component, DestroyRef, ElementRef, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { ApiService, errorMessage, Subject } from './api.service';
import { isValidShortCode, ShortCodeDirective } from './short-code.directive';

@Component({
  selector: 'app-subject-settings',
  imports: [FormsModule, ShortCodeDirective],
  template: `
    <dialog #dialog class="panel subject-settings-dialog" aria-labelledby="subject-settings-title" (cancel)="onCancel($event)">
      <div class="dialog-heading">
        <h2 id="subject-settings-title">Настройки предмета</h2>
        <button class="dialog-close secondary" type="button" aria-label="Закрыть настройки" (click)="cancelled.emit()" [disabled]="busy()">×</button>
      </div>
      <form autocomplete="off" (ngSubmit)="save()" #form="ngForm">
        <label for="settings-subject-name">Название предмета</label>
        <input autocomplete="off" id="settings-subject-name" name="name" [(ngModel)]="name" required maxlength="100" autofocus [disabled]="busy()">
        <p class="field-hint">До 100 символов. Название должно быть уникальным.</p>
        <label for="settings-subject-short-name">Короткое имя</label>
        <input autocomplete="off" id="settings-subject-short-name" name="shortName" [(ngModel)]="shortName" appShortCode #codeField="ngModel" required maxlength="8" [disabled]="busy()">
        <p class="field-hint">До 8 символов, без слешей. Используется в полных кодах проектов и задач.</p>
        @if (codeField.dirty && codeField.invalid) { <p class="error" role="alert">Короткое имя должно содержать от 1 до 8 символов, без слешей и управляющих символов.</p> }
        <label for="settings-subject-description">Описание предмета</label>
        <textarea autocomplete="off" id="settings-subject-description" name="description" [(ngModel)]="description" maxlength="4000" rows="7" [disabled]="busy()"></textarea>
        <p class="field-hint">{{ description.length }} / 4000 · Поддерживается Markdown.</p>
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <div class="actions">
          <button type="submit" [disabled]="form.invalid || !name.trim() || !shortName.trim() || busy()">{{ busy() ? 'Сохраняем…' : 'Сохранить изменения' }}</button>
          <button class="secondary" type="button" (click)="cancelled.emit()" [disabled]="busy()">Отмена</button>
        </div>
      </form>
    </dialog>
  `
})
export class SubjectSettingsComponent {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly document = inject(DOCUMENT);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  readonly subject = input.required<Subject>();
  readonly saved = output<Subject>();
  readonly cancelled = output<void>();
  readonly busy = signal(false);
  readonly error = signal('');
  name = '';
  shortName = '';
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
    effect(() => {
      this.name = this.subject().name;
      this.shortName = this.subject().shortName;
      this.description = this.subject().description;
    });
  }

  onCancel(event: Event) {
    event.preventDefault();
    if (!this.busy()) this.cancelled.emit();
  }

  save() {
    if (this.busy() || !this.name.trim() || !isValidShortCode(this.shortName) || this.description.length > 4000) return;
    this.busy.set(true); this.error.set('');
    this.api.updateSubject(this.subject().id, this.name, this.shortName, this.description)
      .pipe(finalize(() => this.busy.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: subject => this.saved.emit(subject), error: error => this.error.set(errorMessage(error)) });
  }
}
