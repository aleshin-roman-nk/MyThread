import { catchError, forkJoin, map, of } from 'rxjs';
import { errorMessage } from '../../shared/error-message';
import type { PageResult } from '../../shared/page-result';
import type { Project, ProjectsApi } from '../projects/projects-api';
import type { Session, SessionsApi } from '../sessions/sessions-api';
import type { Subject, SubjectsApi } from './subjects-api';

export interface SubjectPage { subject: Subject; projects: Project[]; sessions: Session[]; currentSession: Session | null; }

export function loadSubjectPage(projectsApi: ProjectsApi, subjectsApi: SubjectsApi, sessionsApi: SessionsApi, id: number) {
  return forkJoin({
    subject: subjectsApi.subject(id), projects: projectsApi.projects(id),
    sessions: sessionsApi.subjectSessions(id), currentSession: sessionsApi.currentSession()
  }).pipe(
    map(data => ({ data, error: '' } as PageResult<SubjectPage>)),
    catchError(error => of({ data: null, error: errorMessage(error) } as PageResult<SubjectPage>))
  );
}
