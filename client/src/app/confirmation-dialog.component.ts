import { afterRenderEffect, Component, ElementRef, inject, viewChild } from '@angular/core';
import { ConfirmationService } from './confirmation.service';

@Component({
  selector: 'app-confirmation-dialog',
  template: `
    <dialog #dialog class="panel confirmation-dialog" aria-labelledby="confirmation-title"
            aria-describedby="confirmation-message" (cancel)="cancel($event)">
      <h2 id="confirmation-title">Подтверждение</h2>
      <p id="confirmation-message">{{ request()?.message }}</p>
      <div class="confirmation-actions">
        <button type="button" class="secondary" autofocus (click)="answer(false)">Отмена</button>
        <button type="button" (click)="answer(true)">Да</button>
      </div>
    </dialog>
  `,
  styles: `
    .confirmation-dialog {
      width: min(420px, calc(100% - 32px));
      max-height: calc(100dvh - 32px);
      color: inherit;
      overflow: auto;
      box-shadow: 0 24px 80px #152d3640;
    }
    .confirmation-dialog::backdrop { background: #172b27a6; }
    .confirmation-dialog p { margin: 12px 0 24px; overflow-wrap: anywhere; }
    .confirmation-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 10px; }
  `
})
export class ConfirmationDialogComponent {
  readonly request = inject(ConfirmationService).request;
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    afterRenderEffect(() => {
      const element = this.dialog().nativeElement;
      if (this.request()) {
        if (!element.open) element.showModal();
      } else if (element.open) {
        element.close();
      }
    });
  }

  answer(confirmed: boolean) { this.request()?.answer(confirmed); }

  cancel(event: Event) {
    event.preventDefault();
    this.answer(false);
  }
}
