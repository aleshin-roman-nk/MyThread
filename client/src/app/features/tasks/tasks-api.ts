import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { tap } from 'rxjs';
import { SessionState } from '../sessions/session-state';

export type ProjectTaskStatus = 0 | 1 | 2;
export const taskStatuses: { value: ProjectTaskStatus; label: string }[] = [
  { value: 0, label: 'В работе' }, { value: 1, label: 'Отложено' }, { value: 2, label: 'Закрыто' }
];
export interface ProjectTask { id: number; projectId: number; name: string; code: string; fullCode: string; body: string; status: ProjectTaskStatus; createdAt: string | null; }

@Injectable({ providedIn: 'root' })
export class TasksApi {
  private readonly http = inject(HttpClient);
  private readonly sessionState = inject(SessionState);

  createTask(projectId: number, value: { name: string; body: string }) {
    return this.http.post<ProjectTask>('/api/projects/' + projectId + '/tasks', value);
  }
  updateTask(task: ProjectTask, value: { name: string; body: string; status: ProjectTaskStatus }) {
    return this.http.put<ProjectTask>('/api/projects/' + task.projectId + '/tasks/' + task.id, value);
  }

  deleteTask(projectId: number, id: number, confirmationCode: string) {
    return this.http.delete<void>('/api/projects/' + projectId + '/tasks/' + id, { body: { confirmationCode } })
      .pipe(tap(() => this.sessionState.clearAfterDeletion('taskId', id)));
  }
}
