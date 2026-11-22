import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { NoteFields } from '../notes/notes-api';

export type SaveDiaryNote = NoteFields;
export interface DiaryNote extends NoteFields { id: number; }

const diaryNotesUrl = '/api/diary/notes';

@Injectable({ providedIn: 'root' })
export class DiaryApi {
  private readonly http = inject(HttpClient);

  diaryNotes(from: string, through: string) {
    return this.http.get<DiaryNote[]>(diaryNotesUrl, { params: { from, through } });
  }

  saveDiaryNote(value: SaveDiaryNote, id?: number) {
    return id === undefined ? this.http.post<DiaryNote>(diaryNotesUrl, value)
      : this.http.put<DiaryNote>(diaryNotesUrl + '/' + id, value);
  }

  deleteDiaryNote(id: number) {
    return this.http.delete<void>(diaryNotesUrl + '/' + id);
  }
}
