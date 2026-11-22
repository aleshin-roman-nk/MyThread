import { inject } from '@angular/core';
import type { ActivatedRouteSnapshot, Route } from '@angular/router';
import { NotesApi } from '../notes/notes-api';
import { ProjectsApi } from '../projects/projects-api';
import { SessionsApi } from '../sessions/sessions-api';
import { SubjectsApi } from '../subjects/subjects-api';
import { loadProjectTasksPage, loadTaskPage } from './task-pages';

export const projectTasksRoute: Route = { path: 'projects/:id/tasks',
  loadComponent: () => import('./tasks.component').then(module => module.TasksComponent),
  title: 'Задачи проекта',
  resolve: { page: (route: ActivatedRouteSnapshot) => loadProjectTasksPage(inject(ProjectsApi), inject(SubjectsApi), Number(route.paramMap.get('id'))) } };

export const taskRoute: Route = { path: 'projects/:projectId/tasks/:taskId',
  loadComponent: () => import('./task.component').then(module => module.TaskComponent),
  title: 'Задача',
  resolve: { page: (route: ActivatedRouteSnapshot) => loadTaskPage(inject(NotesApi), inject(ProjectsApi), inject(SubjectsApi), inject(SessionsApi),
    Number(route.paramMap.get('projectId')), Number(route.paramMap.get('taskId'))) } };
