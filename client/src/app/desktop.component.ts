import { DatePipe, DOCUMENT } from '@angular/common';
import { afterNextRender, Component, DestroyRef, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, distinctUntilChanged, filter, finalize, forkJoin, fromEvent, map, merge, of, skip, startWith, switchMap, timer } from 'rxjs';
import { ApiService, DaySession, DesktopNote, DesktopTask, Session, Sticker, errorMessage, today } from './api.service';
import { NoteContentComponent } from './note-content.component';
import { DaySessionsComponent } from './day-sessions.component';
import { ConfirmationService } from './confirmation.service';
import { formatNoteTime } from './time-format';

interface Point { x: number; y: number; }
interface Area extends Point { width: number; height: number; }
interface Layout {
  width: number;
  height: number;
  cardWidth: number;
  cardHeight: number;
  toolbar: Area;
  centeredSessions: Area;
}
interface Drag {
  sticker: Sticker;
  pointerId: number;
  start: Point;
  origin: Point;
  position: Point;
  moved: boolean;
}

function intersects(a: Area, b: Area) {
  return a.x < b.x + b.width && a.x + a.width > b.x && a.y < b.y + b.height && a.y + a.height > b.y;
}

@Component({
  imports: [FormsModule, RouterLink, DatePipe, NoteContentComponent, DaySessionsComponent],
  templateUrl: './desktop.component.html',
  styleUrl: './desktop.component.scss'
})
export class DesktopComponent {
  private readonly api = inject(ApiService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly sheet = viewChild.required<ElementRef<HTMLElement>>('sheet');
  private readonly toolbar = viewChild.required<ElementRef<HTMLElement>>('toolbar');
  private readonly sessionsArea = viewChild.required<ElementRef<HTMLElement>>('sessionsArea');
  private readonly cardSize = viewChild.required<ElementRef<HTMLElement>>('cardSize');
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly refreshSessions = new Subject<void>();
  private readonly layout = signal<Layout | null>(null);
  private readonly stickers = signal<Sticker[]>([]);
  private readonly daySessions = signal<DaySession[]>([]);
  private readonly earlierCurrent = signal<DaySession | null>(null);
  private readonly drag = signal<Drag | null>(null);
  private readonly pendingMove = signal<{ id: number; position: Point } | null>(null);
  private readonly movedSessionsPosition = signal<Point | null>(null);
  private sessionsDrag: { pointerId: number; offset: Point } | null = null;
  private ignoreClick = false;

  readonly currentSession = this.api.activeSession;
  readonly formatNoteTime = formatNoteTime;
  readonly sessions = computed(() => {
    const current = this.currentSession();
    const rows = this.daySessions().filter(row => row.session.endTime || row.session.id === current?.id);
    if (!current) return rows;
    if (rows.some(row => row.session.id === current.id)) {
      return rows.map(row => row.session.id === current.id ? { ...row, session: current } : row);
    }
    const earlier = this.earlierCurrent();
    const context = earlier?.session.id === current.id ? earlier : null;
    return [{ session: current, fullCode: context?.fullCode ?? '', targetName: context?.targetName ?? 'Текущая сессия' }, ...rows];
  });
  readonly loadingStickers = signal(true);
  readonly loadingSessions = signal(true);
  readonly sessionsVisible = signal(true);
  readonly sessionsPosition = computed(() => {
    const position = this.movedSessionsPosition();
    const layout = this.layout();
    if (!position || !layout) return null;
    return {
      x: Math.max(0, Math.min(position.x, layout.width - layout.centeredSessions.width)),
      y: Math.max(layout.toolbar.y + layout.toolbar.height, Math.min(position.y, layout.height - layout.centeredSessions.height))
    };
  });
  readonly stickersError = signal('');
  readonly sessionsError = signal('');
  readonly actionError = signal('');
  readonly saving = signal(false);
  readonly moving = signal(false);
  readonly removingStickerId = signal<number | null>(null);
  readonly changingSession = signal(false);
  readonly dialogMode = signal<'create' | 'edit' | 'read' | null>(null);
  readonly selectedSticker = signal<Sticker | null>(null);
  readonly dialogError = signal('');
  draft = '';

  private readonly positions = computed(() => {
    const layout = this.layout();
    if (!layout) return [];
    const slots = this.availableSlots(layout);
    const positioned = this.stickers().map(sticker => {
      const position = sticker.x === null || sticker.y === null ? null : {
        x: sticker.x * (layout.width - layout.cardWidth),
        y: sticker.y * (layout.height - layout.cardHeight)
      };
      return { sticker, position: position && this.isAllowed(position, layout) ? position : null };
    });
    const area = (point: Point): Area => ({ ...point, width: layout.cardWidth, height: layout.cardHeight });
    const occupied = positioned.flatMap(({ position }) => position ? [area(position)] : []);
    return positioned.flatMap(({ sticker, position }, index) => {
      const preferred = slots[index % slots.length];
      const isFree = (slot: Point) => !occupied.some(existing => intersects(area(slot), existing));
      const placed = position ?? (preferred && isFree(preferred) ? preferred : slots.find(isFree) ?? preferred);
      if (!placed) return [];
      if (!position) occupied.push(area(placed));
      return [{ sticker, position: placed }];
    });
  });

  readonly cards = computed(() => {
    const drag = this.drag();
    const pending = this.pendingMove();
    return this.positions().map(({ sticker, position }) => ({
      sticker,
      position: drag?.sticker.id === sticker.id ? drag.position : pending?.id === sticker.id ? pending.position : position,
      dragging: drag?.sticker.id === sticker.id && drag.moved
    }));
  });

  constructor() {
    afterNextRender(() => {
      const observer = new ResizeObserver(() => this.measure());
      for (const ref of [this.sheet, this.toolbar, this.sessionsArea, this.cardSize]) observer.observe(ref().nativeElement);
      this.destroyRef.onDestroy(() => observer.disconnect());
      this.measure();
    });
    this.loadStickers();
    merge(this.refreshSessions,
      fromEvent(this.document, 'visibilitychange').pipe(filter(() => this.document.visibilityState === 'visible')),
      timer(0, 30_000).pipe(map(() => today()), distinctUntilChanged(), skip(1))
    ).pipe(startWith(undefined), switchMap(() => {
      this.sessionsError.set('');
      const date = today();
      return forkJoin({ rows: this.api.daySessions(date), current: this.api.currentSession() }).pipe(
        switchMap(({ rows, current }) => {
          this.daySessions.set(rows);
          if (!current || rows.some(row => row.session.id === current.id)) return of(null);
          const started = today(new Date(current.startTime));
          return started === date ? of(null) : this.api.daySessions(started).pipe(
            map(rows => rows.find(row => row.session.id === current.id) ?? null)
          );
        }),
        catchError(error => {
          this.sessionsError.set(errorMessage(error));
          return EMPTY;
        }),
        finalize(() => this.loadingSessions.set(false))
      );
    }), takeUntilDestroyed()).subscribe(row => this.earlierCurrent.set(row));
  }

  loadStickers() {
    this.loadingStickers.set(true);
    this.stickersError.set('');
    this.api.stickers().pipe(
      finalize(() => this.loadingStickers.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: stickers => this.stickers.set(stickers),
      error: error => this.stickersError.set(errorMessage(error))
    });
  }

  reloadSessions() { this.refreshSessions.next(); }

  hideSessions(showButton: HTMLButtonElement) {
    this.sessionsVisible.set(false);
    showButton.focus();
  }

  startSessionsDrag(event: PointerEvent) {
    if (event.button !== 0 || !event.isPrimary || this.sessionsDrag || (event.target as Element).closest('button')) return;
    const panel = this.sessionsArea().nativeElement.getBoundingClientRect();
    this.sessionsDrag = { pointerId: event.pointerId, offset: { x: event.clientX - panel.left, y: event.clientY - panel.top } };
    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
  }

  moveSessionsDrag(event: PointerEvent) {
    const drag = this.sessionsDrag;
    if (drag?.pointerId !== event.pointerId) return;
    const pointer = this.pointerPosition(event);
    this.movedSessionsPosition.set({ x: pointer.x - drag.offset.x, y: pointer.y - drag.offset.y });
  }

  endSessionsDrag(event: PointerEvent) {
    if (this.sessionsDrag?.pointerId === event.pointerId) this.sessionsDrag = null;
  }

  createSticker() {
    this.draft = '';
    this.dialogError.set('');
    this.openDialog('create');
  }

  readSticker(sticker: Sticker, event?: MouseEvent) {
    if (this.ignoreClick && event && event.detail > 0) {
      this.ignoreClick = false;
      return;
    }
    this.selectedSticker.set(sticker);
    this.openDialog('read');
  }

  editSticker(sticker: Sticker) {
    this.selectedSticker.set(sticker);
    this.draft = sticker.text ?? '';
    this.dialogError.set('');
    this.openDialog('edit');
  }

  closeDialog() {
    this.dialog().nativeElement.close();
    this.dialogMode.set(null);
    this.selectedSticker.set(null);
  }

  cancelDialog(event: Event) {
    event.preventDefault();
    if (!this.saving()) this.closeDialog();
  }

  saveSticker() {
    if (this.saving() || !this.draft.trim()) return;
    const id = this.dialogMode() === 'edit' ? this.selectedSticker()?.id : undefined;
    this.saving.set(true);
    this.dialogError.set('');
    this.api.saveTextSticker(this.draft, id).pipe(
      finalize(() => this.saving.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: sticker => {
        this.stickers.update(stickers => id === undefined ? [...stickers, sticker]
          : stickers.map(item => item.id === sticker.id ? sticker : item));
        this.closeDialog();
      },
      error: error => this.dialogError.set(errorMessage(error))
    });
  }

  removeSticker(sticker: Sticker) {
    if (this.removingStickerId() !== null || this.moving()) return;
    const confirmation = sticker.text === null ? of(true) : this.confirmation.ask('Удалить текстовый стикер?');
    confirmation.pipe(filter(Boolean), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.deleteSticker(sticker));
  }

  private deleteSticker(sticker: Sticker) {
    this.removingStickerId.set(sticker.id);
    this.actionError.set('');
    const request = sticker.task
      ? this.api.unpinTaskSticker(sticker.task.id)
      : sticker.note ? this.api.unpinNoteSticker(sticker.note.id) : this.api.deleteTextSticker(sticker.id);
    request.pipe(
      finalize(() => this.removingStickerId.set(null)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: () => this.stickers.update(stickers => stickers.filter(item => item.id !== sticker.id)),
      error: error => this.actionError.set(errorMessage(error))
    });
  }

  startSession(task: DesktopTask) {
    if (this.changingSession()) return;
    this.changeSession(this.api.startSession(task.subjectId, task.projectId, task.id));
  }

  closeSession(session: Session) {
    if (this.changingSession()) return;
    this.changeSession(this.api.closeSession(session.id));
  }

  taskLink(task: DesktopTask) { return ['/projects', task.projectId, 'tasks', task.id]; }

  noteLink(note: DesktopNote) {
    switch (note.kind) {
      case 'subject': return ['/subjects', note.subjectId, 'notes'];
      case 'project': return ['/projects', note.projectId, 'notes'];
      case 'task': return ['/projects', note.projectId, 'tasks', note.taskId, 'notes'];
      case 'diary': return ['/diary'];
    }
  }

  noteQuery(note: DesktopNote) {
    return note.kind === 'diary' ? { editNote: note.id, date: note.date } : { editNote: note.id };
  }

  noteKindLabel(note: DesktopNote) {
    switch (note.kind) {
      case 'diary': return 'Запись дневника';
      case 'subject': return 'Заметка предмета';
      case 'project': return 'Заметка проекта';
      case 'task': return 'Заметка задачи';
    }
  }

  startDrag(event: PointerEvent, sticker: Sticker, origin: Point) {
    if (event.button !== 0 || !event.isPrimary || this.moving() || this.drag()) return;
    const target = event.target as Element;
    if (target.closest('.sticker-action')) return;
    const button = target.closest('button');
    this.ignoreClick = false;
    const capture = button ?? event.currentTarget as HTMLElement;
    capture.setPointerCapture(event.pointerId);
    this.drag.set({
      sticker, origin, pointerId: event.pointerId, position: origin, moved: false,
      start: this.pointerPosition(event)
    });
  }

  moveDrag(event: PointerEvent) {
    const drag = this.drag();
    const layout = this.layout();
    if (!drag || drag.pointerId !== event.pointerId || !layout) return;
    const pointer = this.pointerPosition(event);
    const dx = pointer.x - drag.start.x;
    const dy = pointer.y - drag.start.y;
    if (!drag.moved && Math.hypot(dx, dy) < 4) return;
    this.drag.set({ ...drag, position: this.clamp({ x: drag.origin.x + dx, y: drag.origin.y + dy }, layout), moved: true });
  }

  endDrag(event: PointerEvent) {
    this.moveDrag(event);
    const drag = this.drag();
    if (!drag || drag.pointerId !== event.pointerId) return;
    this.drag.set(null);
    this.ignoreClick = drag.moved;
    if (drag.moved) this.savePosition(drag.sticker, drag.position);
  }

  cancelDrag(event: PointerEvent) {
    if (this.drag()?.pointerId === event.pointerId) this.drag.set(null);
  }

  moveWithKeys(event: KeyboardEvent, sticker: Sticker, origin: Point) {
    if (event.target !== event.currentTarget) return;
    const direction = {
      ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1]
    }[event.key];
    const layout = this.layout();
    if (!direction || !layout) return;
    event.preventDefault();
    if (this.moving()) return;
    const [x, y] = direction;
    this.savePosition(sticker, this.clamp({
      x: origin.x + x * layout.cardWidth,
      y: origin.y + y * layout.cardHeight
    }, layout));
  }

  private openDialog(mode: 'create' | 'edit' | 'read') {
    this.dialogMode.set(mode);
    const dialog = this.dialog().nativeElement;
    dialog.showModal();
    // Angular inserts the form after showModal sets initial focus.
    if (mode !== 'read') requestAnimationFrame(() => dialog.querySelector('textarea')?.focus());
  }

  private changeSession(request: ReturnType<ApiService['startSession']>) {
    this.changingSession.set(true);
    this.actionError.set('');
    request.pipe(
      finalize(() => this.changingSession.set(false)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: () => this.reloadSessions(),
      error: error => {
        this.actionError.set(errorMessage(error));
        this.reloadSessions();
      }
    });
  }

  private measure() {
    const sheet = this.sheet().nativeElement.getBoundingClientRect();
    const card = this.cardSize().nativeElement.getBoundingClientRect();
    const areaOnSheet = (ref: ElementRef<HTMLElement>): Area => {
      const rect = ref.nativeElement.getBoundingClientRect();
      return { x: rect.left - sheet.left, y: rect.top - sheet.top, width: rect.width, height: rect.height };
    };
    const sessions = this.sessionsArea().nativeElement.getBoundingClientRect();
    this.layout.set({
      width: sheet.width, height: sheet.height, cardWidth: card.width, cardHeight: card.height,
      toolbar: areaOnSheet(this.toolbar()),
      centeredSessions: {
        x: (sheet.width - sessions.width) / 2, y: (sheet.height - sessions.height) / 2,
        width: sessions.width, height: sessions.height
      }
    });
  }

  private pointerPosition(event: PointerEvent): Point {
    const sheet = this.sheet().nativeElement.getBoundingClientRect();
    return { x: event.clientX - sheet.left, y: event.clientY - sheet.top };
  }

  private availableSlots(layout: Layout): Point[] {
    const slots: Point[] = [];
    const slotsOverSessions: Point[] = [];
    const gap = 6;
    const axisSlots = (starts: number[], size: number, limit: number) => {
      const positions = new Set<number>();
      for (const start of starts) {
        for (let position = start; position + size <= limit; position += size + gap) positions.add(position);
      }
      return [...positions].sort((a, b) => a - b);
    };
    const { toolbar, centeredSessions: sessions } = layout;
    const columns = axisSlots([gap, toolbar.x + toolbar.width + gap, sessions.x + sessions.width + gap], layout.cardWidth, layout.width);
    const rows = axisSlots([gap, toolbar.y + toolbar.height + gap, sessions.y + sessions.height + gap], layout.cardHeight, layout.height);
    for (const y of rows) {
      for (const x of columns) {
        if (!this.isAllowed({ x, y }, layout)) continue;
        const card = { x, y, width: layout.cardWidth, height: layout.cardHeight };
        (intersects(card, sessions) ? slotsOverSessions : slots).push({ x, y });
      }
    }
    return [...slots, ...slotsOverSessions];
  }

  private isAllowed(position: Point, layout: Layout) {
    const area = { ...position, width: layout.cardWidth, height: layout.cardHeight };
    return area.x >= 0 && area.y >= 0 && area.x + area.width <= layout.width && area.y + area.height <= layout.height
      && !intersects(area, layout.toolbar);
  }

  private clamp(position: Point, layout: Layout) {
    return {
      x: Math.max(0, Math.min(position.x, layout.width - layout.cardWidth)),
      y: Math.max(0, Math.min(position.y, layout.height - layout.cardHeight))
    };
  }

  private savePosition(sticker: Sticker, position: Point) {
    const layout = this.layout();
    if (!layout || !this.isAllowed(position, layout) || this.moving() || sticker.id === this.removingStickerId()) return;
    const x = position.x / Math.max(1, layout.width - layout.cardWidth);
    const y = position.y / Math.max(1, layout.height - layout.cardHeight);
    this.moving.set(true);
    this.pendingMove.set({ id: sticker.id, position });
    this.actionError.set('');
    this.api.moveSticker(sticker.id, x, y).pipe(
      finalize(() => {
        this.pendingMove.set(null);
        this.moving.set(false);
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: updated => this.stickers.update(stickers => stickers.map(item => item.id === updated.id ? updated : item)),
      error: error => this.actionError.set(errorMessage(error))
    });
  }
}
