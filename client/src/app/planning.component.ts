import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, combineLatest, map, of, startWith, Subject, switchMap } from 'rxjs';
import { ApiService, errorMessage, PlannedActivity, today } from './api.service';
import { calendarRange, dateKey, formatDuration, months, weekdays, weekRange } from './planning-calendar';
import { PlanningEditorComponent } from './planning-editor.component';
import { PlanningTimelineComponent } from './planning-timeline.component';

@Component({
  imports: [FormsModule, PlanningEditorComponent, PlanningTimelineComponent],
  templateUrl: './planning.component.html',
  styleUrl: './planning.component.scss'
})
export class PlanningComponent {
  private readonly api = inject(ApiService);
  private readonly refresh = new Subject<void>();
  private readonly dayLabel = new Intl.DateTimeFormat('ru', {
    weekday: 'long', day: 'numeric', month: 'long', timeZone: 'UTC'
  });
  private readonly shortDate = new Intl.DateTimeFormat('ru', { day: 'numeric', month: 'short', timeZone: 'UTC' });
  readonly today = today();
  readonly date = signal(this.today);
  readonly month = computed(() => this.date().slice(0, 7));
  readonly calendar = computed(() => calendarRange(this.month()));
  readonly year = computed(() => Number(this.month().slice(0, 4)));
  readonly monthNumber = computed(() => Number(this.month().slice(5)));
  readonly months = months;
  readonly weekdays = weekdays;
  readonly formatDuration = formatDuration;

  private readonly activities = signal<PlannedActivity[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly editor = signal<{ date: string; startTime: string; activity?: PlannedActivity } | null>(null);
  readonly summaryPeriod = signal<'week' | 'month'>('week');
  readonly selectedDayLabel = computed(() => this.dayLabel.format(new Date(this.date())));
  readonly weekLabel = computed(() => {
    const { from, through } = weekRange(this.date());
    return this.shortDate.format(new Date(from)) + ' — ' + this.shortDate.format(new Date(through));
  });
  readonly daysWithActivities = computed(() => new Set(this.activities().map(activity => activity.date)));
  readonly dailyActivities = computed(() => this.activities().filter(activity => activity.date === this.date()));
  private readonly activitySelection = signal<{ date: string; id: number } | null>(null);
  readonly selectedActivity = computed(() => {
    const selection = this.activitySelection();
    const activities = this.dailyActivities();
    return activities.find(activity => activity.date === selection?.date && activity.id === selection.id) ?? activities[0];
  });
  readonly totals = computed(() => {
    const weekly = this.summaryPeriod() === 'week';
    const { from, through } = weekRange(this.date());
    const groups = new Map<number | null, { name: string; minutes: number }>();
    for (const activity of this.activities()) {
      const inPeriod = weekly ? activity.date >= from && activity.date <= through : activity.date.startsWith(this.month());
      if (!inPeriod) continue;
      const id = activity.subject?.id ?? null;
      const group = groups.get(id);
      if (group) group.minutes += activity.durationMinutes;
      else groups.set(id, { name: activity.subject?.name ?? 'Без предмета', minutes: activity.durationMinutes });
    }
    return Array.from(groups, ([id, value]) => ({ id, ...value })).sort((a, b) => b.minutes - a.minutes);
  });
  readonly totalMinutes = computed(() => this.totals().reduce((sum, row) => sum + row.minutes, 0));

  constructor() {
    combineLatest([toObservable(this.calendar), this.refresh.pipe(startWith(undefined))]).pipe(
      switchMap(([{ from, through }]) => {
        this.loading.set(true);
        this.loadError.set('');
        return this.api.plannedActivities(from, through).pipe(
          map(activities => ({ activities, error: '' })),
          catchError(error => of({ activities: [], error: errorMessage(error) }))
        );
      }),
      takeUntilDestroyed()
    ).subscribe(({ activities, error }) => {
      this.activities.set(activities);
      this.loadError.set(error);
      this.loading.set(false);
    });
  }

  changeMonth(offset: number) {
    const target = new Date(this.month() + '-01');
    target.setUTCMonth(target.getUTCMonth() + offset);
    this.selectMonth(target.getUTCFullYear(), target.getUTCMonth() + 1);
  }

  selectMonth(year: number, month: number) {
    if (!Number.isInteger(year) || year < 1 || year > 9999) return;
    const target = new Date(String(year).padStart(4, '0') + '-' + String(month).padStart(2, '0') + '-01');
    target.setUTCMonth(target.getUTCMonth() + 1, 0);
    target.setUTCDate(Math.min(target.getUTCDate(), Number(this.date().slice(-2))));
    this.date.set(dateKey(target));
  }

  openEditor(activity?: PlannedActivity) {
    if (activity) {
      this.editor.set({ date: activity.date, startTime: activity.startTime.slice(0, 5), activity });
      return;
    }

    const date = this.date();
    const dayStart = date + 'T00:00:00';
    const latestEnd = this.activities().reduce((latest, item) =>
      item.date <= date && item.end > latest ? item.end : latest, dayStart);
    this.editor.set({
      date: latestEnd.slice(0, 10),
      startTime: latestEnd === dayStart ? '09:00' : latestEnd.slice(11, 16)
    });
  }

  selectActivity(activity: PlannedActivity) {
    this.activitySelection.set({ date: activity.date, id: activity.id });
  }

  saved(activity: PlannedActivity) {
    this.selectActivity(activity);
    const sameMonth = activity.date.startsWith(this.month());
    this.editor.set(null);
    this.date.set(activity.date);
    if (sameMonth) this.reload();
  }

  deleted() {
    this.editor.set(null);
    this.reload();
  }

  reload() { this.refresh.next(); }
}
