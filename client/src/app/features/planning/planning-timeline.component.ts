import { Component, computed, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { MovePlannedActivity, PlannedActivity } from './planning-api';
import { formatDuration } from '../../shared/calendar';
import { dragStepMinutes, previewScheduleDrop, ScheduleBlock } from './planning-drag';

const minutesPerHour = 60;
const minutesPerDay = 24 * minutesPerHour;
const minimumAxisHeight = 640;
const labelSpacing = 24;
const minuteMs = 60_000;

interface TimelineDrag {
  activity: PlannedActivity;
  date: string;
  pointerId: number;
  origin: number;
  start: number;
  blocks: ScheduleBlock[];
  firstMinute: number;
  lastMinute: number;
  minutesPerPixel: number;
  moved: boolean;
}

@Component({
  selector: 'app-planning-timeline',
  imports: [RouterLink],
  templateUrl: './planning-timeline.component.html',
  styleUrl: './planning-timeline.component.scss'
})
export class PlanningTimelineComponent {
  readonly date = input.required<string>();
  readonly activities = input.required<PlannedActivity[]>();
  readonly selectedActivity = input<PlannedActivity>();
  readonly schedule = input.required<PlannedActivity[]>();
  readonly saving = input(false);
  readonly selected = output<PlannedActivity>();
  readonly edited = output<PlannedActivity>();
  readonly moved = output<{ id: number; position: MovePlannedActivity }>();

  private drag?: TimelineDrag;
  private readonly preview = signal<ReturnType<typeof previewScheduleDrop> | null>(null);
  readonly dropBlocked = computed(() =>
    this.dragging() && this.drag?.date === this.date() && (this.preview()?.blocked ?? false));
  readonly dragging = signal(false);
  private readonly midnight = computed(() => Date.parse(this.date() + 'T00:00:00Z'));
  private readonly displayActivities = computed(() => {
    const preview = this.preview();
    const drag = this.drag;
    if (!preview || !drag || drag.date !== this.date()) return this.activities();

    const start = this.timestamp(preview.start);
    const end = this.timestamp(preview.start + drag.activity.durationMinutes);
    return this.activities().map(activity => activity.id === drag.activity.id
      ? { ...activity, date: start.slice(0, 10), startTime: start.slice(11), end }
      : activity);
  });
  readonly displaySelection = computed(() =>
    this.displayActivities().find(activity => activity.id === this.selectedActivity()?.id));

  readonly formatDuration = formatDuration;
  readonly timeline = computed(() => {
    const entries = this.displayActivities().map(activity => ({ activity, start: this.startMinute(activity) }));

    let firstMinute = 8 * minutesPerHour;
    let lastMinute = minutesPerDay;
    for (const { activity, start } of entries) {
      firstMinute = Math.min(firstMinute, Math.floor(start / minutesPerHour) * minutesPerHour);
      lastMinute = Math.max(lastMinute, start + activity.durationMinutes);
    }

    const drag = this.dragging() && this.drag?.date === this.date() ? this.drag : undefined;
    firstMinute = drag?.firstMinute ?? firstMinute;
    lastMinute = drag?.lastMinute ?? lastMinute;
    const span = lastMinute - firstMinute;
    const axisHeight = Math.max(minimumAxisHeight, entries.length * labelSpacing);
    const position = (minute: number) => (minute - firstMinute) / span * 100;
    const tickStep = Math.ceil(span / (20 * minutesPerHour)) * minutesPerHour;
    const ticks = [];
    for (let minute = firstMinute; minute < lastMinute; minute += tickStep) {
      if ((lastMinute - minute) / span * axisHeight >= 22) ticks.push({ minute, position: position(minute) });
    }
    ticks.push({ minute: lastMinute, position: 100 });

    const marks = entries.map(({ activity, start }) => ({
      activity, position: position(start), labelLine: position(start) * axisHeight / 100
    }));
    // Keep every label readable without moving its time marker.
    const byTime = marks.slice().sort((left, right) => left.position - right.position);
    for (let index = 1; index < byTime.length; index++) {
      byTime[index].labelLine = Math.max(byTime[index].labelLine, byTime[index - 1].labelLine + labelSpacing);
    }
    let lastLabelLine = axisHeight;
    for (let index = byTime.length - 1; index >= 0; index--) {
      byTime[index].labelLine = Math.min(byTime[index].labelLine, lastLabelLine);
      lastLabelLine = byTime[index].labelLine - labelSpacing;
    }

    return { ticks, marks, span, axisHeight, firstMinute, lastMinute };
  });

  startDrag(event: PointerEvent, activity: PlannedActivity, marker: HTMLButtonElement) {
    if (!event.isPrimary || event.button !== 0 || this.saving() || this.drag) return;
    const { firstMinute, lastMinute, span, axisHeight } = this.timeline();
    const blocks = this.schedule().filter(item => item.id !== activity.id)
      .map(item => ({ start: this.startMinute(item), duration: item.durationMinutes }))
      .filter(item => item.start < lastMinute && item.start + item.duration > firstMinute);
    this.drag = {
      activity, date: this.date(), pointerId: event.pointerId, origin: event.pageY, start: this.startMinute(activity),
      blocks, firstMinute, lastMinute: Math.min(lastMinute, (Date.parse('9999-12-31T23:59:00Z') - this.midnight()) / minuteMs),
      minutesPerPixel: span / axisHeight, moved: false
    };
    marker.setPointerCapture(event.pointerId);
  }

  updateDrag(event: PointerEvent) {
    const drag = this.drag;
    if (!drag || event.pointerId !== drag.pointerId || drag.date !== this.date()) return;
    const offset = (event.pageY - drag.origin) * drag.minutesPerPixel;
    if (!drag.moved && Math.abs(offset) < dragStepMinutes / 2) return;
    if (!drag.moved) {
      drag.moved = true;
      this.dragging.set(true);
      this.selected.emit(drag.activity);
    }
    this.preview.set(previewScheduleDrop(drag.blocks, { start: drag.start, duration: drag.activity.durationMinutes },
      offset, drag.firstMinute, drag.lastMinute));
  }

  finishDrag(event: PointerEvent) {
    const drag = this.drag;
    if (!drag || event.pointerId !== drag.pointerId) return;
    if (drag.date === this.date()) {
      this.updateDrag(event);
      const preview = this.preview();
      if (preview && !preview.blocked && preview.start !== drag.start) {
        this.moved.emit({
          id: drag.activity.id,
          position: {
            originalStart: this.timestamp(drag.start),
            durationMinutes: drag.activity.durationMinutes,
            start: this.timestamp(preview.start)
          }
        });
      }
    }
    this.cancelDrag(event);
  }

  cancelDrag(event: PointerEvent) {
    if (event.pointerId !== this.drag?.pointerId) return;
    this.drag = undefined;
    this.dragging.set(false);
    this.preview.set(null);
  }

  private startMinute(activity: PlannedActivity) {
    return (Date.parse(activity.date + 'T' + activity.startTime + 'Z') - this.midnight()) / minuteMs;
  }

  private timestamp(minute: number) {
    return new Date(this.midnight() + minute * minuteMs).toISOString().slice(0, 19);
  }

  activityName(activity: PlannedActivity) {
    return activity.description || activity.subject?.name || 'Занятие';
  }

  markerLabel(activity: PlannedActivity) {
    return activity.startTime.slice(0, 5) + ' — ' + this.activityName(activity);
  }

  tickLabel(minute: number) {
    return formatDuration(minute % minutesPerDay);
  }

  dayOffset(minute: number) {
    return Math.floor(minute / minutesPerDay);
  }
}
