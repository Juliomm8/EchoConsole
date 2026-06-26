using System.Globalization;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Persistence;
using EchoConsole.Web;
using EchoConsole.Web.BackgroundServices;
using EchoConsole.Web.Hubs;
using EchoConsole.Web.Security;
using EchoConsole.Web.Services.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("es")
};

builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

builder.Services
    .AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (_, factory) =>
            factory.Create(typeof(SharedResource));
    });

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");

    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    options.RequestCultureProviders = new IRequestCultureProvider[]
    {
        new CookieRequestCultureProvider()
    };
});

var connectionString = builder.Configuration.GetConnectionString(
    "DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured.");
}

builder.Services.AddDbContext<EchoConsoleDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure();
    }));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSignalR();
builder.Services.AddHostedService<TelemetryRelayService>();

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

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            IdentityConstants.ApplicationScheme;

        options.DefaultChallengeScheme =
            IdentityConstants.ApplicationScheme;

        options.DefaultSignInScheme =
            IdentityConstants.ApplicationScheme;

        options.DefaultSignOutScheme =
            IdentityConstants.ApplicationScheme;
    })
    .AddCookie(
        IdentityConstants.ApplicationScheme,
        options =>
        {
            options.LoginPath = "/Auth/Login";
            options.AccessDeniedPath = "/Auth/AccessDenied";
            options.Cookie.Name = "EchoConsole.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;
        });

builder.Services.AddAuthorization();

builder.Services.AddScoped<
    IUserClaimsPrincipalFactory<User>,
    EchoConsoleUserClaimsPrincipalFactory>();

builder.Services.AddTransient<AdminApiKeyHandler>();

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"]
    ?? throw new InvalidOperationException(
        "ApiSettings:BaseUrl is not configured.");

if (!Uri.TryCreate(
        apiBaseUrl,
        UriKind.Absolute,
        out var apiBaseUri))
{
    throw new InvalidOperationException(
        "ApiSettings:BaseUrl must be an absolute URI.");
}

void ConfigureApiClient(HttpClient client)
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(10);
}

builder.Services.AddHttpClient(
    EchoConsoleApiClientNames.Public,
    (_, client) => ConfigureApiClient(client));

builder.Services
    .AddHttpClient(
        EchoConsoleApiClientNames.Admin,
        (_, client) => ConfigureApiClient(client))
    .AddHttpMessageHandler<AdminApiKeyHandler>();

builder.Services.AddScoped<EchoConsoleDashboardApiClient>();
builder.Services.AddScoped<EchoConsoleInstallationsApiClient>();
builder.Services.AddScoped<EchoConsoleBuildsApiClient>();
builder.Services.AddScoped<EchoConsoleAlertsApiClient>();
builder.Services.AddScoped<EchoConsoleUsersApiClient>();
builder.Services.AddScoped<EchoConsoleHomeApiClient>();
builder.Services.AddScoped<EchoConsoleProfileApiClient>();
builder.Services.AddScoped<EchoConsoleSessionEventsApiClient>();
builder.Services.AddScoped<EchoConsoleSessionEventAnalyticsApiClient>();
builder.Services.AddScoped<EchoConsoleLiveOperationsApiClient>();
builder.Services.AddScoped<EchoConsolePatchNotesApiClient>();
builder.Services.AddScoped<EchoConsoleSimulationApiClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
    app.UseResponseCompression();
}

var localizationOptions = app.Services
    .GetRequiredService<IOptions<RequestLocalizationOptions>>()
    .Value;

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (!app.Environment.IsDevelopment())
        {
            context.Context.Response.Headers.CacheControl =
                "public,max-age=604800";
        }
    }
});

app.UseRequestLocalization(localizationOptions);

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<AdminTelemetryHub>("/hubs/admin-telemetry");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();