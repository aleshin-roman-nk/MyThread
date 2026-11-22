import type { Route } from '@angular/router';

export const planningRoute: Route = { path: 'planning', loadComponent: () => import('./planning.component').then(module => module.PlanningComponent),
  title: 'Планирование' };
