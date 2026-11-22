import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { errorMessage } from '../../shared/error-message';
import { Subject, SubjectsApi } from './subjects-api';
import { isValidShortCode, ShortCodeDirective } from '../../shared/short-code.directive';

@Component({
  imports: [FormsModule, RouterLink, ShortCodeDirective, DecimalPipe],
  template: `
    <a class="back-link" routerLink="/">← К моему дню</a>
    <div class="page-heading"><div><p class="eyebrow">КТО СМОТРИТ НА ТУЧИ - НИКОГДА НЕ ПОЖНЕТ</p><h1>Предметы</h1><p class="subtitle">Направления деятельности, предметные области жизни</p></div></div>
    <div class="workspace">
      <section aria-label="Список предметов">
        <div class="section-heading"><h2>Мои предметы</h2><span class="count">{{ subjects().length }} всего</span></div>
        @if (error()) { <p class="error" role="alert">{{ error() }} <button class="text-button" (click)="load()">Повторить</button></p> }
        @if (loading()) { <p role="status">Загружаем предметы…</p> }
        @else {
          <div class="subject-grid">
            @for (subject of subjects(); track subject.id) {
              <a class="subject-card" [routerLink]="['/subjects', subject.id]">
                <div class="subject-card-heading">
                  <span class="subject-initial">{{ subject.shortName }}</span>
                  <h2>{{ subject.name }}</h2>
                </div>
                <div class="subject-stats">
                  <span class="subject-stat" title="Общее время всех закрытых сессий предмета за всё время">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></svg>
                    {{ subject.closedSessionHours | number:'1.0-2' }} ч
                  </span>
                  <span aria-hidden="true">\\</span>
                  <span class="subject-stat" title="Проекты" [attr.aria-label]="'Проекты: ' + subject.projectCount">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 7V5h6l2 2h10v12H3Z" /></svg>
                    {{ subject.projectCount }}
                  </span>
                  <span aria-hidden="true">\\</span>
                  <span class="subject-stat" title="Задачи в работе и отложенные" [attr.aria-label]="'Задачи в работе и отложенные: ' + (subject.inProgressTaskCount + subject.deferredTaskCount)">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="4" y="3" width="16" height="18" rx="2" /><path d="m8 12 3 3 5-6" /></svg>
                    {{ subject.inProgressTaskCount + subject.deferredTaskCount }}
                  </span>
                </div>
                <span class="subject-arrow" aria-hidden="true">↗</span>
              </a>
            } @empty {
              @if (!error()) { <div class="empty-state"><h2>С чего начнём?</h2><p>Добавьте первый предмет, например «Математика» или «ASP.NET».</p></div> }
            }
          </div>
        }
      </section>
      <section class="panel editor">
        <p class="eyebrow">НОВОЕ НАПРАВЛЕНИЕ</p><h2>Добавить предмет</h2>
        <form autocomplete="off" (ngSubmit)="create(form)" #form="ngForm">
          <label for="subject-name">Название</label>
          <input autocomplete="off" id="subject-name" name="name" [(ngModel)]="name" required maxlength="100" placeholder="Например, Angular" [disabled]="saving()">
          <p class="field-hint">До 100 символов. Название должно быть уникальным.</p>
          <label for="subject-short-name">Короткое имя</label>
          <input autocomplete="off" id="subject-short-name" name="shortName" [(ngModel)]="shortName" appShortCode #codeField="ngModel" required maxlength="8" placeholder="Например, DEV" [disabled]="saving()">
          <p class="field-hint">До 8 символов. Используется в кодах проектов и задач.</p>
          @if (codeField.dirty && codeField.invalid) { <p class="error" role="alert">Короткое имя должно содержать от 1 до 8 символов, без слешей и управляющих символов.</p> }
          <label for="subject-description">Описание предмета</label>
          <textarea autocomplete="off" id="subject-description" name="description" [(ngModel)]="description" maxlength="4000" rows="5" [disabled]="saving()"></textarea>
          <p class="field-hint">{{ description.length }} / 4000 · Необязательно. Поддерживается Markdown.</p>
          @if (createError()) { <p class="error" role="alert">{{ createError() }}</p> }
          <button [disabled]="form.invalid || !name.trim() || !shortName.trim() || saving()">{{ saving() ? 'Добавляем…' : 'Создать предмет' }}</button>
        </form>
      </section>
    </div>
    @if (message()) { <p class="toast" role="status">{{ message() }}</p> }
  `
})
export class SubjectsComponent {
  private readonly api = inject(SubjectsApi);
  private readonly destroyRef = inject(DestroyRef);
  readonly subjects = signal<Subject[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly createError = signal('');
  readonly message = signal('');
  name = '';
  shortName = '';
  description = '';
  constructor() { this.load(); }
  load() {
    this.loading.set(true); this.error.set('');
    this.api.subjects().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: subjects => { this.subjects.set(subjects); this.loading.set(false); },
      error: error => { this.error.set(errorMessage(error)); this.loading.set(false); }
    });
  }
  create(form: NgForm) {
    if (this.saving() || !this.name.trim() || !isValidShortCode(this.shortName) || this.description.length > 4000) return;
    this.saving.set(true); this.createError.set('');
    this.api.createSubject(this.name, this.shortName, this.description).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.name = ''; this.shortName = ''; this.description = '';
        form.resetForm({ name: '', shortName: '', description: '' });
        this.saving.set(false); this.message.set('Предмет создан.'); this.load();
      },
      error: error => { this.saving.set(false); this.createError.set(errorMessage(error)); }
    });
  }
}

