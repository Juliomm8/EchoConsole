using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Api.Persistence;
using EchoConsole.Web.Models.Auth;
using EchoConsole.Web.Security;
using EchoConsole.Web.Services.Accounts;
using EchoConsole.Web.Services.Profile;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace EchoConsole.Web.Controllers;

public sealed class AuthController : Controller
{
    private static readonly TimeSpan OtpLifetime =
        TimeSpan.FromMinutes(5);

    private static readonly TimeSpan PasswordResetLifetime =
        TimeSpan.FromHours(2);

    private const string OtpEmailTempDataKey =
        "Auth:OtpEmail";

    private const string OtpCodeTempDataKey =
        "Auth:OtpCode";

    private const string OtpExpiresAtTempDataKey =
        "Auth:OtpExpiresAtUtc";

    private readonly EchoConsoleDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IUserSessionService _userSessionService;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly IOtpEmailSender _otpEmailSender;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AuthController(
        EchoConsoleDbContext dbContext,
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IUserSessionService userSessionService,
        IAuthenticationSchemeProvider schemeProvider,
        IOtpEmailSender otpEmailSender,
        TimeProvider timeProvider,
        ILogger<AuthController> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _signInManager = signInManager;
        _userSessionService = userSessionService;
        _schemeProvider = schemeProvider;
        _otpEmailSender = otpEmailSender;
        _timeProvider = timeProvider;
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
            var otpEmail = user.Email ?? normalizedEmail;

            var otpDispatched = await TryIssueOtpAsync(
                user,
                cancellationToken);

            TempData["OtpStatusType"] =
                otpDispatched ? "success" : "error";

            TempData["OtpStatusMessage"] =
                otpDispatched
                    ? "NUEVA SECUENCIA DESPACHADA"
                    : "NO FUE POSIBLE DESPACHAR LA SECUENCIA. SOLICITE UN REENVÍO.";

            return RedirectToAction(
                nameof(VerifyOtp),
                new
                {
                    email = otpEmail
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

        var otpDispatched = await TryIssueOtpAsync(
            user,
            cancellationToken);

        TempData["OtpStatusType"] =
            otpDispatched ? "success" : "error";

        TempData["OtpStatusMessage"] =
            otpDispatched
                ? "SECUENCIA DE AUTORIZACIÓN DESPACHADA"
                : "LA CUENTA FUE CREADA, PERO EL CORREO NO PUDO SER ENVIADO. SOLICITE UNA NUEVA SECUENCIA.";

        _logger.LogInformation(
            "User registered and awaits OTP verification. UserId={UserId}, Email={Email}, Alias={Alias}, OtpDispatched={OtpDispatched}.",
            user.Id,
            user.Email,
            user.Alias,
            otpDispatched);

        return RedirectToAction(
            nameof(VerifyOtp),
            new { email });
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(
                "Index",
                "Profile");
        }

        SetAuthTitle(
            "Auth_ForgotPasswordPageTitle");

        return View(
            new ForgotPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken = default)
    {
        SetAuthTitle(
            "Auth_ForgotPasswordPageTitle");

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail =
            model.Email.Trim();

        var user =
            await _userManager.FindByEmailAsync(
                normalizedEmail);

        if (user is not null &&
            !string.IsNullOrWhiteSpace(user.Email))
        {
            try
            {
                var resetToken =
                    await _userManager
                        .GeneratePasswordResetTokenAsync(
                            user);

                var resetPath = Url.Action(
                    nameof(ResetPassword),
                    "Auth");

                if (string.IsNullOrWhiteSpace(resetPath))
                {
                    throw new InvalidOperationException(
                        "The password reset path could not be generated.");
                }

                var resetBaseUrl =
                    $"{Request.Scheme}://{Request.Host}{Request.PathBase}{resetPath}";

                var resetUrl = QueryHelpers.AddQueryString(
                    resetBaseUrl,
                    new Dictionary<string, string?>
                    {
                        ["email"] = user.Email,
                        ["token"] = resetToken
                    });

                await _otpEmailSender
                    .SendPasswordResetAsync(
                        user,
                        resetUrl,
                        _timeProvider
                            .GetUtcNow()
                            .Add(
                                PasswordResetLifetime),
                        cancellationToken);

                _logger.LogInformation(
                    "Password reset email dispatched. UserId={UserId}, Email={Email}.",
                    user.Id,
                    user.Email);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Password reset email dispatch failed. Email={Email}.",
                    normalizedEmail);
            }
        }
        else
        {
            _logger.LogInformation(
                "Password reset requested for an unregistered email address.");
        }

        TempData["AuthStatusType"] =
            "success";

        TempData["AuthStatusMessage"] =
            _localizer[
                "Auth_PasswordResetDispatchGeneric"]
                .Value;

        return RedirectToAction(
            nameof(ForgotPassword));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(
        string? email,
        string? token)
    {
        SetAuthTitle(
            "Auth_ResetPasswordPageTitle");

        var model =
            new ResetPasswordViewModel
            {
                Email = email?.Trim() ??
                    string.Empty,
                Token = token ??
                    string.Empty
            };

        if (string.IsNullOrWhiteSpace(
                model.Email) ||
            string.IsNullOrWhiteSpace(
                model.Token))
        {
            ModelState.AddModelError(
                string.Empty,
                _localizer[
                    "Auth_ResetPasswordInvalidOrExpired"]);
        }

        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model,
        CancellationToken cancellationToken = default)
    {
        SetAuthTitle(
            "Auth_ResetPasswordPageTitle");

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail =
            model.Email.Trim();

        var user =
            await _userManager.FindByEmailAsync(
                normalizedEmail);

        if (user is null)
        {
            ModelState.AddModelError(
                string.Empty,
                _localizer[
                    "Auth_ResetPasswordInvalidOrExpired"]);

            return View(model);
        }

        var result =
            await _userManager.ResetPasswordAsync(
                user,
                model.Token,
                model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var message =
                    string.Equals(
                        error.Code,
                        "InvalidToken",
                        StringComparison.Ordinal)
                        ? _localizer[
                            "Auth_ResetPasswordInvalidOrExpired"]
                            .Value
                        : LocalizeIdentityError(error);

                ModelState.AddModelError(
                    string.Empty,
                    message);
            }

            return View(model);
        }

        var now =
            _timeProvider.GetUtcNow();

        await _dbContext.UserSessions
            .Where(session =>
                session.UserId == user.Id &&
                !session.RevokedAtUtc.HasValue)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        session =>
                            session.RevokedAtUtc,
                        now)
                    .SetProperty(
                        session =>
                            session.RevokedReason,
                        "PasswordReset"),
                cancellationToken);

        TempData["AuthStatusType"] =
            "success";

        TempData["AuthStatusMessage"] =
            _localizer[
                "Auth_ResetPasswordSucceeded"]
                .Value;

        _logger.LogInformation(
            "Password reset completed and tracked sessions revoked. UserId={UserId}, Email={Email}.",
            user.Id,
            user.Email);

        return RedirectToAction(
            nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp(
        string? email)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(
                "Index",
                "Profile");
        }

        SetAuthTitle("VERIFY TERMINAL");

        var normalizedEmail = email?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return RedirectToAction(
                nameof(Register));
        }

        var user = await _userManager.FindByEmailAsync(
            normalizedEmail);

        if (user is null)
        {
            return RedirectToAction(
                nameof(Register));
        }

        if (user.EmailConfirmed)
        {
            TempData["AuthStatusType"] = "success";
            TempData["AuthStatusMessage"] =
                "The account is already verified. You can sign in.";

            return RedirectToAction(
                nameof(Login));
        }

        var otpState = ReadOtpState();

        if (otpState is not null &&
            !string.Equals(
                otpState.Email,
                user.Email ?? normalizedEmail,
                StringComparison.OrdinalIgnoreCase))
        {
            otpState = null;
        }

        if (otpState is not null &&
            _timeProvider.GetUtcNow() >=
            otpState.ExpiresAtUtc)
        {
            ClearOtpState();
            otpState = null;

            TempData["OtpStatusType"] = "error";
            TempData["OtpStatusMessage"] =
                "CÓDIGO EXPIRADO. EL LLAVERO DE ACCESO CADUCÓ.";
        }

        return View(
            CreateVerifyOtpViewModel(
                user.Email ?? normalizedEmail,
                otpState?.ExpiresAtUtc));
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(
        VerifyOtpViewModel model,
        CancellationToken cancellationToken = default)
    {
        SetAuthTitle("VERIFY TERMINAL");

        var otpState = ReadOtpState();

        model.ExpiresAtUtc =
            otpState?.ExpiresAtUtc;

        model.ServerTimeUtc =
            _timeProvider.GetUtcNow();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim();
        var submittedCode = model.Code.Trim();

        if (otpState is null ||
            !string.Equals(
                otpState.Email,
                normalizedEmail,
                StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                nameof(model.Code),
                "CÓDIGO INVÁLIDO O EXPIRADO");

            return View(model);
        }

        var now = _timeProvider.GetUtcNow();

        if (now >= otpState.ExpiresAtUtc)
        {
            ClearOtpState();
            model.ExpiresAtUtc = null;

            ModelState.AddModelError(
                nameof(model.Code),
                "CÓDIGO EXPIRADO. EL LLAVERO DE ACCESO CADUCÓ.");

            return View(model);
        }

        if (!OtpCodesMatch(
            otpState.Code,
            submittedCode))
        {
            ModelState.AddModelError(
                nameof(model.Code),
                "CÓDIGO INVÁLIDO O EXPIRADO");

            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(
            normalizedEmail);

        if (user is null ||
            user.Status == UserStatus.Suspended)
        {
            ClearOtpState();

            ModelState.AddModelError(
                nameof(model.Code),
                "CÓDIGO INVÁLIDO O EXPIRADO");

            return View(model);
        }

        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;

            var updateResult =
                await _userManager.UpdateAsync(user);

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

        ClearOtpState();

        await _userSessionService.SignInAsync(
            HttpContext,
            user,
            isPersistent: false,
            cancellationToken);

        _logger.LogInformation(
            "Local account verified by OTP. UserId={UserId}, Email={Email}.",
            user.Id,
            user.Email);

        return RedirectToAction(
            "Index",
            "Profile");
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(
        string email,
        CancellationToken cancellationToken = default)
    {
        SetAuthTitle("VERIFY TERMINAL");

        var normalizedEmail = email?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return RedirectToAction(
                nameof(Register));
        }

        var user = await _userManager.FindByEmailAsync(
            normalizedEmail);

        if (user is null)
        {
            ClearOtpState();

            TempData["OtpStatusType"] = "error";
            TempData["OtpStatusMessage"] =
                "NO FUE POSIBLE DESPACHAR LA SECUENCIA.";

            return RedirectToAction(
                nameof(VerifyOtp),
                new { email = normalizedEmail });
        }

        if (user.EmailConfirmed)
        {
            ClearOtpState();

            TempData["AuthStatusType"] = "success";
            TempData["AuthStatusMessage"] =
                "The account is already verified. You can sign in.";

            return RedirectToAction(
                nameof(Login));
        }

        if (user.Status == UserStatus.Suspended)
        {
            ClearOtpState();

            TempData["OtpStatusType"] = "error";
            TempData["OtpStatusMessage"] =
                "LA CUENTA NO ESTÁ DISPONIBLE PARA VERIFICACIÓN.";

            return RedirectToAction(
                nameof(VerifyOtp),
                new { email = normalizedEmail });
        }

        var otpDispatched = await TryIssueOtpAsync(
            user,
            cancellationToken);

        TempData["OtpStatusType"] =
            otpDispatched ? "success" : "error";

        TempData["OtpStatusMessage"] =
            otpDispatched
                ? "NUEVA SECUENCIA DESPACHADA"
                : "NO FUE POSIBLE DESPACHAR LA SECUENCIA.";

        return RedirectToAction(
            nameof(VerifyOtp),
            new
            {
                email = user.Email ??
                    normalizedEmail
            });
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

    private VerifyOtpViewModel CreateVerifyOtpViewModel(
        string email,
        DateTimeOffset? expiresAtUtc)
    {
        return new VerifyOtpViewModel
        {
            Email = email,
            ExpiresAtUtc = expiresAtUtc,
            ServerTimeUtc =
                _timeProvider.GetUtcNow()
        };
    }

    private async Task<bool> TryIssueOtpAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var email = user.Email?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            ClearOtpState();
            return false;
        }

        var code = CreateOtpCode();
        var expiresAtUtc =
            _timeProvider.GetUtcNow().Add(OtpLifetime);

        try
        {
            await _otpEmailSender.SendOtpAsync(
                user,
                code,
                expiresAtUtc,
                cancellationToken);

            StoreOtpState(
                email,
                code,
                expiresAtUtc);

            _logger.LogInformation(
                "OTP dispatched. UserId={UserId}, Email={Email}, ExpiresAtUtc={ExpiresAtUtc}.",
                user.Id,
                email,
                expiresAtUtc);

            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            ClearOtpState();
            throw;
        }
        catch (Exception exception)
        {
            ClearOtpState();

            _logger.LogError(
                exception,
                "OTP dispatch failed. UserId={UserId}, Email={Email}.",
                user.Id,
                email);

            return false;
        }
    }

    private static string CreateOtpCode()
    {
        return RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString(
                "D6",
                CultureInfo.InvariantCulture);
    }

    private void StoreOtpState(
        string email,
        string code,
        DateTimeOffset expiresAtUtc)
    {
        TempData[OtpEmailTempDataKey] =
            email.Trim();

        TempData[OtpCodeTempDataKey] =
            code;

        TempData[OtpExpiresAtTempDataKey] =
            expiresAtUtc
                .ToUnixTimeSeconds()
                .ToString(
                    CultureInfo.InvariantCulture);
    }

    private OtpState? ReadOtpState()
    {
        var email = TempData
            .Peek(OtpEmailTempDataKey)
            ?.ToString();

        var code = TempData
            .Peek(OtpCodeTempDataKey)
            ?.ToString();

        var expiresAtText = TempData
            .Peek(OtpExpiresAtTempDataKey)
            ?.ToString();

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(code) ||
            !long.TryParse(
                expiresAtText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var expiresAtUnixSeconds))
        {
            return null;
        }

        return new OtpState(
            email,
            code,
            DateTimeOffset.FromUnixTimeSeconds(
                expiresAtUnixSeconds));
    }

    private void ClearOtpState()
    {
        TempData.Remove(
            OtpEmailTempDataKey);

        TempData.Remove(
            OtpCodeTempDataKey);

        TempData.Remove(
            OtpExpiresAtTempDataKey);
    }

    private static bool OtpCodesMatch(
        string expectedCode,
        string submittedCode)
    {
        var expectedBytes =
            System.Text.Encoding.UTF8.GetBytes(
                expectedCode);

        var submittedBytes =
            System.Text.Encoding.UTF8.GetBytes(
                submittedCode);

        return expectedBytes.Length ==
                submittedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(
                expectedBytes,
                submittedBytes);
    }

    private sealed record OtpState(
        string Email,
        string Code,
        DateTimeOffset ExpiresAtUtc);

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
