import { inject } from '@angular/core';
import type { ActivatedRouteSnapshot, Route } from '@angular/router';
import { ProjectsApi } from '../projects/projects-api';
import { SessionsApi } from '../sessions/sessions-api';
import { SubjectsApi } from './subjects-api';
import { loadSubjectPage } from './subject-page';

export const subjectsRoute: Route = { path: 'subjects',
  loadComponent: () => import('./subjects.component').then(module => module.SubjectsComponent),
  title: 'Предметы' };

export const subjectRoute: Route = { path: 'subjects/:id',
  loadComponent: () => import('./subject.component').then(module => module.SubjectComponent),
  title: 'Предмет',
  resolve: { page: (route: ActivatedRouteSnapshot) => loadSubjectPage(inject(ProjectsApi), inject(SubjectsApi), inject(SessionsApi),
    Number(route.paramMap.get('id'))) } };
