import { catchError, forkJoin, map, of, switchMap } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import type { PageResult } from '../../shared/page-result';
import type { Session, SessionsApi } from '../sessions/sessions-api';
import type { Subject, SubjectsApi } from '../subjects/subjects-api';
import type { Project, ProjectsApi } from './projects-api';

export interface SubjectProjectsPage { subject: Subject; projects: Project[]; }
export interface ProjectPage { subject: Subject; project: Project; }
export interface ProjectSessionsPage extends ProjectPage { sessions: Session[]; }

export function loadSubjectProjectsPage(projectsApi: ProjectsApi, subjectsApi: SubjectsApi, id: number) {
  return forkJoin({ subject: subjectsApi.subject(id), projects: projectsApi.projects(id) }).pipe(
    map(data => ({ data, error: '' } as PageResult<SubjectProjectsPage>)),
    catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<SubjectProjectsPage>))
  );
}

export function loadProjectPage(projectsApi: ProjectsApi, subjectsApi: SubjectsApi, id: number) {
  return projectsApi.project(id).pipe(
    switchMap(project => subjectsApi.subject(project.subjectId).pipe(map(subject => ({ project, subject })))),
    map(data => ({ data, error: '' } as PageResult<ProjectPage>)),
    catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<ProjectPage>))
  );
}

export function loadProjectSessionsPage(projectsApi: ProjectsApi, subjectsApi: SubjectsApi, sessionsApi: SessionsApi, id: number) {
  return forkJoin({
    page: loadProjectPage(projectsApi, subjectsApi, id), sessions: sessionsApi.projectSessions(id), current: sessionsApi.currentSession()
  }).pipe(
    map(result => ({ data: result.page.data ? { ...result.page.data, sessions: result.sessions } : null, error: result.page.error } as PageResult<ProjectSessionsPage>)),
    catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<ProjectSessionsPage>))
  );
}
