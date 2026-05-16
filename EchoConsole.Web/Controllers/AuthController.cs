using EchoConsole.Api.Domain.Entities;
using EchoConsole.Api.Domain.Enums;
using EchoConsole.Web.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoConsole.Web.Controllers;

public sealed class AuthController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["Title"] = "Login";
        ViewData["ReturnUrl"] = returnUrl;

        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Login";
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        if (user.Status == UserStatus.Suspended)
        {
            ModelState.AddModelError(string.Empty, "This account is suspended.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            model.Password,
            isPersistent: false,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("User logged in successfully. UserId: {UserId}, Email: {Email}, Role: {Role}", user.Id, user.Email, user.Role);

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            if (user.Role == UserRole.Admin)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["Title"] = "Register";
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Register";

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
            ModelState.AddModelError(nameof(model.Email), "This email is already registered.");
            return View(model);
        }

        var aliasAlreadyExists = await _userManager.Users
            .AnyAsync(x => x.Alias == alias, cancellationToken);

        if (aliasAlreadyExists)
        {
            ModelState.AddModelError(nameof(model.Alias), "This alias is already in use.");
            return View(model);
        }

        var user = new User
        {
            UserName = email,
            Email = email,
            Name = name,
            Alias = alias,
            Theme = "cyan",
            AvatarKey = "avatar-01",
            Role = UserRole.Viewer,
            Status = UserStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        _logger.LogInformation("User registered successfully. UserId: {UserId}, Email: {Email}, Alias: {Alias}", user.Id, user.Email, user.Alias);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutPost()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Access Denied";
        return View();
    }
}