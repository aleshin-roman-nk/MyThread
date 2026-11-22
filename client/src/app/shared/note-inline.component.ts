import { Component, computed, input } from '@angular/core';
import { parseNoteInline } from './note-markdown';

@Component({
  selector: 'app-note-inline',
  styles: `:host { display: inline; } strong { font-weight: 750; }`,
  template: `
    @for (span of spans(); track $index) {
      @if (span.bold && span.italic) { <strong><em>{{ span.text }}</em></strong> }
      @else if (span.bold) { <strong>{{ span.text }}</strong> }
      @else if (span.italic) { <em>{{ span.text }}</em> }
      @else { <span>{{ span.text }}</span> }
    }
  `
})
export class NoteInlineComponent {
  readonly text = input.required<string>();
  readonly spans = computed(() => parseNoteInline(this.text()));
}
