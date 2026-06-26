namespace EchoConsole.Web.Security;

public static class UserAgentParser
{
    public static UserAgentDescriptor Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return new UserAgentDescriptor(
                "Unknown browser",
                "Unknown system");
        }

        var browser = DetectBrowser(userAgent);
        var operatingSystem = DetectOperatingSystem(userAgent);

        return new UserAgentDescriptor(browser, operatingSystem);
    }

    private static string DetectBrowser(string userAgent)
    {
        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft Edge";
        }

        if (userAgent.Contains("OPR/", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("Opera", StringComparison.OrdinalIgnoreCase))
        {
            return "Opera";
        }

        if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase))
        {
            return "Mozilla Firefox";
        }

        if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("CriOS/", StringComparison.OrdinalIgnoreCase))
        {
            return "Google Chrome";
        }

        if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase))
        {
            return "Apple Safari";
        }

        return "Unknown browser";
    }

    private static string DetectOperatingSystem(string userAgent)
    {
        if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            return "Android";
        }

        if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
        {
            return "iOS";
        }

        if (userAgent.Contains("Windows NT", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows";
        }

        if (userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase))
        {
            return "macOS";
        }

        if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
        {
            return "Linux";
        }

        return "Unknown system";
    }
}
