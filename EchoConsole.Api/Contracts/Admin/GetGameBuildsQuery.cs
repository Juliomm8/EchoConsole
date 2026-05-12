using System.ComponentModel.DataAnnotations;

namespace EchoConsole.Api.Contracts.Admin;

public sealed class GetGameBuildsQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    [MaxLength(64)]
    public string? Search { get; set; }
}