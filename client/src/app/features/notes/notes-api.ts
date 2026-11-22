import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

export interface NoteFields { date: string; time: string; text: string; rating: number; }
export interface Note extends NoteFields { id: number; subjectId: number; subjectName: string; }
export interface SaveNote extends NoteFields { subjectId: number; }
export interface ProjectNote extends NoteFields { id: number; projectId: number; }
export interface TaskNote extends NoteFields { id: number; taskId: number; }

@Injectable({ providedIn: 'root' })
export class NotesApi {
  private readonly http = inject(HttpClient);

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
  notes(date: string) { return this.http.get<Note[]>('/api/notes', { params: { date } }); }
  subjectNotes(id: number) { return this.http.get<Note[]>('/api/subjects/' + id + '/notes'); }
  saveNote(value: SaveNote, id?: number) {
    return id
      ? this.http.put<Note>('/api/notes/' + id, value)
      : this.http.post<Note>('/api/notes', value);
  }
  deleteNote(id: number) { return this.http.delete<void>('/api/notes/' + id); }
}
