import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { tap } from 'rxjs';
import { SessionState } from '../sessions/session-state';

export interface Subject { id: number; name: string; shortName: string; description: string; noteCount: number; closedSessionHours: number; projectCount: number; inProgressTaskCount: number; deferredTaskCount: number; }

@Injectable({ providedIn: 'root' })
export class SubjectsApi {
  private readonly http = inject(HttpClient);
  private readonly sessionState = inject(SessionState);

  subjects() { return this.http.get<Subject[]>('/api/subjects'); }
  deleteSubject(id: number, confirmationShortName: string) {
    return this.http.delete<void>('/api/subjects/' + id, { body: { confirmationShortName } })
      .pipe(tap(() => this.sessionState.clearAfterDeletion('subjectId', id)));
  }
  subject(id: number) { return this.http.get<Subject>('/api/subjects/' + id); }
  createSubject(name: string, shortName: string, description = '') { return this.http.post<Subject>('/api/subjects', { name, shortName, description }); }
  updateSubject(id: number, name: string, shortName: string, description = '') { return this.http.put<Subject>('/api/subjects/' + id, { name, shortName, description }); }
}
