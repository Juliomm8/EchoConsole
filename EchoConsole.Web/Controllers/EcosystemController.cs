using System.Diagnostics;
using System.Net.Http.Headers;
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

    private const int StreamBufferSize = 64 * 1024;

    private const string FailureTokenExpired = "TokenExpirado";
    private const string FailureAssetNotFound = "AssetNoEncontrado";
    private const string FailureGitHubUnavailable = "GitHubCaido";
    private const string FailureRateLimited = "CuotaGitHubAgotada";
    private const string FailureInvalidConfiguration = "ConfiguracionInvalida";
    private const string FailureInvalidResponse = "RespuestaGitHubInvalida";
    private const string FailureClientCancelled = "ClienteCancelo";
    private const string FailureStreamInterrupted = "StreamInterrumpido";
    private const string FailureUnexpected = "ErrorInesperado";

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
    public async Task<IActionResult> DownloadGoldenCat()
    {
        var requestId = HttpContext.TraceIdentifier;
        var requestAborted = HttpContext.RequestAborted;
        var attemptElapsed = Stopwatch.StartNew();

        _logger.LogInformation(
            "GoldenCat download attempt started. RequestId={RequestId}",
            requestId);

        try
        {
            var download = await _releaseService
                .OpenLatestStableInstallerAsync(requestAborted);

            return await StreamInstallerAsync(
                download,
                requestId,
                attemptElapsed,
                requestAborted);
        }
        catch (OperationCanceledException)
            when (requestAborted.IsCancellationRequested)
        {
            _logger.LogInformation(
                "GoldenCat download attempt ended before streaming. RequestId={RequestId}, FailureCategory={FailureCategory}, AttemptDurationMilliseconds={AttemptDurationMilliseconds}",
                requestId,
                FailureClientCancelled,
                attemptElapsed.ElapsedMilliseconds);

            return new EmptyResult();
        }
        catch (GitHubReleaseException exception)
        {
            var failureCategory = MapFailureCategory(
                exception.FailureKind);

            _logger.LogWarning(
                "GoldenCat download attempt failed before streaming. RequestId={RequestId}, FailureCategory={FailureCategory}, FailureKind={FailureKind}, UpstreamStatusCode={UpstreamStatusCode}, AttemptDurationMilliseconds={AttemptDurationMilliseconds}",
                requestId,
                failureCategory,
                exception.FailureKind,
                exception.UpstreamStatusCode,
                attemptElapsed.ElapsedMilliseconds);

            return RedirectToUnavailableLanding();
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unexpected GoldenCat download failure. RequestId={RequestId}, FailureCategory={FailureCategory}, ResponseStarted={ResponseStarted}, AttemptDurationMilliseconds={AttemptDurationMilliseconds}",
                requestId,
                FailureUnexpected,
                Response.HasStarted,
                attemptElapsed.ElapsedMilliseconds);

            if (Response.HasStarted)
            {
                HttpContext.Abort();
                return new EmptyResult();
            }

            return RedirectToUnavailableLanding();
        }
    }

    private async Task<IActionResult> StreamInstallerAsync(
        GitHubReleaseDownload download,
        string requestId,
        Stopwatch attemptElapsed,
        CancellationToken requestAborted)
    {
        await using var ownedDownload = download;
        var streamElapsed = Stopwatch.StartNew();
        var countingStream = new CountingReadStream(download.Content);

        ConfigureDownloadResponse(download);

        using var cancellationRegistration = requestAborted.Register(
            static state =>
            {
                var activeDownload =
                    (GitHubReleaseDownload?)state;

                try
                {
                    activeDownload?.Dispose();
                }
                catch (Exception)
                {
                }
            },
            download);

        _logger.LogInformation(
            "GoldenCat download stream started. RequestId={RequestId}, ReleaseTag={ReleaseTag}, FileName={FileName}, ContentLength={ContentLength}",
            requestId,
            download.ReleaseTag,
            download.FileName,
            download.ContentLength);

        try
        {
            await countingStream.CopyToAsync(
                Response.Body,
                StreamBufferSize,
                requestAborted);

            if (countingStream.BytesRead != download.ContentLength)
            {
                throw new EndOfStreamException(
                    "The upstream installer stream ended before the declared content length was reached.");
            }

            await Response.Body.FlushAsync(requestAborted);

            _logger.LogInformation(
                "GoldenCat download completed successfully. RequestId={RequestId}, ReleaseTag={ReleaseTag}, FileName={FileName}, BytesStreamed={BytesStreamed}, StreamDurationMilliseconds={StreamDurationMilliseconds}, AttemptDurationMilliseconds={AttemptDurationMilliseconds}",
                requestId,
                download.ReleaseTag,
                download.FileName,
                countingStream.BytesRead,
                streamElapsed.ElapsedMilliseconds,
                attemptElapsed.ElapsedMilliseconds);

            return new EmptyResult();
        }
        catch (OperationCanceledException)
            when (requestAborted.IsCancellationRequested)
        {
            LogClientCancellation(
                requestId,
                download,
                countingStream.BytesRead,
                streamElapsed,
                attemptElapsed);

            return new EmptyResult();
        }
        catch (Exception exception)
            when (requestAborted.IsCancellationRequested &&
                  exception is IOException or ObjectDisposedException)
        {
            LogClientCancellation(
                requestId,
                download,
                countingStream.BytesRead,
                streamElapsed,
                attemptElapsed);

            return new EmptyResult();
        }
        catch (Exception exception)
            when (exception is IOException or
                  HttpRequestException or
                  ObjectDisposedException)
        {
            _logger.LogWarning(
                exception,
                "GoldenCat download stream failed. RequestId={RequestId}, FailureCategory={FailureCategory}, ReleaseTag={ReleaseTag}, FileName={FileName}, BytesStreamed={BytesStreamed}, ResponseStarted={ResponseStarted}, StreamDurationMilliseconds={StreamDurationMilliseconds}, AttemptDurationMilliseconds={AttemptDurationMilliseconds}",
                requestId,
                FailureStreamInterrupted,
                download.ReleaseTag,
                download.FileName,
                countingStream.BytesRead,
                Response.HasStarted,
                streamElapsed.ElapsedMilliseconds,
                attemptElapsed.ElapsedMilliseconds);

            if (!Response.HasStarted)
            {
                Response.Clear();
                return RedirectToUnavailableLanding();
            }

            HttpContext.Abort();
            return new EmptyResult();
        }
    }

    private void ConfigureDownloadResponse(
        GitHubReleaseDownload download)
    {
        var contentDisposition =
            new ContentDispositionHeaderValue("attachment")
            {
                FileName = $"\"{download.FileName}\"",
                FileNameStar = download.FileName
            };

        Response.ContentType = download.ContentType;
        Response.ContentLength = download.ContentLength;
        Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.ContentDisposition] =
            contentDisposition.ToString();
        Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.CacheControl] =
            "no-store, no-cache, max-age=0";
        Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Pragma] = "no-cache";
        Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Expires] = "0";
        Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.XContentTypeOptions] = "nosniff";
    }

    private void LogClientCancellation(
        string requestId,
        GitHubReleaseDownload download,
        long bytesStreamed,
        Stopwatch streamElapsed,
        Stopwatch attemptElapsed)
    {
        _logger.LogInformation(
            "GoldenCat download stream was cancelled by the client. RequestId={RequestId}, FailureCategory={FailureCategory}, ReleaseTag={ReleaseTag}, FileName={FileName}, BytesStreamed={BytesStreamed}, StreamDurationMilliseconds={StreamDurationMilliseconds}, AttemptDurationMilliseconds={AttemptDurationMilliseconds}",
            requestId,
            FailureClientCancelled,
            download.ReleaseTag,
            download.FileName,
            bytesStreamed,
            streamElapsed.ElapsedMilliseconds,
            attemptElapsed.ElapsedMilliseconds);
    }

    private static string MapFailureCategory(
        GitHubReleaseFailureKind failureKind)
    {
        return failureKind switch
        {
            GitHubReleaseFailureKind.Authentication =>
                FailureTokenExpired,
            GitHubReleaseFailureKind.NotFound =>
                FailureAssetNotFound,
            GitHubReleaseFailureKind.RateLimited =>
                FailureRateLimited,
            GitHubReleaseFailureKind.Timeout or
            GitHubReleaseFailureKind.Unavailable =>
                FailureGitHubUnavailable,
            GitHubReleaseFailureKind.Configuration =>
                FailureInvalidConfiguration,
            GitHubReleaseFailureKind.InvalidResponse =>
                FailureInvalidResponse,
            _ => FailureUnexpected
        };
    }

    private IActionResult RedirectToUnavailableLanding()
    {
        TempData[DownloadErrorTempDataKey] =
            DownloadUnavailableResourceKey;

        return RedirectToAction(nameof(Index));
    }

    private sealed class CountingReadStream : Stream
    {
        private readonly Stream _inner;
        private long _bytesRead;

        public CountingReadStream(Stream inner)
        {
            ArgumentNullException.ThrowIfNull(inner);
            _inner = inner;
        }

        public long BytesRead =>
            Interlocked.Read(ref _bytesRead);

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get => BytesRead;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            var read = _inner.Read(buffer, offset, count);
            AddBytes(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = _inner.Read(buffer);
            AddBytes(read);
            return read;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            var read = await _inner.ReadAsync(
                buffer,
                offset,
                count,
                cancellationToken);

            AddBytes(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = await _inner.ReadAsync(
                buffer,
                cancellationToken);

            AddBytes(read);
            return read;
        }

        public override long Seek(
            long offset,
            SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
        }

        private void AddBytes(int count)
        {
            if (count > 0)
            {
                Interlocked.Add(ref _bytesRead, count);
            }
        }
    }
}
