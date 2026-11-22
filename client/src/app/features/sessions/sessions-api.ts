import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { tap } from 'rxjs';
import { SessionState } from './session-state';

export interface Session {
  id: number; subjectId: number; projectId: number | null; taskId: number | null;
  startTime: string; endTime: string | null; status: 0 | 1; text: string; duration: string | null;
}
export interface ClosedSessionTime { subjectId: number; startTime: string; endTime: string; }
export interface DaySession { session: Session; fullCode: string; targetName: string; }

const sessionsUrl = '/api/sessions';

@Injectable({ providedIn: 'root' })
export class SessionsApi {
  private readonly http = inject(HttpClient);
  private readonly state = inject(SessionState);

  daySessions(date: string) {
    return this.http.get<DaySession[]>(sessionsUrl, { params: this.dayBounds(date) });
  }

  subjectSessionJournal(subjectId: number, from: string, through = from) {
    return this.http.get<DaySession[]>('/api/subjects/' + subjectId + '/sessions/journal', { params: this.dayBounds(from, through) });
  }

  closedSessionTimes(from: string, through: string) {
    return this.http.get<ClosedSessionTime[]>(sessionsUrl + '/closed-times', { params: this.dayBounds(from, through) });
  }

  usefulWork(date: string) {
    return this.http.get<{ totalSeconds: number }>(sessionsUrl + '/useful-work', { params: this.dayBounds(date) });
  }

  subjectSessions(subjectId: number) {
    return this.http.get<Session[]>('/api/subjects/' + subjectId + '/sessions');
  }

  projectSessions(projectId: number) {
    return this.http.get<Session[]>('/api/projects/' + projectId + '/sessions');
  }

  taskSessions(projectId: number, taskId: number) {
    return this.http.get<Session[]>('/api/projects/' + projectId + '/tasks/' + taskId + '/sessions');
  }

  currentSession() {
    return this.state.refreshCurrent(() => this.http.get<Session | null>(sessionsUrl + '/current'));
  }

  startSession(subjectId: number, projectId?: number, taskId?: number) {
    return this.http.post<Session>(sessionsUrl, { subjectId, projectId, taskId })
      .pipe(tap(session => this.state.sessionChanged(session)));
  }

  closeSession(id: number) {
    return this.http.post<Session>(sessionsUrl + '/' + id + '/close', null)
      .pipe(tap(session => this.state.sessionChanged(session)));
  }

  deleteSession(id: number) {
    return this.http.delete<void>(sessionsUrl + '/' + id)
      .pipe(tap(() => this.state.clearAfterDeletion('id', id)));
  }

  updateSession(id: number, value: { startTime: string; endTime: string | null; text: string }) {
    return this.http.put<Session>(sessionsUrl + '/' + id, value)
      .pipe(tap(session => this.state.sessionChanged(session)));
  }

  private dayBounds(from: string, through = from) {
    const start = new Date(from + 'T00:00:00');
    const end = new Date(through + 'T00:00:00');
    end.setDate(end.getDate() + 1);
    const startTime = start.getUTCFullYear() < 1 ? '0001-01-01T00:00:00Z' : start.toISOString();
    const endTime = end.getUTCFullYear() > 9999 ? '9999-12-31T23:59:59.9999999Z' : end.toISOString();
    return { startTime, endTime };
  }
}
