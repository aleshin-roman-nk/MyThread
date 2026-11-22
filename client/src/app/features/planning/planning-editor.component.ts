import {
  afterNextRender, Component, computed, DestroyRef, inject, input, linkedSignal, OnInit, output, signal, viewChild, ElementRef
} from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, combineLatest, map, of, startWith, Subject as Refresh, switchMap } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import { Project, ProjectsApi } from '../projects/projects-api';
import { Subject, SubjectsApi } from '../subjects/subjects-api';
import { DateInputComponent } from '../../shared/date-input.component';
import { TimeInputComponent } from '../../shared/time-input.component';
import { isClockTime, readTimeInput } from '../../shared/time-format';
import { PlannedActivity, PlannedActivityReference, PlanningApi } from './planning-api';
import { formatDuration } from '../../shared/calendar';

@Component({
  selector: 'app-planning-editor',
  imports: [FormsModule, DateInputComponent, TimeInputComponent],
  templateUrl: './planning-editor.component.html',
  styleUrl: './planning-editor.component.scss'
})
export class PlanningEditorComponent implements OnInit {
  private readonly projectsApi = inject(ProjectsApi);
  private readonly subjectsApi = inject(SubjectsApi);
  private readonly planningApi = inject(PlanningApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly refreshProjects = new Refresh<void>();
  readonly date = input.required<string>();
  readonly initialStartTime = input.required<string>();
  readonly activity = input<PlannedActivity>();
  readonly initialSubjects = input<Subject[]>();
  readonly saved = output<PlannedActivity>();
  readonly deleted = output<void>();
  readonly cancelled = output<void>();

  readonly draftDate = linkedSignal(() => this.activity()?.date ?? this.date());
  readonly startTime = linkedSignal(() => this.initialStartTime());
  readonly duration = linkedSignal(() => formatDuration(this.activity()?.durationMinutes ?? 60));
  readonly description = linkedSignal(() => this.activity()?.description ?? '');
  readonly subjectId = linkedSignal(() => this.activity()?.subject?.id ?? null);
  readonly references = linkedSignal(() => this.activity()?.references ?? []);
  readonly subjects = signal<Subject[]>([]);
  readonly subjectsLoading = signal(true);
  readonly subjectsError = signal('');
  readonly projects = signal<Project[]>([]);
  readonly projectsLoading = signal(false);
  readonly projectsError = signal('');
  readonly pickerOpen = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly confirmingDelete = signal(false);
  readonly keys = computed(() => new Set(this.references().map(reference => this.key(reference))));

  readonly durationMinutes = computed(() => {
    const value = /^(\d+):([0-5]\d)$/.exec(this.duration().trim());
    if (!value) return 0;
    const minutes = Number(value[1]) * 60 + Number(value[2]);
    return minutes > 0 && minutes <= 2_147_483_647 ? minutes : 0;
  });

  readonly end = computed(() => {
    const date = this.draftDate();
    const time = this.startTime();
    const minutes = this.durationMinutes();
    if (!/^(?!0000)\d{4}-\d{2}-\d{2}$/.test(date) || !isClockTime(time) || !minutes) return '';
    const end = new Date(Date.parse(date + 'T' + time + 'Z') + minutes * 60_000);
    if (!Number.isFinite(end.getTime()) || end.getUTCFullYear() > 9999) return '';
    const iso = end.toISOString();
    const displayTime = iso.slice(11, 16);
    return iso.slice(0, 10) === date ? displayTime : displayTime + ' · ' + iso.slice(0, 10).split('-').reverse().join('.');
  });

  constructor() {
    afterNextRender(() => this.dialog().nativeElement.showModal());
    combineLatest([toObservable(this.subjectId), this.refreshProjects.pipe(startWith(undefined))]).pipe(
      switchMap(([id]) => {
        this.projectsLoading.set(id !== null);
        this.projectsError.set('');
        this.projects.set([]);
        return id === null ? of({ projects: [], error: '' }) : this.projectsApi.projects(id, 0).pipe(
          map(projects => ({
            projects: projects.map(project => ({ ...project, tasks: project.tasks.filter(task => task.status === 0) })),
            error: ''
          })),
          catchError(error => of({ projects: [], error: errorMessage(error) }))
        );
      }),
      takeUntilDestroyed()
    ).subscribe(({ projects, error }) => {
      this.projects.set(projects);
      this.projectsError.set(error);
      this.projectsLoading.set(false);
    });
  }

  ngOnInit() {
    const subjects = this.initialSubjects();
    if (subjects) {
      this.subjects.set(subjects);
      this.subjectsLoading.set(false);
    } else {
      this.loadSubjects();
    }
  }

  updateDuration(event: Event) {
    this.duration.set(readTimeInput(event, this.duration(), 'duration'));
  }

  loadSubjects() {
    this.subjectsLoading.set(true);
    this.subjectsError.set('');
    this.subjectsApi.subjects().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: subjects => {
        this.subjects.set(subjects);
        this.subjectsLoading.set(false);
      },
      error: error => {
        this.subjectsError.set(errorMessage(error));
        this.subjectsLoading.set(false);
      }
    });
  }

  changeSubject(id: number | null) {
    if (id === this.subjectId()) return;
    this.pickerOpen.set(false);
    this.references.set([]);
    this.subjectId.set(id);
  }

  retryProjects() { this.refreshProjects.next(); }

  key(reference: PlannedActivityReference) {
    return reference.taskId === null ? 'project-' + reference.projectId : 'task-' + reference.taskId;
  }

  addReference(reference: PlannedActivityReference) {
    if (this.subjectId() !== null && !this.keys().has(this.key(reference))) {
      this.references.update(references => [...references, reference]);
    }
  }

  removeReference(reference: PlannedActivityReference) {
    this.references.update(references => references.filter(item => this.key(item) !== this.key(reference)));
  }

  save() {
    if (this.busy() || this.confirmingDelete()) return;
    this.error.set('');
    if (!isClockTime(this.startTime())) {
      this.error.set('Укажите время начала в 24-часовом формате ЧЧ:ММ, например 18:30.');
      return;
    }
    if (!this.end()) {
      this.error.set('Укажите дату, время начала и длительность больше нуля в формате ЧЧ:ММ.');
      return;
    }
    if (!this.activity() && this.subjectId() === null) {
      this.error.set('Выберите предмет для нового занятия.');
      return;
    }
    if (this.subjectId() === null && !this.description().trim()) {
      this.error.set('Укажите предмет или опишите занятие.');
      return;
    }
    const references = this.references();
    this.busy.set(true);
    this.planningApi.savePlannedActivity({
      date: this.draftDate(),
      startTime: this.startTime() + ':00',
      durationMinutes: this.durationMinutes(),
      description: this.description(),
      subjectId: this.subjectId(),
      projectIds: references.filter(reference => reference.taskId === null).map(reference => reference.projectId),
      taskIds: references.flatMap(reference => reference.taskId === null ? [] : [reference.taskId])
    }, this.activity()?.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: activity => this.saved.emit(activity),
      error: error => this.onError(error)
    });
  }

  deleteActivity() {
    const activity = this.activity();
    if (!activity || this.busy() || !this.confirmingDelete()) return;
    this.error.set('');
    this.busy.set(true);
    this.planningApi.deletePlannedActivity(activity.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.deleted.emit(),
      error: error => this.onError(error)
    });
  }

  cancel(event?: Event) {
    event?.preventDefault();
    if (!this.busy()) this.cancelled.emit();
  }

  private onError(error: unknown) {
    this.error.set(errorMessage(error));
    this.busy.set(false);
  }
}
