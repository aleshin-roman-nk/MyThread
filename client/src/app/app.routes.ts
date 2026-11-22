import type { Routes } from '@angular/router';
import { dayRoute } from './features/day/day.routes';
import { diaryRoute } from './features/diary/diary.routes';
import { planningRoute } from './features/planning/planning.routes';
import { statisticsRoute } from './features/statistics/statistics.routes';
import { desktopRoute } from './features/desktop/desktop.routes';
import { subjectsRoute, subjectRoute } from './features/subjects/subjects.routes';
import { subjectNotesRoute, projectNotesRoute, taskNotesRoute } from './features/notes/notes.routes';
import { subjectProjectsRoute, projectRoute } from './features/projects/projects.routes';
import { projectTasksRoute, taskRoute } from './features/tasks/tasks.routes';

export const routes: Routes = [
  dayRoute,
  diaryRoute,
  planningRoute,
  statisticsRoute,
  desktopRoute,
  subjectsRoute,
  subjectNotesRoute,
  subjectProjectsRoute,
  subjectRoute,
  projectNotesRoute,
  projectTasksRoute,
  projectRoute,
  taskNotesRoute,
  taskRoute,
  { path: '**', redirectTo: '' }
];
