using System.Data;
using System.Diagnostics;
using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Hashing;
using EvidenceChain.Api.Infrastructure.Persistence;
using EvidenceChain.Api.Infrastructure.Persistence.Configurations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EvidenceChain.Api.Seed;

public class DeterministicSeeder(AppDbContext db)
{
    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seeder = new DeterministicSeeder(db);
        await seeder.SeedAsync(cancellationToken);
    }

    public async Task<SeedData> SeedAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine("--> Iniciando generación y carga determinista de datos (seed)...");

        var seedData = SeedGenerator.Generate(SeedConstants.DefaultRandomSeed);

        var connection = db.Database.GetDbConnection() as SqlConnection
            ?? throw new InvalidOperationException("La conexión a la base de datos no es una instancia de SqlConnection.");

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Limpieza atómica previa para asegurar idempotencia total
            await WipeExistingDataAsync(connection, transaction, cancellationToken);

            // 2. Preparar DataTables en memoria para SqlBulkCopy ultrarrápido
            using var usersTable = BuildUsersTable(seedData.Users);
            using var evidenceTable = BuildEvidenceTable(seedData.Evidence);
            using var transfersTable = BuildTransfersTable(seedData.CustodyTransfers);
            using var eventsTable = BuildEventsTable(seedData.CustodyEvents);

            // 3. Inserción masiva optimizada (< 1 segundo para 10.000+ filas)
            const SqlBulkCopyOptions copyOptions =
                SqlBulkCopyOptions.KeepIdentity |
                SqlBulkCopyOptions.CheckConstraints |
                SqlBulkCopyOptions.TableLock;

            await BulkCopyTableAsync(connection, transaction, "dbo.Users", usersTable, copyOptions, cancellationToken);
            await BulkCopyTableAsync(connection, transaction, "dbo.Evidence", evidenceTable, copyOptions, cancellationToken);
            await BulkCopyTableAsync(connection, transaction, "dbo.CustodyTransfers", transfersTable, copyOptions, cancellationToken);
            await BulkCopyTableAsync(connection, transaction, "dbo.CustodyEvents", eventsTable, copyOptions, cancellationToken);

            // 4. CASO ESPECIAL: Evidencia con evento ALTERADO (EVD-DEMO-TAMPER)
            // Desactivamos temporalmente el trigger append-only EXCLUSIVAMENTE para simular
            // un ataque malicioso directo a la base de datos (DBA deshonesto o inyección SQL),
            // demostrando que el hash SHA-256 canónico detecta la manipulación (HashMismatch)
            // independientemente de los controles de la base de datos.
            await ApplyTamperedEventModificationAsync(connection, transaction, cancellationToken);

            // 5. Reajustar semillas IDENTITY para que inserciones futuras vía API no colisionen
            await ReseedIdentitiesAsync(connection, transaction, seedData, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            sw.Stop();

            Console.WriteLine($"""
                =============================================================
                Seed completado exitosamente en {sw.ElapsedMilliseconds} ms ({sw.Elapsed.TotalSeconds:F2} s)
                -------------------------------------------------------------
                Usuarios creados:       {seedData.TotalUsers}
                Evidencias creadas:     {seedData.TotalEvidence}
                Transferencias creadas: {seedData.TotalTransfers}
                Eventos de custodia:    {seedData.TotalEvents}
                -------------------------------------------------------------
                Casos demo disponibles:
                  [1] Íntegra:     {SeedConstants.IntactEvidenceCode} (Id {SeedConstants.IntactEvidenceId}) -> Cadena completa válida
                  [2] Alterada:    {SeedConstants.TamperedEvidenceCode} (Id {SeedConstants.TamperedEvidenceId}) -> HashMismatch en Secuencia {SeedConstants.TamperedSequence}
                  [3] Vencida:     {SeedConstants.AnomalyEvidenceCode} (Id {SeedConstants.AnomalyEvidenceId}) -> Transferencia pendiente > 48 h
                =============================================================
                """);

            return seedData;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task WipeExistingDataAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string wipeSql = $"""
            ALTER TABLE dbo.CustodyEvents DISABLE TRIGGER {CustodyEventConfiguration.AppendOnlyTrigger};
            DELETE FROM dbo.CustodyEvents;
            DELETE FROM dbo.CustodyTransfers;
            DELETE FROM dbo.Evidence;
            DELETE FROM dbo.Users;
            DELETE FROM dbo.IdempotencyRecords;
            DBCC CHECKIDENT ('dbo.CustodyEvents', RESEED, 0);
            DBCC CHECKIDENT ('dbo.Evidence', RESEED, 0);
            DBCC CHECKIDENT ('dbo.Users', RESEED, 0);
            ALTER TABLE dbo.CustodyEvents ENABLE TRIGGER {CustodyEventConfiguration.AppendOnlyTrigger};
        """;

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = wipeSql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ApplyTamperedEventModificationAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string tamperSql = $"""
            ALTER TABLE dbo.CustodyEvents DISABLE TRIGGER {CustodyEventConfiguration.AppendOnlyTrigger};

            UPDATE dbo.CustodyEvents
            SET Notes = @alteredNotes
            WHERE EvidenceId = @evidenceId AND Sequence = @sequence;

            ALTER TABLE dbo.CustodyEvents ENABLE TRIGGER {CustodyEventConfiguration.AppendOnlyTrigger};
        """;

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = tamperSql;
        cmd.Parameters.Add(new SqlParameter("@alteredNotes", SqlDbType.NVarChar, 500) { Value = SeedConstants.TamperedAlteredNotes });
        cmd.Parameters.Add(new SqlParameter("@evidenceId", SqlDbType.Int) { Value = SeedConstants.TamperedEvidenceId });
        cmd.Parameters.Add(new SqlParameter("@sequence", SqlDbType.Int) { Value = SeedConstants.TamperedSequence });
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReseedIdentitiesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SeedData seedData,
        CancellationToken cancellationToken)
    {
        var reseedSql = $"""
            DBCC CHECKIDENT ('dbo.Users', RESEED, {seedData.TotalUsers});
            DBCC CHECKIDENT ('dbo.Evidence', RESEED, {seedData.TotalEvidence});
            DBCC CHECKIDENT ('dbo.CustodyEvents', RESEED, {seedData.TotalEvents});
        """;

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = reseedSql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task BulkCopyTableAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string destinationTable,
        DataTable dataTable,
        SqlBulkCopyOptions copyOptions,
        CancellationToken cancellationToken)
    {
        using var bulkCopy = new SqlBulkCopy(connection, copyOptions, transaction)
        {
            DestinationTableName = destinationTable,
            BatchSize = 10_000,
            BulkCopyTimeout = 60,
        };

        foreach (DataColumn column in dataTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
    }

    private static DataTable BuildUsersTable(IReadOnlyList<User> users)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("UserName", typeof(string));
        table.Columns.Add("DisplayName", typeof(string));
        table.Columns.Add("Role", typeof(string));

        foreach (var u in users)
        {
            table.Rows.Add(u.Id, u.UserName, u.DisplayName, u.Role);
        }

        return table;
    }

    private static DataTable BuildEvidenceTable(IReadOnlyList<Evidence> evidenceList)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Code", typeof(string));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("CurrentCustodianId", typeof(int));
        table.Columns.Add("LastEventAtUtc", typeof(DateTime));
        table.Columns.Add("IntegrityStatus", typeof(byte));
        table.Columns.Add("IntegrityCheckedAtUtc", typeof(DateTime)).AllowDBNull = true;
        table.Columns.Add("CreatedAtUtc", typeof(DateTime));

        foreach (var e in evidenceList)
        {
            table.Rows.Add(
                e.Id,
                e.Code,
                e.Description,
                e.CurrentCustodianId,
                e.LastEventAtUtc,
                (byte)e.IntegrityStatus,
                e.IntegrityCheckedAtUtc.HasValue ? e.IntegrityCheckedAtUtc.Value : DBNull.Value,
                e.CreatedAtUtc);
        }

        return table;
    }

    private static DataTable BuildTransfersTable(IReadOnlyList<CustodyTransfer> transfers)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("EvidenceId", typeof(int));
        table.Columns.Add("FromCustodianId", typeof(int));
        table.Columns.Add("ToCustodianId", typeof(int));
        table.Columns.Add("RequestedByUserId", typeof(int));
        table.Columns.Add("Status", typeof(byte));
        table.Columns.Add("Reason", typeof(string));
        table.Columns.Add("RequestedAtUtc", typeof(DateTime));
        table.Columns.Add("RespondedAtUtc", typeof(DateTime)).AllowDBNull = true;
        table.Columns.Add("RespondedByUserId", typeof(int)).AllowDBNull = true;
        table.Columns.Add("ResponseNote", typeof(string)).AllowDBNull = true;

        foreach (var t in transfers)
        {
            table.Rows.Add(
                t.Id,
                t.EvidenceId,
                t.FromCustodianId,
                t.ToCustodianId,
                t.RequestedByUserId,
                (byte)t.Status,
                t.Reason,
                t.RequestedAtUtc,
                t.RespondedAtUtc.HasValue ? t.RespondedAtUtc.Value : DBNull.Value,
                t.RespondedByUserId.HasValue ? t.RespondedByUserId.Value : DBNull.Value,
                t.ResponseNote is not null ? t.ResponseNote : DBNull.Value);
        }

        return table;
    }

    private static DataTable BuildEventsTable(IReadOnlyList<CustodyEvent> events)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("EvidenceId", typeof(int));
        table.Columns.Add("Sequence", typeof(int));
        table.Columns.Add("EventType", typeof(byte));
        table.Columns.Add("ActorUserId", typeof(int));
        table.Columns.Add("FromCustodianId", typeof(int)).AllowDBNull = true;
        table.Columns.Add("ToCustodianId", typeof(int)).AllowDBNull = true;
        table.Columns.Add("TransferId", typeof(Guid)).AllowDBNull = true;
        table.Columns.Add("Notes", typeof(string)).AllowDBNull = true;
        table.Columns.Add("OccurredAtUtc", typeof(DateTime));
        table.Columns.Add("PreviousHash", typeof(byte[]));
        table.Columns.Add("Hash", typeof(byte[]));

        foreach (var ev in events)
        {
            // Para la inserción inicial en bulto, el evento alterado (Evidencia 2, Secuencia 2)
            // se inserta bien formado (con sus notas originales que corresponden a su hash).
            // Posteriormente, el seeder desactiva el trigger y actualiza las notas al valor manipulado.
            string? notesToInsert = (ev.EvidenceId == SeedConstants.TamperedEvidenceId && ev.Sequence == SeedConstants.TamperedSequence)
                ? SeedConstants.TamperedOriginalNotes
                : ev.Notes;

            table.Rows.Add(
                ev.Id,
                ev.EvidenceId,
                ev.Sequence,
                (byte)ev.EventType,
                ev.ActorUserId,
                ev.FromCustodianId.HasValue ? ev.FromCustodianId.Value : DBNull.Value,
                ev.ToCustodianId.HasValue ? ev.ToCustodianId.Value : DBNull.Value,
                ev.TransferId.HasValue ? ev.TransferId.Value : DBNull.Value,
                notesToInsert is not null ? notesToInsert : DBNull.Value,
                ev.OccurredAtUtc,
                ev.PreviousHash,
                ev.Hash);
        }

        return table;
    }
}
