import { catchError, forkJoin, map, of, switchMap } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import type { PageResult } from '../../shared/page-result';
import { loadProjectPage, type ProjectPage } from '../projects/project-pages';
import type { ProjectsApi } from '../projects/projects-api';
import type { Subject, SubjectsApi } from '../subjects/subjects-api';
import type { ProjectTask } from '../tasks/tasks-api';
import type { Note, NotesApi, ProjectNote, TaskNote } from './notes-api';

export interface SubjectNotesPage { subject: Subject; subjects: Subject[]; notes: Note[]; }
export interface ProjectNotesPage extends ProjectPage { notes: ProjectNote[]; }
export interface TaskNotesPage extends ProjectPage { task: ProjectTask; notes: TaskNote[]; }

export function loadSubjectNotesPage(notesApi: NotesApi, subjectsApi: SubjectsApi, id: number) {
  return forkJoin({ subject: subjectsApi.subject(id), subjects: subjectsApi.subjects(), notes: notesApi.subjectNotes(id) }).pipe(
    map(data => ({ data, error: '' } as PageResult<SubjectNotesPage>)),
    catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<SubjectNotesPage>))
  );
}

export function loadProjectNotesPage(notesApi: NotesApi, projectsApi: ProjectsApi, subjectsApi: SubjectsApi, id: number) {
  return forkJoin({ page: loadProjectPage(projectsApi, subjectsApi, id), notes: notesApi.projectNotes(id) }).pipe(
    map(result => ({ data: result.page.data ? { ...result.page.data, notes: result.notes } : null, error: result.page.error } as PageResult<ProjectNotesPage>)),
    catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<ProjectNotesPage>))
  );
}

export function loadTaskNotesPage(notesApi: NotesApi, projectsApi: ProjectsApi, subjectsApi: SubjectsApi, projectId: number, taskId: number) {
  return loadProjectPage(projectsApi, subjectsApi, projectId).pipe(switchMap(result => {
    if (!result.data) return of({ data: null, error: result.error } as PageResult<TaskNotesPage>);
    const task = result.data.project.tasks.find(task => task.id === taskId);
    if (!task) return of({ data: null, error: 'Задача не найдена.' } as PageResult<TaskNotesPage>);
    const page = result.data;
    return notesApi.taskNotes(projectId, taskId).pipe(
      map(notes => ({ data: { ...page, task, notes }, error: '' } as PageResult<TaskNotesPage>))
    );
  }), catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<TaskNotesPage>)));
}
