import { HttpErrorResponse } from '@angular/common/http';

export function errorMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'Нет связи с сервером. Проверьте подключение и повторите попытку.';
    return error.error?.title ?? 'Не удалось выполнить запрос. Повторите попытку.';
  }
  return 'Не удалось выполнить запрос. Повторите попытку.';
}
