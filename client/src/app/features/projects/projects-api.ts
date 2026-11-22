import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { tap } from 'rxjs';
import type { ProjectTask } from '../tasks/tasks-api';
import { SessionState } from '../sessions/session-state';

export type ProjectStatus = 0 | 1 | 2;
export const projectStatuses: { value: ProjectStatus; label: string }[] = [
  { value: 0, label: 'В работе' }, { value: 1, label: 'Отложен' }, { value: 2, label: 'Закрыт' }
];
export interface Project { id: number; subjectId: number; name: string; code: string; fullCode: string; tasks: ProjectTask[]; status: ProjectStatus; description: string; createdAt: string | null; }

@Injectable({ providedIn: 'root' })
export class ProjectsApi {
  private readonly http = inject(HttpClient);
  private readonly sessionState = inject(SessionState);

  projects(subjectId: number, status?: ProjectStatus) {
    const params = status === undefined ? undefined : { status };
    return this.http.get<Project[]>('/api/subjects/' + subjectId + '/projects', { params });
  }
  project(id: number) { return this.http.get<Project>('/api/projects/' + id); }
  deleteProject(id: number, confirmationCode: string) {
    return this.http.delete<void>('/api/projects/' + id, { body: { confirmationCode } })
      .pipe(tap(() => this.sessionState.clearAfterDeletion('projectId', id)));
  }

  updateProject(id: number, name: string, status: ProjectStatus, description: string) {
    return this.http.put<Project>('/api/projects/' + id, { name, status, description });
  }
  setProjectStatus(id: number, status: ProjectStatus) { return this.http.put<Project>('/api/projects/' + id + '/status', { status }); }
  createProject(subjectId: number, name: string, description: string) {
    return this.http.post<Project>('/api/subjects/' + subjectId + '/projects', { name, description });
  }
}
