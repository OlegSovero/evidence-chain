using System.Text.Json.Serialization;
using EvidenceChain.Api.Domain;
using EvidenceChain.Api.Domain.Anomalies;
using EvidenceChain.Api.Features.Auth;
using EvidenceChain.Api.Features.Evidence;
using EvidenceChain.Api.Features.Transfers;
using EvidenceChain.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(options =>
{
    // The native generator ids schemas by short type name only, so this codebase's
    // convention of one nested "Request"/"Response" record per endpoint collides
    // (e.g. IssueToken.Response and GetChain.Response both become "Response").
    // Prefix nested types with their declaring type so every schema id stays unique.
    options.CreateSchemaReferenceId = jsonTypeInfo =>
    {
        var baseId = OpenApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);
        var type = jsonTypeInfo.Type;
        return type is { IsNested: true, DeclaringType: not null }
            ? $"{type.DeclaringType.Name}{baseId}"
            : baseId;
    };
});
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Sql")));

builder.Services.AddOptions<AnomalyOptions>()
    .Bind(builder.Configuration.GetSection("Anomalies"))
    .Validate(o => o.PendingTransferThresholdHours > 0, "Anomalies:PendingTransferThresholdHours must be > 0.")
    .ValidateOnStart();
builder.Services.AddSingleton(sp => new PendingTransferRule(
    sp.GetRequiredService<TimeProvider>(),
    sp.GetRequiredService<IOptions<AnomalyOptions>>().Value.PendingTransferThresholdHours));

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(o => o.Key.Length >= JwtOptions.MinKeyLength,
        $"Jwt:Key must be at least {JwtOptions.MinKeyLength} characters (set it with dotnet user-secrets).")
    .ValidateOnStart();
builder.Services.AddSingleton<TokenIssuer>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Value.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Value.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = TokenIssuer.SigningKey(jwt.Value),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = CurrentUser.UserNameClaim,
            RoleClaimType = CurrentUser.RoleClaim,
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.TransferRequester, policy => policy.RequireRole(Roles.Investigador, Roles.Supervisor))
    .AddPolicy(Policies.Supervisor, policy => policy.RequireRole(Roles.Supervisor));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("ETag", "Location", "Idempotent-Replayed"));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapOpenApi("/openapi/{documentName}.yaml");
}

app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api/v1");
api.MapAuthEndpoints();
api.MapEvidenceEndpoints();
api.MapTransferEndpoints();

app.MapGet("/health", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect
        ? Results.Ok(new { status = "healthy" })
        : Results.Json(new { status = "unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable);
});

if (args.Contains("seed"))
{
    await EvidenceChain.Api.Seed.DeterministicSeeder.RunAsync(app.Services);
    return;
}

app.Run();

public partial class Program;
