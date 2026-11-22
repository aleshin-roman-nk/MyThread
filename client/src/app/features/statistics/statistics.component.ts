import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, combineLatest, forkJoin, map, of, startWith, Subject, switchMap } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import { today } from '../../shared/today';
import { SubjectsApi } from '../subjects/subjects-api';
import { PlannedActivity, PlannedActivityReference, PlanningApi } from '../planning/planning-api';
import { calendarRange, formatDuration, months, weekdays } from '../../shared/calendar';
import { SessionsApi } from '../sessions/sessions-api';
import { StatisticsSessionJournalComponent } from './statistics-session-journal.component';
import { monthBounds } from './statistics-period';

const dailyActiveMinutes = (24 - 8) * 60;

interface SubjectTime {
  id: number | null;
  name: string;
  plannedMinutes: number;
  spentMilliseconds: number;
}

interface DaySubject {
  id: number;
  name: string;
  plannedMinutes: number;
  spentMilliseconds: number;
  references: Map<string, PlannedActivityReference>;
}

@Component({
  imports: [FormsModule, RouterLink, StatisticsSessionJournalComponent],
  templateUrl: './statistics.component.html',
  styleUrl: './statistics.component.scss'
})
export class StatisticsComponent {
  private readonly subjectsApi = inject(SubjectsApi);
  private readonly planningApi = inject(PlanningApi);
  private readonly sessionsApi = inject(SessionsApi);
  private readonly refresh = new Subject<void>();
  private readonly dayLabel = new Intl.DateTimeFormat('ru', {
    weekday: 'long', day: 'numeric', month: 'long', timeZone: 'UTC'
  });

  readonly today = today();
  readonly date = signal(this.today);
  readonly journal = signal<{ subject: { id: number; name: string }; date: string; period: 'day' | 'month' } | null>(null);
  readonly month = computed(() => this.date().slice(0, 7));
  readonly year = computed(() => Number(this.month().slice(0, 4)));
  readonly monthNumber = computed(() => Number(this.month().slice(5)));
  readonly months = months;
  readonly weekdays = weekdays;
  readonly formatDuration = formatDuration;
  readonly dailyActiveTime = formatDuration(dailyActiveMinutes);
  readonly selectedDayLabel = computed(() => this.dayLabel.format(new Date(this.date())));

  private readonly monthData = signal<{
    month: string;
    activities: PlannedActivity[];
    subjects: Map<number, string>;
    sessions: { subjectId: number; start: number; end: number }[];
  } | null>(null);
  readonly loading = signal(true);
  readonly loadError = signal('');
  private readonly data = computed(() => {
    const data = this.monthData();
    return data?.month === this.month() ? data : null;
  });
  readonly calendar = computed(() => calendarRange(this.month()));
  private readonly activitiesByDay = computed(() => {
    const days = new Map<string, { activities: PlannedActivity[]; minutes: number }>();
    for (const activity of this.data()?.activities ?? []) {
      let day = days.get(activity.date);
      if (!day) {
        day = { activities: [], minutes: 0 };
        days.set(activity.date, day);
      }
      day.activities.push(activity);
      day.minutes += activity.durationMinutes;
    }
    return days;
  });
  readonly selectedDay = computed(() => this.activitiesByDay().get(this.date()));
  readonly selectedDayDetails = computed(() => {
    const subjects = new Map<number, DaySubject>();
    const withoutSubject: PlannedActivity[] = [];
    for (const activity of this.selectedDay()?.activities ?? []) {
      const { subject } = activity;
      if (!subject) {
        withoutSubject.push(activity);
        continue;
      }

      let group = subjects.get(subject.id);
      if (!group) {
        group = { id: subject.id, name: subject.name, plannedMinutes: 0, spentMilliseconds: 0, references: new Map() };
        subjects.set(subject.id, group);
      }
      group.plannedMinutes += activity.durationMinutes;
      for (const reference of activity.references) {
        const key = reference.taskId === null ? 'project-' + reference.projectId : 'task-' + reference.taskId;
        group.references.set(key, reference);
      }
    }
    const spentBySubject = this.selectedCard()?.spentBySubject;
    if (spentBySubject) {
      const subjectNames = this.data()?.subjects;
      for (const [id, spentMilliseconds] of spentBySubject) {
        const group = subjects.get(id);
        if (group) {
          group.spentMilliseconds = spentMilliseconds;
        } else {
          subjects.set(id, {
            id, name: subjectNames?.get(id) ?? 'Предмет №' + id,
            plannedMinutes: 0, spentMilliseconds, references: new Map()
          });
        }
      }
    }
    return { subjects: Array.from(subjects.values()), withoutSubject };
  });
  readonly cards = computed(() => {
    const sessions = this.data()?.sessions ?? [];
    const activities = this.activitiesByDay();
    return this.calendar().days.map(day => {
      if (!day) return null;

      const start = new Date(day.date + 'T00:00:00');
      const end = new Date(start);
      end.setDate(end.getDate() + 1);
      const from = start.getTime();
      const through = end.getTime();
      const spentBySubject = new Map<number, number>();
      let spentMilliseconds = 0;
      for (const session of sessions) {
        const elapsed = Math.min(session.end, through) - Math.max(session.start, from);
        if (elapsed <= 0) continue;
        spentMilliseconds += elapsed;
        spentBySubject.set(session.subjectId, (spentBySubject.get(session.subjectId) ?? 0) + elapsed);
      }
      const plannedMinutes = activities.get(day.date)?.minutes ?? 0;
      return {
        ...day,
        plannedMinutes,
        spentMilliseconds,
        spentBySubject,
        plannedPercent: plannedMinutes / dailyActiveMinutes * 100,
        spentPercent: spentMilliseconds / (dailyActiveMinutes * 60_000) * 100
      };
    });
  });
  private readonly selectedCard = computed(() => this.cards().find(day => day?.date === this.date()));
  readonly daySpent = computed(() => this.selectedCard()?.spentMilliseconds ?? 0);
  readonly totals = computed(() => {
    const data = this.data();
    if (!data) return [];

    const groups = new Map<number | null, SubjectTime>();
    const groupFor = (id: number | null, name: string) => {
      let group = groups.get(id);
      if (!group) {
        group = { id, name, plannedMinutes: 0, spentMilliseconds: 0 };
        groups.set(id, group);
      }
      return group;
    };
    for (const activity of data.activities) {
      groupFor(activity.subject?.id ?? null, activity.subject?.name ?? 'Без предмета')
        .plannedMinutes += activity.durationMinutes;
    }
    for (const session of data.sessions) {
      groupFor(session.subjectId, data.subjects.get(session.subjectId) ?? 'Предмет №' + session.subjectId)
        .spentMilliseconds += session.end - session.start;
    }

    return Array.from(groups.values()).sort((a, b) => a.name.localeCompare(b.name, 'ru'));
  });
  readonly monthTotal = computed(() => this.totals().reduce((total, row) => ({
    plannedMinutes: total.plannedMinutes + row.plannedMinutes,
    spentMilliseconds: total.spentMilliseconds + row.spentMilliseconds
  }), { plannedMinutes: 0, spentMilliseconds: 0 }));

  constructor() {
    combineLatest([toObservable(this.month), this.refresh.pipe(startWith(undefined))]).pipe(
      switchMap(([month]) => {
        this.loading.set(true);
        this.loadError.set('');
        const { from, through } = monthBounds(month);
        return forkJoin({
          activities: this.planningApi.plannedActivities(from, through),
          sessions: this.sessionsApi.closedSessionTimes(from, through),
          subjects: this.subjectsApi.subjects()
        }).pipe(
          map(({ activities, sessions, subjects }) => ({
            error: '',
            data: {
              month,
              activities,
              subjects: new Map(subjects.map(subject => [subject.id, subject.name])),
              sessions: sessions.map(session => ({
                subjectId: session.subjectId,
                start: Date.parse(session.startTime),
                end: Date.parse(session.endTime)
              }))
            }
          })),
          catchError(error => of({ data: null, error: errorMessage(error) }))
        );
      }),
      takeUntilDestroyed()
    ).subscribe(({ data, error }) => {
      this.monthData.set(data);
      this.loadError.set(error);
      this.loading.set(false);
    });
  }

  openJournal(id: number, name: string, period: 'day' | 'month') {
    this.journal.set({ subject: { id, name }, date: this.date(), period });
  }

  changeMonth(offset: number) {
    const target = new Date(this.month() + '-01');
    target.setUTCMonth(target.getUTCMonth() + offset);
    this.selectMonth(target.getUTCFullYear(), target.getUTCMonth() + 1);
  }

  selectMonth(year: number, month: number) {
    if (!Number.isInteger(year) || year < 1 || year > 9999) return;
    this.date.set(String(year).padStart(4, '0') + '-' + String(month).padStart(2, '0') + '-01');
  }

  formatSpent(milliseconds: number) {
    return milliseconds > 0 && milliseconds < 60_000
      ? '< 1 мин'
      : formatDuration(Math.floor(milliseconds / 60_000));
  }

  reload() { this.refresh.next(); }
}

