import type { Route } from '@angular/router';

export const desktopRoute: Route = {
  path: 'desktop',
  loadComponent: () => import('./desktop.component').then(module => module.DesktopComponent),
  title: 'Рабочий стол'
};
