using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EvidenceChain.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Idempotency", x => new { x.UserId, x.IdempotencyKey });
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_Role", "[Role] IN (N'Investigador', N'Custodio', N'Supervisor')");
                });

            migrationBuilder.CreateTable(
                name: "Evidence",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CurrentCustodianId = table.Column<int>(type: "int", nullable: false),
                    LastEventAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    IntegrityStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    IntegrityCheckedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Evidence_Users_CurrentCustodianId",
                        column: x => x.CurrentCustodianId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustodyTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceId = table.Column<int>(type: "int", nullable: false),
                    FromCustodianId = table.Column<int>(type: "int", nullable: false),
                    ToCustodianId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RespondedByUserId = table.Column<int>(type: "int", nullable: true),
                    ResponseNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustodyTransfers", x => x.Id);
                    table.CheckConstraint("CK_Transfer_DistinctCustodians", "[FromCustodianId] <> [ToCustodianId]");
                    table.ForeignKey(
                        name: "FK_CustodyTransfers_Evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "Evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyTransfers_Users_FromCustodianId",
                        column: x => x.FromCustodianId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyTransfers_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyTransfers_Users_RespondedByUserId",
                        column: x => x.RespondedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyTransfers_Users_ToCustodianId",
                        column: x => x.ToCustodianId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustodyEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvidenceId = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<byte>(type: "tinyint", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    FromCustodianId = table.Column<int>(type: "int", nullable: true),
                    ToCustodianId = table.Column<int>(type: "int", nullable: true),
                    TransferId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    PreviousHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    Hash = table.Column<byte[]>(type: "binary(32)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustodyEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustodyEvents_CustodyTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "CustodyTransfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyEvents_Evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "Evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyEvents_Users_FromCustodianId",
                        column: x => x.FromCustodianId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustodyEvents_Users_ToCustodianId",
                        column: x => x.ToCustodianId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustodyEvents_ActorUserId",
                table: "CustodyEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyEvents_FromCustodianId",
                table: "CustodyEvents",
                column: "FromCustodianId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyEvents_ToCustodianId",
                table: "CustodyEvents",
                column: "ToCustodianId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyEvents_TransferId",
                table: "CustodyEvents",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "UX_CustodyEvents_Evidence_Seq",
                table: "CustodyEvents",
                columns: new[] { "EvidenceId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransfers_FromCustodianId",
                table: "CustodyTransfers",
                column: "FromCustodianId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransfers_RequestedByUserId",
                table: "CustodyTransfers",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransfers_RespondedByUserId",
                table: "CustodyTransfers",
                column: "RespondedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyTransfers_ToCustodianId",
                table: "CustodyTransfers",
                column: "ToCustodianId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfer_PendingAge",
                table: "CustodyTransfers",
                columns: new[] { "Status", "RequestedAtUtc" })
                .Annotation("SqlServer:Include", new[] { "EvidenceId", "ToCustodianId" });

            migrationBuilder.CreateIndex(
                name: "UX_Transfer_OnePendingPerEvidence",
                table: "CustodyTransfers",
                column: "EvidenceId",
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_Custodian_Keyset",
                table: "Evidence",
                columns: new[] { "CurrentCustodianId", "LastEventAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_Keyset",
                table: "Evidence",
                columns: new[] { "LastEventAtUtc", "Id" },
                descending: new bool[0])
                .Annotation("SqlServer:Include", new[] { "Code", "Description", "CurrentCustodianId", "IntegrityStatus" });

            migrationBuilder.CreateIndex(
                name: "UX_Evidence_Code",
                table: "Evidence",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);

            // EF cannot model triggers; CustodyEvents is append-only by database rule,
            // not only by application discipline.
            migrationBuilder.Sql("""
                CREATE TRIGGER [dbo].[TR_CustodyEvents_AppendOnly]
                ON [dbo].[CustodyEvents]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    THROW 50001, N'CustodyEvents es append-only', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_CustodyEvents_AppendOnly];");

            migrationBuilder.DropTable(
                name: "CustodyEvents");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "CustodyTransfers");

            migrationBuilder.DropTable(
                name: "Evidence");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
