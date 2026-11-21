import { Injectable, signal } from '@angular/core';
import { Observable } from 'rxjs';

interface ConfirmationRequest {
  message: string;
  answer: (confirmed: boolean) => void;
}

@Injectable({ providedIn: 'root' })
export class ConfirmationService {
  private readonly pending = signal<ConfirmationRequest | null>(null);
  readonly request = this.pending.asReadonly();

  ask(message: string): Observable<boolean> {
    return new Observable(subscriber => {
      if (this.pending()) {
        subscriber.next(false);
        subscriber.complete();
        return;
      }

      this.pending.set({
        message,
        answer: confirmed => {
          this.pending.set(null);
          subscriber.next(confirmed);
          subscriber.complete();
        }
      });
      const request = this.pending();
      return () => {
        if (this.pending() === request) this.pending.set(null);
      };
    });
  }
}
