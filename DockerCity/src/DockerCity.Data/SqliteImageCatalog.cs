using DockerCity.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace DockerCity.Data;

// Rules are read once and then matched in memory. The table is small and
// read constantly while drawing, so a round trip per service would be waste.
public sealed class SqliteImageCatalog : ImageCatalog
{
    private readonly IReadOnlyList<ImageMapping> _mappings;

    private SqliteImageCatalog(IReadOnlyList<ImageMapping> mappings) => _mappings = mappings;

    public override IReadOnlyList<ImageMapping> All => _mappings;

    public static async Task<SqliteImageCatalog> LoadAsync(
        DockerCityDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rows = await context.ImageMappings
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new SqliteImageCatalog(
        [
            .. rows.Select(row => new ImageMapping(
                row.Pattern,
                row.MatchMode,
                row.Category,
                row.DisplayName,
                row.IconFileName,
                row.Priority))
        ]);
    }
}
