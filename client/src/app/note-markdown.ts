export type NoteBlock =
  | { kind: 'paragraph'; text: string }
  | { kind: 'quote'; text: string }
  | { kind: 'divider' }
  | { kind: 'heading'; level: number; text: string }
  | { kind: 'ordered'; items: { marker: string; text: string }[] }
  | { kind: 'unordered'; items: string[] };

export interface NoteInlineSpan { text: string; bold: boolean; italic: boolean; }

export function parseNoteInline(text: string): NoteInlineSpan[] {
  return parseInlineSpans(text, false, false);
}

function parseInlineSpans(text: string, bold: boolean, italic: boolean): NoteInlineSpan[] {
  const spans: NoteInlineSpan[] = [];
  const emphasis = /(?<![\\*])(\*{1,3})(?!\*)(\S(?:[^\n]*?\S)?)(?<![\\*])\1(?!\*)/g;
  let position = 0;
  for (const match of text.matchAll(emphasis)) {
    if (match.index > position) spans.push({ text: text.slice(position, match.index), bold, italic });
    const length = match[1].length;
    spans.push(...parseInlineSpans(match[2], bold || length >= 2, italic || length !== 2));
    position = match.index + match[0].length;
  }
  if (position < text.length) spans.push({ text: text.slice(position), bold, italic });
  return spans;
}

export function parseNoteMarkdown(text: string): NoteBlock[] {
  const blocks: NoteBlock[] = [];
  let current: NoteBlock | undefined;

  for (const line of text.replace(/\r\n?/g, '\n').split('\n')) {
    if (!line.trim()) {
      current = undefined;
      continue;
    }

    const quote = /^ {0,3}>[\t ]?(.*)$/.exec(line);
    if (quote) {
      if (current?.kind === 'quote') {
        current.text += '\n' + quote[1];
      } else {
        current = { kind: 'quote', text: quote[1] };
        blocks.push(current);
      }
      continue;
    }

    if (/^ {0,3}-{3,}[\t ]*$/.test(line)) {
      blocks.push({ kind: 'divider' });
      current = undefined;
      continue;
    }

    const heading = /^(#{1,4})[\t ]+(.+)$/.exec(line);
    if (heading) {
      blocks.push({ kind: 'heading', level: heading[1].length, text: heading[2] });
      current = undefined;
      continue;
    }

    const unordered = /^-[\t ]+(.*)$/.exec(line);
    const outline = /^(\d{1,9}(?:\.\d{1,9})+)(\.?)[\t ]+(.*)$/.exec(line);
    const ordered = /^(\d{1,9})\.[\t ]+(.*)$/.exec(line);
    if (outline || ordered) {
      const item = outline ? { marker: outline[1] + outline[2], text: outline[3] }
        : { marker: ordered![1] + '.', text: ordered![2] };
      if (current?.kind === 'ordered') {
        current.items.push(item);
      } else {
        current = { kind: 'ordered', items: [item] };
        blocks.push(current);
      }
      continue;
    }
    if (unordered) {
      if (current?.kind === 'unordered') {
        current.items.push(unordered[1]);
      } else {
        current = { kind: 'unordered', items: [unordered[1]] };
        blocks.push(current);
      }
      continue;
    }

    if (current?.kind === 'paragraph') {
      current.text += '\n' + line;
    } else {
      current = { kind: 'paragraph', text: line };
      blocks.push(current);
    }
  }

  return blocks;
}
