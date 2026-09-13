IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE TABLE [IdempotencyRecords] (
        [UserId] int NOT NULL,
        [IdempotencyKey] nvarchar(100) NOT NULL,
        [RequestHash] binary(32) NOT NULL,
        [StatusCode] int NOT NULL,
        [ResponseBody] nvarchar(max) NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_Idempotency] PRIMARY KEY ([UserId], [IdempotencyKey])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [UserName] nvarchar(50) NOT NULL,
        [DisplayName] nvarchar(150) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Users_Role] CHECK ([Role] IN (N'Investigador', N'Custodio', N'Supervisor'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE TABLE [Evidence] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(20) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [CurrentCustodianId] int NOT NULL,
        [LastEventAtUtc] datetime2(3) NOT NULL,
        [IntegrityStatus] tinyint NOT NULL,
        [IntegrityCheckedAtUtc] datetime2(3) NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_Evidence] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Evidence_Users_CurrentCustodianId] FOREIGN KEY ([CurrentCustodianId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE TABLE [CustodyTransfers] (
        [Id] uniqueidentifier NOT NULL,
        [EvidenceId] int NOT NULL,
        [FromCustodianId] int NOT NULL,
        [ToCustodianId] int NOT NULL,
        [RequestedByUserId] int NOT NULL,
        [Status] tinyint NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [RequestedAtUtc] datetime2(3) NOT NULL,
        [RespondedAtUtc] datetime2(3) NULL,
        [RespondedByUserId] int NULL,
        [ResponseNote] nvarchar(500) NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_CustodyTransfers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Transfer_DistinctCustodians] CHECK ([FromCustodianId] <> [ToCustodianId]),
        CONSTRAINT [FK_CustodyTransfers_Evidence_EvidenceId] FOREIGN KEY ([EvidenceId]) REFERENCES [Evidence] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustodyTransfers_Users_FromCustodianId] FOREIGN KEY ([FromCustodianId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustodyTransfers_Users_RequestedByUserId] FOREIGN KEY ([RequestedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustodyTransfers_Users_RespondedByUserId] FOREIGN KEY ([RespondedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustodyTransfers_Users_ToCustodianId] FOREIGN KEY ([ToCustodianId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE TABLE [CustodyEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [EvidenceId] int NOT NULL,
        [Sequence] int NOT NULL,
        [EventType] tinyint NOT NULL,
        [ActorUserId] int NOT NULL,
        [FromCustodianId] int NULL,
        [ToCustodianId] int NULL,
        [TransferId] uniqueidentifier NULL,
        [Notes] nvarchar(500) NULL,
        [OccurredAtUtc] datetime2(3) NOT NULL,
        [PreviousHash] binary(32) NOT NULL,
        [Hash] binary(32) NOT NULL,
        CONSTRAINT [PK_CustodyEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustodyEvents_CustodyTransfers_TransferId] FOREIGN KEY ([TransferId]) REFERENCES [CustodyTransfers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustodyEvents_Evidence_EvidenceId] FOREIGN KEY ([EvidenceId]) REFERENCES [Evidence] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustodyEvents_Users_ActorUserId] FOREIGN KEY ([ActorUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustodyEvents_Users_FromCustodianId] FOREIGN KEY ([FromCustodianId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustodyEvents_Users_ToCustodianId] FOREIGN KEY ([ToCustodianId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustodyEvents_ActorUserId] ON [CustodyEvents] ([ActorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustodyEvents_FromCustodianId] ON [CustodyEvents] ([FromCustodianId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustodyEvents_ToCustodianId] ON [CustodyEvents] ([ToCustodianId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustodyEvents_TransferId] ON [CustodyEvents] ([TransferId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_CustodyEvents_Evidence_Seq] ON [CustodyEvents] ([EvidenceId], [Sequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustodyTransfers_FromCustodianId] ON [CustodyTransfers] ([FromCustodianId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustodyTransfers_RequestedByUserId] ON [CustodyTransfers] ([RequestedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustodyTransfers_RespondedByUserId] ON [CustodyTransfers] ([RespondedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustodyTransfers_ToCustodianId] ON [CustodyTransfers] ([ToCustodianId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Transfer_PendingAge] ON [CustodyTransfers] ([Status], [RequestedAtUtc]) INCLUDE ([EvidenceId], [ToCustodianId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Transfer_OnePendingPerEvidence] ON [CustodyTransfers] ([EvidenceId]) WHERE [Status] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Evidence_Custodian_Keyset] ON [Evidence] ([CurrentCustodianId], [LastEventAtUtc] DESC, [Id] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Evidence_Keyset] ON [Evidence] ([LastEventAtUtc] DESC, [Id] DESC) INCLUDE ([Code], [Description], [CurrentCustodianId], [IntegrityStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Evidence_Code] ON [Evidence] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UX_Users_UserName] ON [Users] ([UserName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    CREATE TRIGGER [dbo].[TR_CustodyEvents_AppendOnly]
    ON [dbo].[CustodyEvents]
    INSTEAD OF UPDATE, DELETE
    AS
    BEGIN
        THROW 50001, N'CustodyEvents es append-only', 1;
    END;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913040102_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260913040102_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

