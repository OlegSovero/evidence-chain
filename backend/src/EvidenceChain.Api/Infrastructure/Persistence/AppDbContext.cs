using Microsoft.EntityFrameworkCore;

namespace EvidenceChain.Api.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
