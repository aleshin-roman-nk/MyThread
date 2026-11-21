import { Component, computed, inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, EMPTY, filter, fromEvent, merge, of, switchMap } from 'rxjs';
import { ApiService } from './api.service';
import { ConfirmationDialogComponent } from './confirmation-dialog.component';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, ConfirmationDialogComponent],
  styles: `
    :host:has(main.desktop-page) > :is(.site-header, footer) { display: none; }
    main.desktop-page { max-width: none; min-height: 0; padding: 0; }
    .site-header nav { flex-wrap: wrap; align-items: center; justify-content: flex-end; }
    .site-header .current-session { display: inline-flex; align-items: center; gap: 8px; background: #e5ebdf; color: #214f46; }
    .session-indicator { width: 8px; height: 8px; border-radius: 50%; background: #487942; flex-shrink: 0; }
  `,
  template: `
    <header class="site-header">
      <a class="brand" routerLink="/"><img class="brand-icon" src="icons/planning-flows-192.png" width="42" height="42" alt="" aria-hidden="true">Мои потоки<span class="brand-caption">Оперативный журнал</span></a>
      <nav aria-label="Основная навигация">
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Мой день</a>
        <a routerLink="/desktop" routerLinkActive="active" #desktopRoute="routerLinkActive" ariaCurrentWhenActive="page">Рабочий стол</a>
        <a routerLink="/subjects" routerLinkActive="active">Предметы</a>
        <a routerLink="/planning" routerLinkActive="active" ariaCurrentWhenActive="page">Планирование</a>
        <a routerLink="/diary" routerLinkActive="active" ariaCurrentWhenActive="page">Мой дневник</a>
        @if (currentSessionLink(); as link) {
          <a class="current-session" [routerLink]="link" (click)="showCurrentSession()" title="Перейти к текущей сессии"><span class="session-indicator" aria-hidden="true"></span>Текущая сессия</a>
        }
      </nav>
    </header>
    <main [class.desktop-page]="desktopRoute.isActive"><router-outlet /></main>
    <footer>Небольшие записи. Большой прогресс.</footer>
    <app-confirmation-dialog />
  `
})
export class AppComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  readonly currentSessionLink = computed(() => {
    const session = this.api.activeSession();
    if (!session) return null;
    if (session.taskId !== null && session.projectId !== null) return ['/projects', session.projectId, 'tasks', session.taskId];
    if (session.projectId !== null) return ['/projects', session.projectId];
    return ['/subjects', session.subjectId];
  });

  showCurrentSession() { this.api.sessionToShow.set(this.api.activeSession()); }

  constructor() {
    merge(of(null), this.router.events.pipe(filter(event => event instanceof NavigationEnd)),
      fromEvent(this.document, 'visibilitychange').pipe(filter(() => this.document.visibilityState === 'visible')))
      .pipe(switchMap(() => this.api.currentSession().pipe(catchError(() => EMPTY))), takeUntilDestroyed())
      .subscribe();
  }
}

