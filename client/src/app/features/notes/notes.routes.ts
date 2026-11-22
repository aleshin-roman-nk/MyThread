import { inject } from '@angular/core';
import type { ActivatedRouteSnapshot, Route } from '@angular/router';
import { NotesApi } from './notes-api';
import { ProjectsApi } from '../projects/projects-api';
import { SubjectsApi } from '../subjects/subjects-api';
import { loadSubjectNotesPage, loadProjectNotesPage, loadTaskNotesPage } from './note-pages';

export const subjectNotesRoute: Route = { path: 'subjects/:id/notes',
  loadComponent: () => import('./subject-notes.component').then(module => module.SubjectNotesComponent),
  title: 'Заметки предмета',
  resolve: { page: (route: ActivatedRouteSnapshot) => loadSubjectNotesPage(inject(NotesApi), inject(SubjectsApi), Number(route.paramMap.get('id'))) } };

export const projectNotesRoute: Route = { path: 'projects/:id/notes',
  loadComponent: () => import('./project-notes.component').then(module => module.ProjectNotesComponent),
  title: 'Заметки проекта',
  resolve: { page: (route: ActivatedRouteSnapshot) => loadProjectNotesPage(inject(NotesApi), inject(ProjectsApi), inject(SubjectsApi), Number(route.paramMap.get('id'))) } };

export const taskNotesRoute: Route = { path: 'projects/:projectId/tasks/:taskId/notes',
  loadComponent: () => import('./task-notes.component').then(module => module.TaskNotesComponent),
  title: 'Заметки задачи',
  resolve: { page: (route: ActivatedRouteSnapshot) => loadTaskNotesPage(inject(NotesApi), inject(ProjectsApi), inject(SubjectsApi), Number(route.paramMap.get('projectId')), Number(route.paramMap.get('taskId'))) } };
