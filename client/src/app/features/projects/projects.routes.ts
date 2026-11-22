import { inject } from '@angular/core';
import type { ActivatedRouteSnapshot, Route } from '@angular/router';
import { ProjectsApi } from './projects-api';
import { SessionsApi } from '../sessions/sessions-api';
import { SubjectsApi } from '../subjects/subjects-api';
import { loadSubjectProjectsPage, loadProjectSessionsPage } from './project-pages';

export const subjectProjectsRoute: Route = { path: 'subjects/:id/projects',
  loadComponent: () => import('./projects.component').then(module => module.ProjectsComponent),
  title: 'Проекты предмета',
  resolve: { page: (route: ActivatedRouteSnapshot) => loadSubjectProjectsPage(inject(ProjectsApi), inject(SubjectsApi), Number(route.paramMap.get('id'))) } };

export const projectRoute: Route = { path: 'projects/:id',
  loadComponent: () => import('./project.component').then(module => module.ProjectComponent),
  title: 'Проект',
  resolve: { page: (route: ActivatedRouteSnapshot) => loadProjectSessionsPage(inject(ProjectsApi), inject(SubjectsApi), inject(SessionsApi),
    Number(route.paramMap.get('id'))) } };
