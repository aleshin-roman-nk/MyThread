import type { Route } from '@angular/router';

export const statisticsRoute: Route = { path: 'statistics', loadComponent: () => import('./statistics.component').then(module => module.StatisticsComponent),
  title: 'Статистика и отчёты' };
