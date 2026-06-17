namespace EchoConsole.Api.Contracts.Profile;

public sealed class UserSessionEventPageDto
{
    public Guid SessionId { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage { get; set; }

    public bool HasNextPage { get; set; }

    public IReadOnlyList<UserSessionEventDto> Items { get; set; } =
        Array.Empty<UserSessionEventDto>();
}