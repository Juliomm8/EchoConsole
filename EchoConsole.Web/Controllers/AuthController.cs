using System.Globalization;
using System.Security.Claims;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Web.Models.Auth;
using EchoConsole.Web.Security;
using EchoConsole.Web.Services.Profile;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace EchoConsole.Web.Controllers;

public sealed class AuthController : Controller
{
    private const string DevelopmentOtpCode = "123456";
    private const string OtpEmailTempDataKey = "Auth:OtpEmail";
    private const string OtpCodeTempDataKey = "Auth:OtpCode";

    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IUserSessionService _userSessionService;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AuthController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AuthController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IUserSessionService userSessionService,
        IAuthenticationSchemeProvider schemeProvider,
        IWebHostEnvironment environment,
        ILogger<AuthController> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _userSessionService = userSessionService;
        _schemeProvider = schemeProvider;
        _environment = environment;
        _logger = logger;
        _localizer = localizer;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        SetAuthTitle("Auth_LoginPageTitle");
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["GoogleLoginAvailable"] =
            await IsGoogleAuthenticationAvailableAsync();

        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        SetAuthTitle("Auth_LoginPageTitle");
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["GoogleLoginAvailable"] =
            await IsGoogleAuthenticationAvailableAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim();
        var user = await _userManager.FindByEmailAsync(
            normalizedEmail);

        if (user is null)
        {
            ModelState.AddModelError(
                string.Empty,
                _localizer["Auth_InvalidLogin"]);
            return View(model);
        }

        if (user.Status == UserStatus.Suspended)
        {
            ModelState.AddModelError(
                string.Empty,
                _localizer["Auth_AccountSuspended"]);
            return View(model);
        }

        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            model.Password,
            lockoutOnFailure: false);

        if (result.IsNotAllowed && !user.EmailConfirmed)
        {
            QueueDevelopmentOtp(user.Email ?? normalizedEmail);

            return RedirectToAction(
                nameof(VerifyOtp),
                new
                {
                    email = user.Email ?? normalizedEmail
                });
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                _localizer["Auth_InvalidLogin"]);
            return View(model);
        }

        await _userSessionService.SignInAsync(
            HttpContext,
            user,
            isPersistent: false,
            cancellationToken);

        _logger.LogInformation(
            "User logged in successfully. UserId={UserId}, Email={Email}, Role={Role}.",
            user.Id,
            user.Email,
            user.Role);

        return RedirectAfterSignIn(user, returnUrl);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalLogin(
        string provider,
        string? returnUrl = null)
    {
        if (!string.Equals(
            provider,
            GoogleDefaults.AuthenticationScheme,
            StringComparison.Ordinal))
        {
            return BadRequest("Unsupported external provider.");
        }

        if (!await IsGoogleAuthenticationAvailableAsync())
        {
            TempData["AuthStatusType"] = "error";
            TempData["AuthStatusMessage"] =
                "Google authentication is not configured for this environment.";

            return RedirectToAction(
                nameof(Login),
                new { returnUrl });
        }

        var callbackUrl = Url.Action(
            nameof(GoogleCallback),
            "Auth",
            new { returnUrl });

        var properties =
            _signInManager.ConfigureExternalAuthenticationProperties(
                GoogleDefaults.AuthenticationScheme,
                callbackUrl);

        return Challenge(
            properties,
            GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback(
        string? returnUrl = null,
        string? remoteError = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            _logger.LogWarning(
                "Google authentication returned an error: {RemoteError}",
                remoteError);

            TempData["AuthStatusType"] = "error";
            TempData["AuthStatusMessage"] =
                "Google authentication could not be completed.";

            return RedirectToAction(
                nameof(Login),
                new { returnUrl });
        }

        var loginInfo = await _signInManager
            .GetExternalLoginInfoAsync();

        if (loginInfo is null)
        {
            TempData["AuthStatusType"] = "error";
            TempData["AuthStatusMessage"] =
                "Google authentication data could not be read.";

            return RedirectToAction(
                nameof(Login),
                new { returnUrl });
        }

        var user = await _userManager.FindByLoginAsync(
            loginInfo.LoginProvider,
            loginInfo.ProviderKey);

        if (user is null)
        {
            var email = loginInfo.Principal
                .FindFirstValue(ClaimTypes.Email)
                ?.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["AuthStatusType"] = "error";
                TempData["AuthStatusMessage"] =
                    "Google did not provide an email address.";

                return RedirectToAction(
                    nameof(Login),
                    new { returnUrl });
            }

            var localAccount = await _userManager.FindByEmailAsync(
                email);

            if (localAccount is not null)
            {
                TempData["AuthStatusType"] = "error";
                TempData["AuthStatusMessage"] =
                    "An account already exists for this email. Sign in locally before linking Google from Security.";

                return RedirectToAction(
                    nameof(Login),
                    new { returnUrl });
            }

            user = await CreateGoogleUserAsync(
                loginInfo.Principal,
                email,
                cancellationToken);

            if (user is null)
            {
                TempData["AuthStatusType"] = "error";
                TempData["AuthStatusMessage"] =
                    "The player account could not be created from Google.";

                return RedirectToAction(
                    nameof(Login),
                    new { returnUrl });
            }

            var loginResult = await _userManager.AddLoginAsync(
                user,
                loginInfo);

            if (!loginResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                _logger.LogWarning(
                    "Google login could not be linked to newly created UserId={UserId}. Errors={Errors}",
                    user.Id,
                    string.Join(
                        "; ",
                        loginResult.Errors.Select(x => x.Description)));

                TempData["AuthStatusType"] = "error";
                TempData["AuthStatusMessage"] =
                    "The Google account could not be linked.";

                return RedirectToAction(
                    nameof(Login),
                    new { returnUrl });
            }
        }

        if (user.Status == UserStatus.Suspended)
        {
            TempData["AuthStatusType"] = "error";
            TempData["AuthStatusMessage"] =
                "This account is suspended.";

            return RedirectToAction(
                nameof(Login),
                new { returnUrl });
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                _logger.LogWarning(
                    "Google account auto-verification failed. UserId={UserId}, Errors={Errors}",
                    user.Id,
                    string.Join(
                        "; ",
                        updateResult.Errors.Select(x => x.Description)));

                TempData["AuthStatusType"] = "error";
                TempData["AuthStatusMessage"] =
                    "The Google account could not be activated.";

                return RedirectToAction(
                    nameof(Login),
                    new { returnUrl });
            }
        }

        await HttpContext.SignOutAsync(
            IdentityConstants.ExternalScheme);

        await _userSessionService.SignInAsync(
            HttpContext,
            user,
            isPersistent: false,
            cancellationToken);

        _logger.LogInformation(
            "Google authentication completed. UserId={UserId}, Email={Email}.",
            user.Id,
            user.Email);

        return RedirectToAction("Index", "Profile");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        SetAuthTitle("Auth_RegisterPageTitle");
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        RegisterViewModel model,
        CancellationToken cancellationToken)
    {
        SetAuthTitle("Auth_RegisterPageTitle");

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim();
        var alias = model.Alias.Trim();
        var name = model.Name.Trim();

        var existingUser = await _userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                _localizer["Auth_EmailAlreadyRegistered"]);
            return View(model);
        }

        var normalizedAlias = alias.ToUpperInvariant();

        var aliasAlreadyExists = await _userManager.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Alias.ToUpper() == normalizedAlias,
                cancellationToken);

        if (aliasAlreadyExists)
        {
            ModelState.AddModelError(
                nameof(model.Alias),
                _localizer["Auth_AliasAlreadyUsed"]);
            return View(model);
        }

        var user = new User
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            Name = name,
            Alias = alias,
            Theme = ProfileCatalog.DefaultTheme,
            AvatarKey = ProfileCatalog.DefaultAvatarKey,
            PreferredLanguage = ResolvePreferredLanguage(),
            Role = UserRole.Viewer,
            Status = UserStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(
            user,
            model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    LocalizeIdentityError(error));
            }

            return View(model);
        }

        QueueDevelopmentOtp(email);

        _logger.LogInformation(
            "User registered and awaits OTP verification. UserId={UserId}, Email={Email}, Alias={Alias}.",
            user.Id,
            user.Email,
            user.Alias);

        return RedirectToAction(
            nameof(VerifyOtp),
            new { email });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp(
        string? email)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Profile");
        }

        SetAuthTitle("VERIFY TERMINAL");

        var normalizedEmail = email?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return RedirectToAction(nameof(Register));
        }

        var user = await _userManager.FindByEmailAsync(
            normalizedEmail);

        if (user is null)
        {
            return RedirectToAction(nameof(Register));
        }

        if (user.EmailConfirmed)
        {
            TempData["AuthStatusType"] = "success";
            TempData["AuthStatusMessage"] =
                "The account is already verified. You can sign in.";

            return RedirectToAction(nameof(Login));
        }

        return View(new VerifyOtpViewModel
        {
            Email = user.Email ?? normalizedEmail
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(
        VerifyOtpViewModel model,
        CancellationToken cancellationToken = default)
    {
        SetAuthTitle("VERIFY TERMINAL");

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!_environment.IsDevelopment())
        {
            ModelState.AddModelError(
                nameof(model.Code),
                "CÓDIGO INVÁLIDO O EXPIRADO");

            return View(model);
        }

        var normalizedEmail = model.Email.Trim();
        var submittedCode = model.Code.Trim();

        var expectedEmail = TempData
            .Peek(OtpEmailTempDataKey)
            ?.ToString();

        var expectedCode = TempData
            .Peek(OtpCodeTempDataKey)
            ?.ToString();

        var emailMatches = string.Equals(
            expectedEmail,
            normalizedEmail,
            StringComparison.OrdinalIgnoreCase);

        var codeMatches = string.Equals(
            expectedCode,
            submittedCode,
            StringComparison.Ordinal);

        if (!emailMatches || !codeMatches)
        {
            ModelState.AddModelError(
                nameof(model.Code),
                "CÓDIGO INVÁLIDO O EXPIRADO");

            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(
            normalizedEmail);

        if (user is null || user.Status == UserStatus.Suspended)
        {
            ModelState.AddModelError(
                nameof(model.Code),
                "CÓDIGO INVÁLIDO O EXPIRADO");

            return View(model);
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;

            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        LocalizeIdentityError(error));
                }

                return View(model);
            }
        }

        TempData.Remove(OtpEmailTempDataKey);
        TempData.Remove(OtpCodeTempDataKey);

        await _userSessionService.SignInAsync(
            HttpContext,
            user,
            isPersistent: false,
            cancellationToken);

        _logger.LogInformation(
            "Local account verified by development OTP. UserId={UserId}, Email={Email}.",
            user.Id,
            user.Email);

        return RedirectToAction("Index", "Profile");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutPost(
        CancellationToken cancellationToken = default)
    {
        await _userSessionService.RevokeCurrentSessionAsync(
            HttpContext,
            "SignedOut",
            cancellationToken);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        SetAuthTitle("Auth_AccessDeniedPageTitle");
        return View();
    }

    private async Task<User?> CreateGoogleUserAsync(
        ClaimsPrincipal googlePrincipal,
        string email,
        CancellationToken cancellationToken)
    {
        var name = googlePrincipal.FindFirstValue(ClaimTypes.Name)
            ?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            name = email.Split('@')[0];
        }

        var aliasSeed = name.Replace(' ', '-');
        var alias = await CreateUniqueAliasAsync(
            aliasSeed,
            cancellationToken);

        var user = new User
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Name = name,
            Alias = alias,
            Theme = ProfileCatalog.DefaultTheme,
            AvatarKey = ProfileCatalog.DefaultAvatarKey,
            PreferredLanguage = ResolvePreferredLanguage(),
            Role = UserRole.Viewer,
            Status = UserStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user);

        if (createResult.Succeeded)
        {
            return user;
        }

        _logger.LogWarning(
            "Google player account creation failed. Email={Email}, Errors={Errors}",
            email,
            string.Join(
                "; ",
                createResult.Errors.Select(x => x.Description)));

        return null;
    }

    private async Task<string> CreateUniqueAliasAsync(
        string seed,
        CancellationToken cancellationToken)
    {
        var sanitized = new string(
            seed
                .Where(character =>
                    char.IsLetterOrDigit(character) ||
                    character is '-' or '_')
                .ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "Player";
        }

        sanitized = sanitized[..Math.Min(sanitized.Length, 24)];

        var candidate = sanitized;
        var suffix = 1;

        while (await _userManager.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.Alias.ToUpper() == candidate.ToUpper(),
                cancellationToken))
        {
            var suffixText = $"-{suffix}";
            var prefixLength = Math.Min(
                sanitized.Length,
                32 - suffixText.Length);

            candidate = $"{sanitized[..prefixLength]}{suffixText}";
            suffix++;
        }

        return candidate;
    }

    private void QueueDevelopmentOtp(string email)
    {
        TempData[OtpEmailTempDataKey] = email.Trim();
        TempData[OtpCodeTempDataKey] = DevelopmentOtpCode;

        _logger.LogInformation(
            "Development OTP generated. Email={Email}, Code={Code}.",
            email,
            DevelopmentOtpCode);
    }

    private async Task<bool> IsGoogleAuthenticationAvailableAsync()
    {
        return await _schemeProvider.GetSchemeAsync(
            GoogleDefaults.AuthenticationScheme) is not null;
    }

    private IActionResult RedirectAfterSignIn(
        User user,
        string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return user.Role == UserRole.Admin
            ? RedirectToAction("Index", "Dashboard")
            : RedirectToAction("Index", "Home");
    }

    private static string ResolvePreferredLanguage()
    {
        var language = CultureInfo
            .CurrentUICulture
            .TwoLetterISOLanguageName
            .ToLowerInvariant();

        return ProfileCatalog.IsAllowedLanguage(language)
            ? language
            : ProfileCatalog.DefaultLanguage;
    }

    private void SetAuthTitle(string resourceKey)
    {
        ViewData["Title"] = _localizer[resourceKey].Value;
        ViewData["TitleResourceKey"] = resourceKey;
    }

    private string LocalizeIdentityError(IdentityError error)
    {
        return error.Code switch
        {
            "PasswordTooShort" =>
                _localizer["Auth_PasswordTooShort"],
            "PasswordRequiresDigit" =>
                _localizer["Auth_PasswordRequiresDigit"],
            "PasswordRequiresLower" =>
                _localizer["Auth_PasswordRequiresLower"],
            "PasswordRequiresUpper" =>
                _localizer["Auth_PasswordRequiresUpper"],
            "PasswordRequiresNonAlphanumeric" =>
                _localizer["Auth_PasswordRequiresSymbol"],
            "DuplicateEmail" =>
                _localizer["Auth_EmailAlreadyRegistered"],
            "DuplicateUserName" =>
                _localizer["Auth_EmailAlreadyRegistered"],
            _ => _localizer["Auth_RegistrationFailed"]
        };
    }
}
