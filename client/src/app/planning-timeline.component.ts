import { Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PlannedActivity } from './api.service';
import { formatDuration } from './planning-calendar';

const minutesPerHour = 60;
const minutesPerDay = 24 * minutesPerHour;
const minimumAxisHeight = 640;
const labelSpacing = 24;

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
  readonly selected = output<PlannedActivity>();
  readonly edited = output<PlannedActivity>();

  readonly formatDuration = formatDuration;
  readonly timeline = computed(() => {
    const midnight = Date.parse(this.date() + 'T00:00:00Z');
    const entries = this.activities().map(activity => ({
      activity,
      start: (Date.parse(activity.date + 'T' + activity.startTime + 'Z') - midnight) / 60_000
    }));

    let firstMinute = 8 * minutesPerHour;
    let lastMinute = minutesPerDay;
    for (const { activity, start } of entries) {
      firstMinute = Math.min(firstMinute, Math.floor(start / minutesPerHour) * minutesPerHour);
      lastMinute = Math.max(lastMinute, start + activity.durationMinutes);
    }

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
    for (let index = 1; index < marks.length; index++) {
      marks[index].labelLine = Math.max(marks[index].labelLine, marks[index - 1].labelLine + labelSpacing);
    }
    let lastLabelLine = axisHeight;
    for (let index = marks.length - 1; index >= 0; index--) {
      marks[index].labelLine = Math.min(marks[index].labelLine, lastLabelLine);
      lastLabelLine = marks[index].labelLine - labelSpacing;
    }

    return { ticks, marks, span, axisHeight };
  });

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
