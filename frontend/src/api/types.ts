// Tipos escritos a mano desde Features/*.cs (backend/src/EvidenceChain.Api).
//
// openapi-typescript se probó primero (ver ai-log.md): el openapi.yaml exportado
// tiene un bug de generación en el backend -- ListDemoUsers, GetChain, VerifyChain
// y ListTransfers comparten por error el schema "Response" porque sus records
// anidados se llaman igual y el generador de OpenAPI de ASP.NET colisiona por el
// nombre corto del tipo. Tipar a mano es más fiable que consumir ese contrato roto.

export type Role = 'Investigador' | 'Custodio' | 'Supervisor';

export interface UserSummary {
  id: number;
  userName: string;
  displayName: string;
}

export interface DemoUser extends UserSummary {
  role: Role;
}

export type EvidenceIntegrityStatus = 'NoVerificada' | 'Integra' | 'Comprometida';
export type TransferStatus = 'Pendiente' | 'Aceptada' | 'Rechazada';
export type AnomalySeverity = 'Media' | 'Alta';
export type CustodyEventType =
  | 'Registrada'
  | 'TransferenciaSolicitada'
  | 'TransferenciaAceptada'
  | 'TransferenciaRechazada';
export type ChainFailureReason = 'HashMismatch' | 'BrokenLink' | 'SequenceGap';

export interface PendingTransferAnomaly {
  rule: string;
  severity: AnomalySeverity;
  message: string;
  hoursPending: number;
  thresholdHours: number;
  evaluatedAtUtc: string;
}

export interface EvidenceSummary {
  id: number;
  code: string;
  description: string;
}

export interface TransferResponse {
  id: string;
  evidence: EvidenceSummary;
  status: TransferStatus;
  reason: string;
  requestedAtUtc: string;
  respondedAtUtc: string | null;
  responseNote: string | null;
  fromCustodian: UserSummary;
  toCustodian: UserSummary;
  requestedBy: UserSummary;
  respondedBy: UserSummary | null;
  /** ETag fuerte tal cual la devuelve la API, p. ej. `"0x00000000000007D1"`. Se reenvía sin tocar como If-Match. */
  version: string;
  anomaly: PendingTransferAnomaly | null;
  /** Solo en el cliente: marca una fila optimista que aún no confirmó el servidor. */
  isOptimistic?: boolean;
}

export interface EvidenceListItem {
  id: number;
  code: string;
  description: string;
  currentCustodian: UserSummary;
  lastEventAtUtc: string;
  integrityStatus: EvidenceIntegrityStatus;
}

export interface EvidencePage {
  items: EvidenceListItem[];
  nextCursor: string | null;
}

export interface EvidenceDetail extends EvidenceListItem {
  integrityCheckedAtUtc: string | null;
  createdAtUtc: string;
  eventCount: number;
  pendingTransfer: TransferResponse | null;
}

export interface ChainEvent {
  id: number;
  sequence: number;
  eventType: CustodyEventType;
  actor: UserSummary;
  fromCustodian: UserSummary | null;
  toCustodian: UserSummary | null;
  transferId: string | null;
  notes: string | null;
  occurredAtUtc: string;
  previousHash: string;
  hash: string;
  anomaly: PendingTransferAnomaly | null;
}

export interface ChainResponse {
  evidenceId: number;
  code: string;
  events: ChainEvent[];
}

export interface VerifyChainResponse {
  evidenceId: number;
  isValid: boolean;
  integrityStatus: EvidenceIntegrityStatus;
  checkedAtUtc: string;
  eventCount: number;
  firstInvalidEvent: { sequence: number; reason: ChainFailureReason } | null;
}

export interface AuthResponse {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  user: DemoUser;
}

export interface ListDemoUsersResponse {
  items: DemoUser[];
}

export interface ListTransfersResponse {
  items: TransferResponse[];
}

export interface TransferCurrentState {
  transferId: string;
  status: TransferStatus;
  version: string;
  respondedBy: string | null;
  respondedAtUtc: string | null;
}

export interface ProblemDetails {
  type?: string | null;
  title?: string | null;
  status?: number | null;
  detail?: string | null;
  instance?: string | null;
  currentState?: TransferCurrentState;
  errors?: Record<string, string[]>;
}
