import type { Route } from '@angular/router';

export const diaryRoute: Route = {
  path: 'diary',
  loadComponent: () => import('./diary.component').then(module => module.DiaryComponent),
  title: 'Дневник'
};
