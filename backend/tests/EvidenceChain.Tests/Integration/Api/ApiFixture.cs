using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace EvidenceChain.Tests.Integration.Api;

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}

// One SQL Server container and one in-process API host shared by every test in the
// "api" collection. Tests isolate their data with unique user names and evidence codes.
public sealed class ApiFixture : IAsyncLifetime
{
    public const string JwtKey = "integration-tests-signing-key-0123456789abcdef0123456789";
    public const int ThresholdHours = 48;

    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = string.Empty;
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "EvidenceChainApiTests",
        }.ConnectionString;

        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Sql", ConnectionString);
            builder.UseSetting("Jwt:Key", JwtKey);
            builder.UseSetting("Anomalies:PendingTransferThresholdHours", ThresholdHours.ToString());
        });
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task<HttpClient> ClientAsAsync(User user)
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/token", new { userName = user.UserName });
        response.EnsureSuccessStatusCode();

        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

internal static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static Task<T?> ReadAsync<T>(this HttpContent content) => content.ReadFromJsonAsync<T>(Options);
}
