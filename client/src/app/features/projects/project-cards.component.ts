import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Project, projectStatuses } from './projects-api';

@Component({
  selector: 'app-project-cards',
  imports: [RouterLink],
  styles: `
    .project-card h2 { margin-bottom: 12px; }
    .project-card .project-code { padding-right: 20px; }
    .project-meta { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; }
  `,
  template: `
    <div class="subject-grid project-grid">
      @for (project of projects(); track project.id) {
        <a class="subject-card project-card" [routerLink]="['/projects', project.id]">
          <p class="project-code">{{ project.fullCode }}</p>
          <h2>{{ project.name }}</h2>
          <div class="project-meta"><span class="muted">Задач: {{ project.tasks.length }}</span><span class="tag">{{ statusLabel(project) }}</span></div>
          <span class="subject-arrow" aria-hidden="true">↗</span>
        </a>
      }
    </div>
  `
})
export class ProjectCardsComponent {
  readonly projects = input.required<Project[]>();
  statusLabel(project: Project) { return projectStatuses.find(status => status.value === project.status)!.label; }
}
