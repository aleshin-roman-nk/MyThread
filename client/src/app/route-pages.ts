import { catchError, forkJoin, map, of, switchMap } from 'rxjs';
import { ApiService, errorMessage, Note, Project, ProjectNote, ProjectTask, Session, Subject, TaskNote } from './api.service';

export interface PageResult<T> { data: T | null; error: string; }
export interface SubjectPage { subject: Subject; projects: Project[]; sessions: Session[]; currentSession: Session | null; }
export interface SubjectNotesPage { subject: Subject; subjects: Subject[]; notes: Note[]; }
export interface SubjectProjectsPage { subject: Subject; projects: Project[]; }
export interface ProjectPage { subject: Subject; project: Project; }
export interface ProjectNotesPage extends ProjectPage { notes: ProjectNote[]; }
export interface TaskNotesPage extends ProjectPage { task: ProjectTask; notes: TaskNote[]; }
export interface ProjectTasksPage extends ProjectPage { tasks: ProjectTask[]; }
export interface ProjectSessionsPage extends ProjectPage { sessions: Session[]; }
export interface TaskPage extends ProjectPage { task: ProjectTask; sessions: Session[]; notes: PageResult<TaskNote[]>; }

export function loadSubjectPage(api: ApiService, id: number) {
  return forkJoin({ subject: api.subject(id), projects: api.projects(id), sessions: api.subjectSessions(id), currentSession: api.currentSession() }).pipe(
    map(data => ({ data, error: '' } as PageResult<SubjectPage>)),
    catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<SubjectPage>))
  );
}

export function loadSubjectNotesPage(api: ApiService, id: number) {
  return forkJoin({ subject: api.subject(id), subjects: api.subjects(), notes: api.subjectNotes(id) }).pipe(
    map(data => ({ data, error: '' } as PageResult<SubjectNotesPage>)),
    catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<SubjectNotesPage>))
  );
}

export function loadSubjectProjectsPage(api: ApiService, id: number) {
  return forkJoin({ subject: api.subject(id), projects: api.projects(id) }).pipe(
    map(data => ({ data, error: '' } as PageResult<SubjectProjectsPage>)),
    catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<SubjectProjectsPage>))
  );
}

export function loadProjectPage(api: ApiService, id: number) {
  return api.project(id).pipe(
    switchMap(project => api.subject(project.subjectId).pipe(map(subject => ({ project, subject })))),
    map(data => ({ data, error: '' } as PageResult<ProjectPage>)),
    catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<ProjectPage>))
  );
}

export function loadProjectTasksPage(api: ApiService, id: number) {
  return loadProjectPage(api, id).pipe(map(result => ({
    data: result.data ? { ...result.data, tasks: result.data.project.tasks } : null,
    error: result.error
  } as PageResult<ProjectTasksPage>)));
}

export function loadTaskPage(api: ApiService, projectId: number, taskId: number) {
  return loadProjectPage(api, projectId).pipe(switchMap(result => {
    if (!result.data) return of({ data: null, error: result.error } as PageResult<TaskPage>);
    const task = result.data.project.tasks.find(task => task.id === taskId);
    if (!task) return of({ data: null, error: 'Задача не найдена.' } as PageResult<TaskPage>);
    const page = result.data;
    const notes = api.taskNotes(projectId, taskId).pipe(
      map((data): PageResult<TaskNote[]> => ({ data, error: '' })),
      catchError(error => of({ data: null, error: errorMessage(error) }))
    );
    return forkJoin({ sessions: api.taskSessions(projectId, taskId), current: api.currentSession(), notes }).pipe(
      map(({ sessions, notes }) => ({ data: { ...page, task, sessions, notes }, error: '' } as PageResult<TaskPage>))
    );
  }), catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<TaskPage>)));
}

export function loadProjectSessionsPage(api: ApiService, id: number) {
  return forkJoin({ page: loadProjectPage(api, id), sessions: api.projectSessions(id), current: api.currentSession() }).pipe(
    map(result => ({ data: result.page.data ? { ...result.page.data, sessions: result.sessions } : null, error: result.page.error } as PageResult<ProjectSessionsPage>)),
    catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<ProjectSessionsPage>))
  );
}

export function loadProjectNotesPage(api: ApiService, id: number) {
  return forkJoin({ page: loadProjectPage(api, id), notes: api.projectNotes(id) }).pipe(
    map(result => ({ data: result.page.data ? { ...result.page.data, notes: result.notes } : null, error: result.page.error } as PageResult<ProjectNotesPage>)),
    catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<ProjectNotesPage>))
  );
}

export function loadTaskNotesPage(api: ApiService, projectId: number, taskId: number) {
  return loadProjectPage(api, projectId).pipe(switchMap(result => {
    if (!result.data) return of({ data: null, error: result.error } as PageResult<TaskNotesPage>);
    const task = result.data.project.tasks.find(task => task.id === taskId);
    if (!task) return of({ data: null, error: 'Задача не найдена.' } as PageResult<TaskNotesPage>);
    const page = result.data;
    return api.taskNotes(projectId, taskId).pipe(
      map(notes => ({ data: { ...page, task, notes }, error: '' } as PageResult<TaskNotesPage>))
    );
  }), catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<TaskNotesPage>)));
}
