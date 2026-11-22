import { Component, DestroyRef, effect, inject, input, output, signal } from '@angular/core';
import { finalize, Observable, Subscription } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import { DesktopApi } from './desktop-api';

@Component({
  selector: 'app-desktop-pin',
  styles: `
    :host { display: inline-flex; align-items: center; gap: 8px; flex-shrink: 0; }
    .task-pin { width: var(--task-pin-size, 42px); height: var(--task-pin-size, 42px); padding: 6px; }
    svg { width: 20px; height: 20px; }
    .task-pin.pinned { background: #fff3ba; border-color: #bdab67; }
    .task-pin.pinned:hover { background: #f8e69e; }
  `,
  template: `
    <button class="secondary task-pin" [class.pinned]="pinned()" type="button"
            (click)="togglePin()" [disabled]="busy()" [attr.aria-busy]="busy()"
            aria-label="Закрепление на рабочем столе" [attr.aria-pressed]="pinned()"
            [title]="pinned() === null ? 'Проверить закрепление' : pinned() ? 'Открепить от рабочего стола' : 'Закрепить на рабочем столе'">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        @if (pinned()) {
          <path class="pin-sticker" d="M4 4h19v14l-5 5H4zM18 23v-5h5" fill="#fff9dc" />
          <circle cx="18" cy="20" r="1" fill="currentColor" stroke="none" />
        }
        <g transform="rotate(-36 12 12)">
          <path d="M8 3h8v1l-1 1v4l3 3v2H6v-2l3-3V5L8 4z" [attr.fill]="pinned() ? '#fff3ba' : 'none'" />
          <path d="M12 14v8" />
        </g>
      </svg>
    </button>
    @if (inlineError() && error()) { <span class="danger" role="alert">{{ error() }}</span> }
  `
})
export class DesktopPinComponent {
  private readonly api = inject(DesktopApi);
  private request?: Subscription;
  readonly targetId = input.required<number>();
  readonly kind = input<'task' | 'note'>('task');
  readonly inlineError = input(false);
  readonly changed = output<boolean>();
  readonly errorChanged = output<string>();
  readonly pinned = signal<boolean | null>(null);
  readonly busy = signal(true);
  readonly error = signal('');

  constructor() {
    effect(() => this.load(this.targetId(), this.kind()));
    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
  }

  togglePin() {
    if (this.busy()) return;
    const id = this.targetId();
    const kind = this.kind();
    const pinned = this.pinned();
    if (pinned === null) {
      this.load(id, kind);
      return;
    }
    const request: Observable<unknown> = kind === 'note'
      ? pinned ? this.api.unpinNoteSticker(id) : this.api.pinNoteSticker(id)
      : pinned ? this.api.unpinTaskSticker(id) : this.api.pinTaskSticker(id);
    this.send(request, () => {
      this.pinned.set(!pinned);
      this.changed.emit(!pinned);
    });
  }

  private load(id: number, kind: 'task' | 'note') {
    this.pinned.set(null);
    const status = kind === 'note' ? this.api.noteStickerStatus(id) : this.api.taskStickerStatus(id);
    this.send(status, ({ pinned }) => this.pinned.set(pinned));
  }

  private send<T>(request: Observable<T>, onSuccess: (result: T) => void) {
    this.request?.unsubscribe();
    this.busy.set(true);
    this.error.set('');
    this.errorChanged.emit('');
    this.request = request.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: onSuccess,
      error: error => {
        const message = errorMessage(error);
        this.error.set(message);
        this.errorChanged.emit(message);
      }
    });
  }
}
