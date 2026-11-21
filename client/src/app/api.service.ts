import { Injectable, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { defer, tap } from 'rxjs';

export interface Subject { id: number; name: string; shortName: string; description: string; noteCount: number; closedSessionHours: number; projectCount: number; inProgressTaskCount: number; deferredTaskCount: number; }
export type ProjectTaskStatus = 0 | 1 | 2;
export const taskStatuses: { value: ProjectTaskStatus; label: string }[] = [
  { value: 0, label: 'В работе' }, { value: 1, label: 'Отложено' }, { value: 2, label: 'Закрыто' }
];
export interface ProjectTask { id: number; projectId: number; name: string; code: string; fullCode: string; body: string; status: ProjectTaskStatus; createdAt: string | null; }
export type ProjectStatus = 0 | 1 | 2;
export const projectStatuses: { value: ProjectStatus; label: string }[] = [
  { value: 0, label: 'В работе' }, { value: 1, label: 'Отложен' }, { value: 2, label: 'Закрыт' }
];
export interface Project { id: number; subjectId: number; name: string; code: string; fullCode: string; tasks: ProjectTask[]; status: ProjectStatus; description: string; createdAt: string | null; }
export interface NoteFields { date: string; time: string; text: string; rating: number; }
export interface Note extends NoteFields { id: number; subjectId: number; subjectName: string; }
export interface SaveNote extends NoteFields { subjectId: number; }
export type SaveDiaryNote = NoteFields;
export interface DiaryNote extends NoteFields { id: number; }
export interface ProjectNote extends NoteFields { id: number; projectId: number; }
export interface TaskNote extends NoteFields { id: number; taskId: number; }
export interface Session { id: number; subjectId: number; projectId: number | null; taskId: number | null; startTime: string; endTime: string | null; status: 0 | 1; text: string; duration: string | null; }
export interface PlannedActivityReference { projectId: number; taskId: number | null; name: string; }
export interface PlannedActivity {
  id: number; date: string; startTime: string; durationMinutes: number; end: string; description: string;
  subject: { id: number; name: string; shortName: string } | null; references: PlannedActivityReference[];
}
export interface SavePlannedActivity {
  date: string; startTime: string; durationMinutes: number; description: string; subjectId: number | null;
  projectIds: number[]; taskIds: number[];
}
export interface DaySession { session: Session; fullCode: string; targetName: string; }
export interface DesktopTask { id: number; projectId: number; subjectId: number; code: string; fullCode: string; name: string; }
export interface DesktopNote extends NoteFields {
  id: number;
  kind: 'diary' | 'subject' | 'project' | 'task';
  source: string;
  subjectId: number | null;
  projectId: number | null;
  taskId: number | null;
}
export interface Sticker { id: number; text: string | null; task: DesktopTask | null; note: DesktopNote | null; x: number | null; y: number | null; }

export function today(date = new Date()): string {
  return [date.getFullYear(), String(date.getMonth() + 1).padStart(2, '0'), String(date.getDate()).padStart(2, '0')].join('-');
}
export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'Нет связи с сервером. Проверьте подключение и повторите попытку.';
    return error.error?.title ?? 'Не удалось выполнить запрос. Повторите попытку.';
  }
  return 'Не удалось выполнить запрос. Повторите попытку.';
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private sessionRevision = 0;
  readonly activeSession = signal<Session | null>(null);
  readonly sessionToShow = signal<Session | null>(null);
  stickers() { return this.http.get<Sticker[]>('/api/desktop/stickers'); }
  saveTextSticker(text: string, id?: number) {
    const url = '/api/desktop/stickers';
    return id === undefined ? this.http.post<Sticker>(url + '/text', { text })
      : this.http.put<Sticker>(url + '/' + id + '/text', { text });
  }
  deleteTextSticker(id: number) { return this.http.delete<void>('/api/desktop/stickers/' + id + '/text'); }
  pinTaskSticker(taskId: number) { return this.http.put<Sticker>('/api/desktop/stickers/tasks/' + taskId, null); }
  unpinTaskSticker(taskId: number) { return this.http.delete<void>('/api/desktop/stickers/tasks/' + taskId); }
  taskStickerStatus(taskId: number) { return this.http.get<{ pinned: boolean }>('/api/desktop/stickers/tasks/' + taskId); }
  pinNoteSticker(noteId: number) { return this.http.put<Sticker>('/api/desktop/stickers/notes/' + noteId, null); }
  unpinNoteSticker(noteId: number) { return this.http.delete<void>('/api/desktop/stickers/notes/' + noteId); }
  noteStickerStatus(noteId: number) { return this.http.get<{ pinned: boolean }>('/api/desktop/stickers/notes/' + noteId); }
  moveSticker(id: number, x: number, y: number) {
    return this.http.put<Sticker>('/api/desktop/stickers/' + id + '/position', { x, y });
  }
  subjects() { return this.http.get<Subject[]>('/api/subjects'); }
  diaryNotes(from: string, through: string) {
    return this.http.get<DiaryNote[]>('/api/diary/notes', { params: { from, through } });
  }
  saveDiaryNote(value: SaveDiaryNote, id?: number) {
    const url = '/api/diary/notes';
    return id === undefined ? this.http.post<DiaryNote>(url, value) : this.http.put<DiaryNote>(url + '/' + id, value);
  }
  deleteDiaryNote(id: number) { return this.http.delete<void>('/api/diary/notes/' + id); }
  plannedActivities(from: string, through: string) {
    return this.http.get<PlannedActivity[]>('/api/planning/activities', { params: { from, through } });
  }
  savePlannedActivity(value: SavePlannedActivity, id?: number) {
    const url = '/api/planning/activities';
    return id === undefined ? this.http.post<PlannedActivity>(url, value)
      : this.http.put<PlannedActivity>(url + '/' + id, value);
  }
  deletePlannedActivity(id: number) { return this.http.delete<void>('/api/planning/activities/' + id); }
  daySessions(date: string) {
    return this.http.get<DaySession[]>('/api/sessions', { params: this.dayBounds(date) });
  }
  usefulWork(date: string) {
    return this.http.get<{ totalSeconds: number }>('/api/sessions/useful-work', { params: this.dayBounds(date) });
  }
  private dayBounds(date: string) {
    const start = new Date(`${date}T00:00:00`);
    const end = new Date(start);
    end.setDate(end.getDate() + 1);
    return { startTime: start.toISOString(), endTime: end.toISOString() };
  }
  deleteSubject(id: number, confirmationShortName: string) {
    return this.http.delete<void>('/api/subjects/' + id, { body: { confirmationShortName } }).pipe(tap(() => {
      this.sessionRevision++;
      if (this.activeSession()?.subjectId === id) this.activeSession.set(null);
    }));
  }
  subject(id: number) { return this.http.get<Subject>('/api/subjects/' + id); }
  subjectSessions(subjectId: number) { return this.http.get<Session[]>('/api/subjects/' + subjectId + '/sessions'); }
  projectSessions(projectId: number) { return this.http.get<Session[]>('/api/projects/' + projectId + '/sessions'); }
  taskSessions(projectId: number, taskId: number) { return this.http.get<Session[]>('/api/projects/' + projectId + '/tasks/' + taskId + '/sessions'); }
  currentSession() {
    return defer(() => {
      const revision = this.sessionRevision;
      return this.http.get<Session | null>('/api/sessions/current').pipe(tap(session => {
        if (revision === this.sessionRevision) this.activeSession.set(session);
      }));
    });
  }
  startSession(subjectId: number, projectId?: number, taskId?: number) {
    return this.http.post<Session>('/api/sessions', { subjectId, projectId, taskId }).pipe(tap(session => this.onSessionChanged(session)));
  }
  closeSession(id: number) {
    return this.http.post<Session>('/api/sessions/' + id + '/close', null).pipe(tap(session => this.onSessionChanged(session)));
  }
  deleteSession(id: number) {
    return this.http.delete<void>('/api/sessions/' + id).pipe(tap(() => {
      this.sessionRevision++;
      if (this.activeSession()?.id === id) this.activeSession.set(null);
    }));
  }
  updateSession(id: number, value: { startTime: string; endTime: string | null; text: string }) {
    return this.http.put<Session>('/api/sessions/' + id, value).pipe(tap(session => this.onSessionChanged(session)));
  }
  private onSessionChanged(session: Session) {
    this.sessionRevision++;
    if (!session.endTime) this.activeSession.set(session);
    else if (this.activeSession()?.id === session.id) this.activeSession.set(null);
  }
  createSubject(name: string, shortName: string, description = '') { return this.http.post<Subject>('/api/subjects', { name, shortName, description }); }
  updateSubject(id: number, name: string, shortName: string, description = '') { return this.http.put<Subject>('/api/subjects/' + id, { name, shortName, description }); }
  projects(subjectId: number, status?: ProjectStatus) {
    const params = status === undefined ? undefined : { status };
    return this.http.get<Project[]>('/api/subjects/' + subjectId + '/projects', { params });
  }
  project(id: number) { return this.http.get<Project>('/api/projects/' + id); }
  deleteProject(id: number, confirmationCode: string) {
    return this.http.delete<void>('/api/projects/' + id, { body: { confirmationCode } }).pipe(tap(() => {
      this.sessionRevision++;
      if (this.activeSession()?.projectId === id) this.activeSession.set(null);
    }));
  }
  deleteTask(projectId: number, id: number, confirmationCode: string) {
    return this.http.delete<void>('/api/projects/' + projectId + '/tasks/' + id, { body: { confirmationCode } }).pipe(tap(() => {
      this.sessionRevision++;
      if (this.activeSession()?.taskId === id) this.activeSession.set(null);
    }));
  }
  projectNotes(projectId: number) { return this.http.get<ProjectNote[]>('/api/projects/' + projectId + '/notes'); }
  taskNotes(projectId: number, taskId: number) {
    return this.http.get<TaskNote[]>('/api/projects/' + projectId + '/tasks/' + taskId + '/notes');
  }
  saveTaskNote(projectId: number, taskId: number, value: NoteFields, noteId?: number) {
    const url = '/api/projects/' + projectId + '/tasks/' + taskId + '/notes';
    return noteId ? this.http.put<TaskNote>(url + '/' + noteId, value) : this.http.post<TaskNote>(url, value);
  }
  deleteTaskNote(projectId: number, taskId: number, noteId: number) {
    return this.http.delete<void>('/api/projects/' + projectId + '/tasks/' + taskId + '/notes/' + noteId);
  }
  saveProjectNote(projectId: number, value: NoteFields, noteId?: number) {
    const url = '/api/projects/' + projectId + '/notes';
    return noteId ? this.http.put<ProjectNote>(url + '/' + noteId, value) : this.http.post<ProjectNote>(url, value);
  }
  deleteProjectNote(projectId: number, noteId: number) {
    return this.http.delete<void>('/api/projects/' + projectId + '/notes/' + noteId);
  }
  updateProject(id: number, name: string, status: ProjectStatus, description: string) {
    return this.http.put<Project>('/api/projects/' + id, { name, status, description });
  }
  setProjectStatus(id: number, status: ProjectStatus) { return this.http.put<Project>('/api/projects/' + id + '/status', { status }); }
  createProject(subjectId: number, name: string, description: string) {
    return this.http.post<Project>('/api/subjects/' + subjectId + '/projects', { name, description });
  }
  createTask(projectId: number, value: { name: string; body: string }) {
    return this.http.post<ProjectTask>('/api/projects/' + projectId + '/tasks', value);
  }
  updateTask(task: ProjectTask, value: { name: string; body: string; status: ProjectTaskStatus }) {
    return this.http.put<ProjectTask>('/api/projects/' + task.projectId + '/tasks/' + task.id, value);
  }
  notes(date: string) { return this.http.get<Note[]>('/api/notes', { params: { date } }); }
  subjectNotes(id: number) { return this.http.get<Note[]>('/api/subjects/' + id + '/notes'); }
  saveNote(value: SaveNote, id?: number) {
    return id
      ? this.http.put<Note>('/api/notes/' + id, value)
      : this.http.post<Note>('/api/notes', value);
  }
  deleteNote(id: number) { return this.http.delete<void>('/api/notes/' + id); }
}

