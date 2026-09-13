using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Infrastructure.Http;

public static class SqlErrors
{
    public const int DuplicateKeyIndex = 2601;
    public const int DuplicateKeyConstraint = 2627;

    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: DuplicateKeyIndex or DuplicateKeyConstraint };
}
