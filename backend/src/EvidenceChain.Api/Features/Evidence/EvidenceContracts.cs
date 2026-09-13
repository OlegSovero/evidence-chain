using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Features.Common;
using EvidenceChain.Api.Features.Transfers;

namespace EvidenceChain.Api.Features.Evidence;

public sealed record EvidenceListItem(
    int Id,
    string Code,
    string Description,
    UserSummary CurrentCustodian,
    DateTime LastEventAtUtc,
    EvidenceIntegrityStatus IntegrityStatus);

public sealed record EvidencePage(IReadOnlyList<EvidenceListItem> Items, string? NextCursor);

public sealed record EvidenceDetail(
    int Id,
    string Code,
    string Description,
    UserSummary CurrentCustodian,
    DateTime LastEventAtUtc,
    EvidenceIntegrityStatus IntegrityStatus,
    DateTime? IntegrityCheckedAtUtc,
    DateTime CreatedAtUtc,
    int EventCount,
    TransferResponse? PendingTransfer);
