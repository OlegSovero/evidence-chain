import { ApiError } from './client';

// Mensaje legible en español para cualquier error de red o de la API,
// priorizando el detail de problem+json que ya viene redactado por el backend.
export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    return error.problem.detail ?? error.problem.title ?? `La API respondió ${error.status}.`;
  }
  if (error instanceof Error) {
    if (error.name === 'AbortError') {
      return 'La solicitud se canceló.';
    }
    return error.message;
  }
  return 'Ocurrió un error inesperado.';
}
