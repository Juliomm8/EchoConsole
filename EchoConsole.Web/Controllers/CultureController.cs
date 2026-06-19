using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[AllowAnonymous]
[Route("Culture")]
public sealed class CultureController : Controller
{
    private static readonly IReadOnlyDictionary<string, string>
        SupportedCultures =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "en",
                ["es"] = "es"
            };

    [HttpPost("Set")]
    [ValidateAntiForgeryToken]
    public IActionResult Set(
        string culture,
        string? returnUrl)
    {
        if (!SupportedCultures.TryGetValue(
                culture,
                out var normalizedCulture))
        {
            return BadRequest(new
            {
                message = "The selected culture is not supported."
            });
        }

        var requestCulture = new RequestCulture(
            normalizedCulture,
            normalizedCulture);

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(
                requestCulture),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Path = "/"
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(
            "Index",
            "Home");
    }
}