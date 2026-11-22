import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { NoteFields } from '../notes/notes-api';

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

@Injectable({ providedIn: 'root' })
export class DesktopApi {
  private readonly http = inject(HttpClient);
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
}
