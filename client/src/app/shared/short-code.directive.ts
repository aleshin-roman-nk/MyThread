import { Directive, forwardRef } from '@angular/core';
import { AbstractControl, NG_VALIDATORS, ValidationErrors, Validator } from '@angular/forms';

export function isValidShortCode(value: string): boolean {
  const code = value.trim();
  return code.length > 0 && code.length <= 8 && !/[\\/\u0000-\u001f\u007f-\u009f]/.test(code);
}

@Directive({
  selector: 'input[appShortCode]',
  host: { '[attr.maxlength]': '8' },
  providers: [{ provide: NG_VALIDATORS, useExisting: forwardRef(() => ShortCodeDirective), multi: true }]
})
export class ShortCodeDirective implements Validator {
  validate(control: AbstractControl): ValidationErrors | null {
    return isValidShortCode(String(control.value ?? '')) ? null : { shortCode: true };
  }
}
