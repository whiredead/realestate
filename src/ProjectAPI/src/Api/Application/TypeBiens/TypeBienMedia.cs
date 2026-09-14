namespace ProjectAPI.Api.Application.TypeBiens;

/// <summary>
/// TypeBien.ImagesInterieur is a single comma-separated column, not a list —
/// every read path splits it on ',' (see ProjectRepository and
/// GetAllProjectsHandler). The write paths need the same split to validate each
/// URL, so it lives here once rather than being re-typed in two handlers.
/// </summary>
internal static class TypeBienMedia
{
    public static IEnumerable<string> SplitImages(string? packed) =>
        string.IsNullOrWhiteSpace(packed)
            ? Enumerable.Empty<string>()
            : packed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
