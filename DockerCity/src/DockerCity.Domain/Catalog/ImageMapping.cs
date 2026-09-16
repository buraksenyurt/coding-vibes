using DockerCity.Domain.Values;

namespace DockerCity.Domain.Catalog;

// A single rule that ties an image repository to a category and an icon.
// Higher Priority wins; ties are broken by the longer pattern.
public sealed record ImageMapping(
    string Pattern,
    MatchMode MatchMode,
    ServiceCategory Category,
    string DisplayName,
    string IconFileName,
    int Priority = 0)
{
    public bool Matches(ImageRef image) => MatchMode switch
    {
        MatchMode.Exact => image.Repository.Equals(Pattern, StringComparison.OrdinalIgnoreCase),
        MatchMode.StartsWith => image.Repository.StartsWith(Pattern, StringComparison.OrdinalIgnoreCase),
        MatchMode.Contains => image.Repository.Contains(Pattern, StringComparison.OrdinalIgnoreCase),
        _ => false
    };
}
