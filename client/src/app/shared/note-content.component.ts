import { Component, computed, input } from '@angular/core';
import { parseNoteMarkdown } from './note-markdown';
import { NoteInlineComponent } from './note-inline.component';

@Component({
  selector: 'app-note-content',
  imports: [NoteInlineComponent],
  host: {
    '[class.preview]': 'preview()',
    '[style.--preview-lines]': 'previewLines()',
    '[style.--preview-height]': "previewLines() * 1.85 + 'em'"
  },
  template: `
    @for (block of blocks(); track $index) {
      @switch (block.kind) {
        @case ('heading') {
          @switch (block.level) {
            @case (1) { <h1><app-note-inline [text]="block.text" /></h1> }
            @case (2) { <h2><app-note-inline [text]="block.text" /></h2> }
            @case (3) { <h3><app-note-inline [text]="block.text" /></h3> }
            @case (4) { <h4><app-note-inline [text]="block.text" /></h4> }
          }
        }
        @case ('unordered') {
          <ul>@for (item of block.items; track $index) { <li><app-note-inline [text]="item" /></li> }</ul>
        }
        @case ('ordered') {
          <ol class="numbered-list">
            @for (item of block.items; track $index) {
              <li><span class="numbered-marker">{{ item.marker }}</span><span><app-note-inline [text]="item.text" /></span></li>
            }
          </ol>
        }
        @case ('paragraph') { <p><app-note-inline [text]="block.text" /></p> }
        @case ('quote') { <blockquote><app-note-inline [text]="block.text" /></blockquote> }
        @case ('divider') { <hr /> }
      }
    }
  `,
  styles: `
    :host { display: block; overflow-wrap: anywhere; font-size: 15px; line-height: 1.85; margin: 20px 0; }
    h1, h2, h3, h4 { font-family: inherit; font-weight: 650; line-height: 1.35; margin: 1em 0 .5em; }
    h1 { font-size: 26px; }
    h2 { font-size: 22px; }
    h3 { font-size: 19px; }
    h4 { font-size: 16px; }
    p { white-space: pre-wrap; margin: 0 0 .8em; }
    blockquote { white-space: pre-wrap; margin: 0 0 .8em; padding: 8px 12px; border-left: 3px solid #6f8c78; border-radius: 0 5px 5px 0; background: #e9eddf; color: #3e5c50; }
    hr { border: 0; border-top: 1px solid #9caf9f; margin: .8em 0; }
    ul, ol { margin: 0 0 .8em; padding-left: 1.75em; }
    li { white-space: pre-wrap; }
    .numbered-list { list-style: none; padding-left: .75em; }
    .numbered-list li { display: flex; align-items: baseline; gap: .35em; }
    .numbered-marker { flex-shrink: 0; font-variant-numeric: tabular-nums; }
    :host > :first-child { margin-top: 0; }
    :host > :last-child { margin-bottom: 0; }
    :host.preview { display: -webkit-box; -webkit-box-orient: vertical; -webkit-line-clamp: var(--preview-lines); line-clamp: var(--preview-lines); max-height: var(--preview-height); overflow: hidden; }
    :host.preview h1, :host.preview h2, :host.preview h3, :host.preview h4 { margin: 0; }
    :host.preview p, :host.preview ul, :host.preview ol { margin: 0; }
    :host.preview blockquote { margin: 0; padding: 4px 10px; }
  `
})
export class NoteContentComponent {
  readonly text = input.required<string>();
  readonly preview = input(false);
  readonly previewLines = input(3);
  readonly blocks = computed(() => parseNoteMarkdown(this.text()));
}
