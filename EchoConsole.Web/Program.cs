using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence;
using EchoConsole.Web.Security;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
}

// --- CONFIGURACIÓN DE CONTEXTO ---
builder.Services.AddDbContext<EchoConsoleDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure();
    }));

builder.Services.AddSingleton(TimeProvider.System);

// --- CONFIGURACIÓN DE IDENTITY CORE ---
builder.Services
    .AddIdentityCore<User>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;

        options.SignIn.RequireConfirmedAccount = false;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddSignInManager<SignInManager<User>>()
    .AddEntityFrameworkStores<EchoConsoleDbContext>()
    .AddDefaultTokenProviders();

// --- CONFIGURACIÓN DE AUTENTICACIÓN ---
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignOutScheme = IdentityConstants.ApplicationScheme;
    })
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.Cookie.Name = "EchoConsole.Auth";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IUserClaimsPrincipalFactory<User>, EchoConsoleUserClaimsPrincipalFactory>();

// --- SEGURIDAD DE LA API ---
builder.Services.AddTransient<AdminApiKeyHandler>();

// --- CLIENTES HTTP ---
builder.Services.AddHttpClient("EchoConsoleApiPublic", (serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["ApiSettings:BaseUrl"]
        ?? throw new InvalidOperationException("ApiSettings:BaseUrl is not configured.");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient("EchoConsoleApiAdmin", (serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["ApiSettings:BaseUrl"]
        ?? throw new InvalidOperationException("ApiSettings:BaseUrl is not configured.");

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
})
.AddHttpMessageHandler<AdminApiKeyHandler>();

// --- SERVICIOS DE TELEMETRÍA ---
builder.Services.AddScoped<EchoConsoleDashboardApiClient>();
builder.Services.AddScoped<EchoConsoleInstallationsApiClient>();
builder.Services.AddScoped<EchoConsoleBuildsApiClient>();
builder.Services.AddScoped<EchoConsoleAlertsApiClient>();
builder.Services.AddScoped<EchoConsoleUsersApiClient>();
builder.Services.AddScoped<EchoConsoleHomeApiClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();