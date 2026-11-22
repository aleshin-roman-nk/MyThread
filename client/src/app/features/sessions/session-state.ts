import { Injectable, signal } from '@angular/core';
import { defer, tap } from 'rxjs';
import type { Observable } from 'rxjs';
import type { Session } from './sessions-api';

type SessionScope = 'id' | 'subjectId' | 'projectId' | 'taskId';

@Injectable({ providedIn: 'root' })
export class SessionState {
  private revision = 0;
  private readonly active = signal<Session | null>(null);
  readonly activeSession = this.active.asReadonly();
  readonly sessionToShow = signal<Session | null>(null);

  refreshCurrent(load: () => Observable<Session | null>) {
    return defer(() => {
      const revision = this.revision;
      return load().pipe(tap(session => {
        if (revision === this.revision) this.active.set(session);
      }));
    });
  }

  sessionChanged(session: Session) {
    this.revision++;
    if (!session.endTime) this.active.set(session);
    else if (this.active()?.id === session.id) this.active.set(null);
  }

  clearAfterDeletion(scope: SessionScope, id: number) {
    this.revision++;
    if (this.active()?.[scope] === id) this.active.set(null);
  }
}
