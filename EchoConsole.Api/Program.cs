using EchoConsole.Api.BackgroundServices;
using EchoConsole.Api.Hubs;
using EchoConsole.Api.Persistence;
using EchoConsole.Api.Security;
using EchoConsole.Api.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

builder.Services.AddSignalR();
builder.Services.AddSingleton<SessionTokenService>();
builder.Services.AddHostedService<SessionPresenceWorker>();
builder.Services.AddMemoryCache();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("client-ingest", limiterOptions =>
    {
        limiterOptions.PermitLimit = 300;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
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