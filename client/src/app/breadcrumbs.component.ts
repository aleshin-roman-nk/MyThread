import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

export interface Breadcrumb { label: string; link?: (string | number)[]; }

@Component({
  selector: 'app-breadcrumbs',
  imports: [RouterLink],
  styles: `
    nav { display: block; margin-bottom: 28px; }
    ol { display: flex; flex-wrap: wrap; align-items: baseline; gap: 8px 0; list-style: none; padding: 0; margin: 0; font-size: 26px; line-height: 1.6; }
    li { min-width: 0; overflow-wrap: anywhere; }
    li + li::before { content: '/'; padding: 0 10px; color: #929b8f; }
    a { padding: 0; border-radius: 0; color: #66766e; font-size: inherit; font-weight: 400; }
    a:hover { color: #214f46; text-decoration: underline; }
    [aria-current] { color: #243c36; font-weight: 600; }
  `,
  template: `
    <nav aria-label="Хлебные крошки"><ol>
      @for (item of items(); track $index) {
        <li>@if (item.link) { <a [routerLink]="item.link">{{ item.label }}</a> }
          @else { <span aria-current="page">{{ item.label }}</span> }</li>
      }
    </ol></nav>
  `
})
export class BreadcrumbsComponent { readonly items = input.required<Breadcrumb[]>(); }
