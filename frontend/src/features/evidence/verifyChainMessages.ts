import type { ChainFailureReason, VerifyChainResponse } from '../../api/types';

const REASON_LABEL: Record<ChainFailureReason, string> = {
  HashMismatch: 'el hash de este evento no coincide con su contenido: fue alterado después de registrarse.',
  BrokenLink: 'el hash previo de este evento no coincide con el hash del evento anterior: la cadena está rota.',
  SequenceGap: 'falta un evento en la secuencia antes de este punto.',
};

export function describeVerifyResult(result: VerifyChainResponse): string {
  if (result.isValid) {
    return `Cadena íntegra: ${result.eventCount} eventos verificados.`;
  }
  const invalid = result.firstInvalidEvent!;
  return `Cadena rota en el evento #${invalid.sequence}: ${REASON_LABEL[invalid.reason]}`;
}
