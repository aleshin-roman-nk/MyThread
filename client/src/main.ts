import { bootstrapApplication } from '@angular/platform-browser';
import { inject } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { ActivatedRouteSnapshot, provideRouter } from '@angular/router';
import { AppComponent } from './app/app.component';
import { DayComponent } from './app/day.component';
import { DiaryComponent } from './app/diary.component';
import { DesktopComponent } from './app/desktop.component';
import { SubjectsComponent } from './app/subjects.component';
import { SubjectComponent } from './app/subject.component';
import { SubjectNotesComponent } from './app/subject-notes.component';
import { ProjectComponent } from './app/project.component';
import { ProjectsComponent } from './app/projects.component';
import { ProjectNotesComponent } from './app/project-notes.component';
import { TaskComponent } from './app/task.component';
import { TasksComponent } from './app/tasks.component';
import { TaskNotesComponent } from './app/task-notes.component';
import { ApiService } from './app/api.service';
import { loadProjectNotesPage, loadProjectSessionsPage, loadProjectTasksPage, loadSubjectNotesPage, loadSubjectPage, loadSubjectProjectsPage, loadTaskNotesPage, loadTaskPage } from './app/route-pages';

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(),
    provideRouter([
      { path: '', component: DayComponent, title: 'Мой день' },
      { path: 'diary', component: DiaryComponent, title: 'Дневник' },
      { path: 'planning', loadComponent: () => import('./app/planning.component').then(module => module.PlanningComponent),
        title: 'Планирование' },
      { path: 'desktop', component: DesktopComponent, title: 'Рабочий стол' },
      { path: 'subjects', component: SubjectsComponent, title: 'Предметы' },
      { path: 'subjects/:id/notes', component: SubjectNotesComponent, title: 'Заметки предмета',
        resolve: { page: (route: ActivatedRouteSnapshot) => loadSubjectNotesPage(inject(ApiService), Number(route.paramMap.get('id'))) } },
      { path: 'subjects/:id/projects', component: ProjectsComponent, title: 'Проекты предмета',
        resolve: { page: (route: ActivatedRouteSnapshot) => loadSubjectProjectsPage(inject(ApiService), Number(route.paramMap.get('id'))) } },
      { path: 'subjects/:id', component: SubjectComponent, title: 'Предмет',
        resolve: { page: (route: ActivatedRouteSnapshot) => loadSubjectPage(inject(ApiService), Number(route.paramMap.get('id'))) } },
      { path: 'projects/:id/notes', component: ProjectNotesComponent, title: 'Заметки проекта',
        resolve: { page: (route: ActivatedRouteSnapshot) => loadProjectNotesPage(inject(ApiService), Number(route.paramMap.get('id'))) } },
      { path: 'projects/:id/tasks', component: TasksComponent, title: 'Задачи проекта',
        resolve: { page: (route: ActivatedRouteSnapshot) => loadProjectTasksPage(inject(ApiService), Number(route.paramMap.get('id'))) } },
      { path: 'projects/:id', component: ProjectComponent, title: 'Проект',
        resolve: { page: (route: ActivatedRouteSnapshot) => loadProjectSessionsPage(inject(ApiService), Number(route.paramMap.get('id'))) } },
      { path: 'projects/:projectId/tasks/:taskId/notes', component: TaskNotesComponent, title: 'Заметки задачи',
        resolve: { page: (route: ActivatedRouteSnapshot) => loadTaskNotesPage(inject(ApiService), Number(route.paramMap.get('projectId')), Number(route.paramMap.get('taskId'))) } },
      { path: 'projects/:projectId/tasks/:taskId', component: TaskComponent, title: 'Задача',
        resolve: { page: (route: ActivatedRouteSnapshot) => loadTaskPage(inject(ApiService), Number(route.paramMap.get('projectId')), Number(route.paramMap.get('taskId'))) } },
      { path: '**', redirectTo: '' }
    ])
  ]
}).catch(console.error);

