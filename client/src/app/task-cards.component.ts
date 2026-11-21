import { Component, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProjectTask, taskStatuses } from './api.service';
import { DesktopPinComponent } from './desktop-pin.component';

@Component({
  selector: 'app-task-cards',
  imports: [RouterLink, DesktopPinComponent],
  styles: `
    .task-card-layout { min-width: 0; padding: 0; }
    .task-card-link { display: block; padding: 24px; border-radius: inherit; }
    .task-card-link h2 { margin-bottom: 12px; }
    .task-card-link .project-code { padding-right: 20px; }
    .with-pin .project-code { padding-right: 56px; }
    .with-pin .subject-arrow { top: 16px; right: 60px; line-height: 34px; }
    .task-card-pin { position: absolute; top: 16px; right: 16px; z-index: 1; --task-pin-size: 34px; }
    .task-preview { display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden; white-space: pre-wrap; margin: 12px 0 0; }
  `,
  template: `
    @if (pinError()) { <p class="error" role="alert">{{ pinError() }}</p> }
    <div class="subject-grid project-grid">
      @for (task of tasks(); track task.id) {
        <article class="subject-card task-card-layout" [class.with-pin]="showPin()">
          <a class="task-card-link" [routerLink]="['/projects', task.projectId, 'tasks', task.id]">
            <p class="project-code">{{ task.fullCode }}</p>
            <h2>{{ task.name }}</h2>
            <span class="tag">{{ statusLabel(task) }}</span>
            <p class="muted task-preview">{{ task.body }}</p>
            <span class="subject-arrow" aria-hidden="true">↗</span>
          </a>
          @if (showPin()) {
            <app-desktop-pin class="task-card-pin" [targetId]="task.id" (errorChanged)="pinError.set($event)" />
          }
        </article>
      }
    </div>
  `
})
export class TaskCardsComponent {
  readonly tasks = input.required<ProjectTask[]>();
  readonly showPin = input(false);
  readonly pinError = signal('');
  statusLabel(task: ProjectTask) { return taskStatuses.find(status => status.value === task.status)!.label; }
}
