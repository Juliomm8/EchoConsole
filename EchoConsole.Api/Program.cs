using EchoConsole.Api.BackgroundServices;
using EchoConsole.Api.Hubs;
using EchoConsole.Api.Persistence;
using EchoConsole.Api.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services.AddDbContext<EchoConsoleDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddSignalR();
builder.Services.AddSingleton<SessionTokenService>();
builder.Services.AddHostedService<SessionPresenceWorker>();

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

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseCors("EchoConsoleCors");

app.MapControllers();
app.MapHub<TelemetryHub>("/hubs/telemetry");

app.Run();