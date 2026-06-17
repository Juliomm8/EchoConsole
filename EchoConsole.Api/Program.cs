using EchoConsole.Api.BackgroundServices;
using EchoConsole.Api.Configuration;
using EchoConsole.Api.Hubs;
using EchoConsole.Api.Persistence;
using EchoConsole.Api.Security;
using EchoConsole.Api.Seed;
using EchoConsole.Api.Services;
using EchoConsole.Api.Services.Ownership;
using EchoConsole.Api.Services.Profile;
using EchoConsole.Api.Services.SessionEventAnalytics;
using EchoConsole.Api.Services.SessionEvents;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

builder.Services.AddControllers();

// --- INYECCIÓN DE SEGURIDAD: API KEY (Server-to-Server) ---
builder.Services.AddAuthentication(AdminApiKeyAuthenticationOptions.SchemeName)
    .AddScheme<AdminApiKeyAuthenticationOptions, AdminApiKeyAuthenticationHandler>(
        AdminApiKeyAuthenticationOptions.SchemeName,
        options =>
        {
            options.ApiKey = builder.Configuration["AdminApiSecurity:ApiKey"] ?? string.Empty;
        });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminApiKeyAuthenticationOptions.AdminPolicy, policy =>
    {
        policy.AddAuthenticationSchemes(AdminApiKeyAuthenticationOptions.SchemeName);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("api_access", "admin");
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services.AddDbContext<EchoConsoleDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IInstallationOwnershipService, InstallationOwnershipService>();
builder.Services.AddSingleton<SessionTokenService>();
builder.Services.AddHostedService<SessionPresenceWorker>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IUserDashboardService, UserDashboardService>();
builder.Services.AddScoped<IUserSessionTimelineService, UserSessionTimelineService>();
builder.Services.AddScoped<IUserProfileSettingsService, UserProfileSettingsService>();
builder.Services.AddScoped<IAdminSessionEventsService, AdminSessionEventsService>();
builder.Services.AddScoped<IAdminSessionEventAnalyticsService, AdminSessionEventAnalyticsService>();

var sessionEventIngestionOptions = builder.Configuration
    .GetSection(SessionEventIngestionOptions.SectionName)
    .Get<SessionEventIngestionOptions>()
    ?? new SessionEventIngestionOptions();

sessionEventIngestionOptions.Validate();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        int? retryAfterSeconds = null;

        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out var retryAfter))
        {
            retryAfterSeconds = Math.Max(
                1,
                (int)Math.Ceiling(retryAfter.TotalSeconds));

            context.HttpContext.Response.Headers["Retry-After"] =
                retryAfterSeconds.Value.ToString(
                    CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                code = "rate_limit_exceeded",
                message = "The request rate limit has been exceeded.",
                retryAfterSeconds
            },
            cancellationToken);
    };

    options.AddFixedWindowLimiter(
        "client-ingest",
        limiterOptions =>
        {
            limiterOptions.PermitLimit = 300;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueLimit = 0;
            limiterOptions.QueueProcessingOrder =
                QueueProcessingOrder.OldestFirst;
            limiterOptions.AutoReplenishment = true;
        });

    options.AddPolicy(
        "session-events",
        httpContext =>
        {
            var routeSessionId =
                httpContext.Request.RouteValues["sessionId"]?.ToString();

            var remoteAddress =
                httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";

            var partitionKey = Guid.TryParse(
                routeSessionId,
                out var parsedSessionId)
                ? $"session:{parsedSessionId:N}"
                : $"invalid-session:{remoteAddress}";

            return RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey,
                _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit =
                        sessionEventIngestionOptions.PermitLimit,

                    Window = TimeSpan.FromSeconds(
                        sessionEventIngestionOptions.WindowSeconds),

                    SegmentsPerWindow =
                        sessionEventIngestionOptions.SegmentsPerWindow,

                    QueueLimit = 0,

                    QueueProcessingOrder =
                        QueueProcessingOrder.OldestFirst,

                    AutoReplenishment = true
                });
        });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("EchoConsoleCors", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddSingleton<IRealtimeApiKeyValidator, RealtimeApiKeyValidator>();
builder.Services.AddSingleton<TelemetryHubApiKeyFilter>();

builder.Services.AddSignalR(options =>
{
    options.AddFilter<TelemetryHubApiKeyFilter>();
});

// --- INYECCIÓN DEL SEEDER DE DATOS (Fake Data) ---
builder.Services.Configure<DemoSeedOptions>(
    builder.Configuration.GetSection(DemoSeedOptions.SectionName));

builder.Services.AddScoped<DevelopmentDataSeeder>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();
    await seeder.SeedAsync();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseCors("EchoConsoleCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TelemetryHub>("/hubs/telemetry");

app.Run();