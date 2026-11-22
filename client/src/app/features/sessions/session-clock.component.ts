import { DatePipe } from '@angular/common';
import { Component } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { map, timer } from 'rxjs';

@Component({
  selector: 'app-session-clock',
  imports: [DatePipe],
  template: `<time [attr.datetime]="now().toISOString()">{{ now() | date:'HH' }}<span class="clock-colon">:</span>{{ now() | date:'mm' }}</time>`,
  styles: `
    time { color: inherit; white-space: nowrap; font-variant-numeric: tabular-nums; }
    .clock-colon { animation: clock-blink 2s step-end infinite; }
    @keyframes clock-blink { 50% { opacity: 0; } }
    @media (prefers-reduced-motion: reduce) { .clock-colon { animation: none; } }
  `
})
export class SessionClockComponent {
  readonly now = toSignal(timer(0, 1000).pipe(map(() => new Date())), { initialValue: new Date() });
}
