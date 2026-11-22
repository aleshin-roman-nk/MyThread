import { catchError, forkJoin, map, of, switchMap } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import type { PageResult } from '../../shared/page-result';
import type { NotesApi, TaskNote } from '../notes/notes-api';
import { loadProjectPage, type ProjectPage } from '../projects/project-pages';
import type { ProjectsApi } from '../projects/projects-api';
import type { Session, SessionsApi } from '../sessions/sessions-api';
import type { SubjectsApi } from '../subjects/subjects-api';
import type { ProjectTask } from './tasks-api';

export interface ProjectTasksPage extends ProjectPage { tasks: ProjectTask[]; }
export interface TaskPage extends ProjectPage { task: ProjectTask; sessions: Session[]; notes: PageResult<TaskNote[]>; }

export function loadProjectTasksPage(projectsApi: ProjectsApi, subjectsApi: SubjectsApi, id: number) {
  return loadProjectPage(projectsApi, subjectsApi, id).pipe(map(result => ({
    data: result.data ? { ...result.data, tasks: result.data.project.tasks } : null,
    error: result.error
  } as PageResult<ProjectTasksPage>)));
}

export function loadTaskPage(notesApi: NotesApi, projectsApi: ProjectsApi, subjectsApi: SubjectsApi, sessionsApi: SessionsApi, projectId: number, taskId: number) {
  return loadProjectPage(projectsApi, subjectsApi, projectId).pipe(switchMap(result => {
    if (!result.data) return of({ data: null, error: result.error } as PageResult<TaskPage>);
    const task = result.data.project.tasks.find(task => task.id === taskId);
    if (!task) return of({ data: null, error: 'Задача не найдена.' } as PageResult<TaskPage>);
    const page = result.data;
    const notes = notesApi.taskNotes(projectId, taskId).pipe(
      map((data): PageResult<TaskNote[]> => ({ data, error: '' })),
      catchError(error => of({ data: null, error: errorMessage(error) }))
    );
    return forkJoin({ sessions: sessionsApi.taskSessions(projectId, taskId), current: sessionsApi.currentSession(), notes }).pipe(
      map(({ sessions, notes }) => ({ data: { ...page, task, sessions, notes }, error: '' } as PageResult<TaskPage>))
    );
  }), catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<TaskPage>)));
}
