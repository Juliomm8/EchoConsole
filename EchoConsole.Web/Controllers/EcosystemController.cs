using System.Diagnostics;
using EchoConsole.Web.Services.Releases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EchoConsole.Web.Controllers;

[AllowAnonymous]
[Route("studio/goldencat")]
public sealed class EcosystemController : Controller
{
    public const string DownloadErrorTempDataKey =
        "GoldenCatDownloadError";

    public const string DownloadErrorViewDataKey =
        "GoldenCatDownloadErrorResourceKey";

    public const string DownloadUnavailableResourceKey =
        "GoldenCatDownloadUnavailable";

    private readonly IGitHubReleaseService _releaseService;
    private readonly ILogger<EcosystemController> _logger;

    public EcosystemController(
        IGitHubReleaseService releaseService,
        ILogger<EcosystemController> logger)
    {
        _releaseService = releaseService;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var errorResourceKey =
            TempData[DownloadErrorTempDataKey] as string;

        ViewData[DownloadErrorViewDataKey] = errorResourceKey;
        ViewData["GoldenCatDownloadStatus"] =
            errorResourceKey is null ? null : "unavailable";

        return View();
    }

    [HttpGet("download", Name = "DownloadGoldenCat")]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public async Task<IActionResult> DownloadGoldenCat(
        CancellationToken cancellationToken)
    {
        var requestId = HttpContext.TraceIdentifier;
        var elapsed = Stopwatch.StartNew();
        GitHubReleaseDownload? download = null;

        _logger.LogInformation(
            "GoldenCat download attempt started. RequestId={RequestId}",
            requestId);

        try
        {
            download = await _releaseService
                .OpenLatestStableInstallerAsync(cancellationToken);

            Response.Headers["Cache-Control"] =
                "no-store, no-cache, max-age=0";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.ContentLength = download.ContentLength;

            var fileName = download.FileName;
            var contentLength = download.ContentLength;
            var releaseTag = download.ReleaseTag;

            Response.OnCompleted(() =>
            {
                _logger.LogInformation(
                    "GoldenCat download response completed. RequestId={RequestId}, ReleaseTag={ReleaseTag}, FileName={FileName}, ContentLength={ContentLength}, ClientAborted={ClientAborted}, ElapsedMilliseconds={ElapsedMilliseconds}",
                    requestId,
                    releaseTag,
                    fileName,
                    contentLength,
                    HttpContext.RequestAborted.IsCancellationRequested,
                    elapsed.ElapsedMilliseconds);

                return Task.CompletedTask;
            });

            var result = new FileStreamResult(
                download.Content,
                download.ContentType)
            {
                FileDownloadName = download.FileName,
                EnableRangeProcessing = false
            };

            download = null;

            _logger.LogInformation(
                "GoldenCat download stream opened. RequestId={RequestId}, ReleaseTag={ReleaseTag}, FileName={FileName}, ContentLength={ContentLength}",
                requestId,
                releaseTag,
                fileName,
                contentLength);

            return result;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "GoldenCat download attempt was cancelled by the client. RequestId={RequestId}, ElapsedMilliseconds={ElapsedMilliseconds}",
                requestId,
                elapsed.ElapsedMilliseconds);

            return new EmptyResult();
        }
        catch (GitHubReleaseException exception)
        {
            _logger.LogWarning(
                "GoldenCat download attempt failed before streaming. RequestId={RequestId}, FailureKind={FailureKind}, UpstreamStatusCode={UpstreamStatusCode}, ElapsedMilliseconds={ElapsedMilliseconds}",
                requestId,
                exception.FailureKind,
                exception.UpstreamStatusCode,
                elapsed.ElapsedMilliseconds);

            return RedirectToUnavailableLanding();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Unexpected GoldenCat download failure before streaming. RequestId={RequestId}, ExceptionType={ExceptionType}, ElapsedMilliseconds={ElapsedMilliseconds}",
                requestId,
                exception.GetType().Name,
                elapsed.ElapsedMilliseconds);

            return RedirectToUnavailableLanding();
        }
        finally
        {
            if (download is not null)
            {
                await download.DisposeAsync();
            }
        }
    }

    private IActionResult RedirectToUnavailableLanding()
    {
        TempData[DownloadErrorTempDataKey] =
            DownloadUnavailableResourceKey;

        return RedirectToAction(nameof(Index));
    }
}
