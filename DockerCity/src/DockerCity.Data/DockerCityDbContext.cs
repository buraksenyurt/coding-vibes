using DockerCity.Data.Entities;
using DockerCity.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace DockerCity.Data;

public sealed class DockerCityDbContext(DbContextOptions<DockerCityDbContext> options)
    : DbContext(options)
{
    public DbSet<ImageMappingEntity> ImageMappings => Set<ImageMappingEntity>();
    public DbSet<ComposeProjectEntity> ComposeProjects => Set<ComposeProjectEntity>();
    public DbSet<ServiceLayoutEntity> ServiceLayouts => Set<ServiceLayoutEntity>();
    public DbSet<AppSettingEntity> AppSettings => Set<AppSettingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImageMappingEntity>(entity =>
        {
            entity.HasKey(mapping => mapping.Id);
            entity.Property(mapping => mapping.Pattern).HasMaxLength(200).IsRequired();
            entity.Property(mapping => mapping.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(mapping => mapping.IconFileName).HasMaxLength(100).IsRequired();
            entity.HasIndex(mapping => new { mapping.Pattern, mapping.MatchMode }).IsUnique();

            entity.HasData(SeedRows());
        });

        modelBuilder.Entity<ComposeProjectEntity>(entity =>
        {
            entity.HasKey(project => project.Id);
            entity.Property(project => project.Name).HasMaxLength(200).IsRequired();
            entity.Property(project => project.FilePath).HasMaxLength(1000).IsRequired();
            entity.HasIndex(project => project.FilePath).IsUnique();

            // SQLite has no date type: a DateTimeOffset lands as TEXT and cannot
            // be used in ORDER BY. Storing UTC ticks keeps the column sortable in
            // the database while callers still work with DateTimeOffset.
            entity.Property(project => project.LastOpenedAt)
                  .HasConversion(
                      value => value.UtcDateTime.Ticks,
                      value => new DateTimeOffset(value, TimeSpan.Zero));

            entity.HasMany(project => project.Layouts)
                  .WithOne(layout => layout.ComposeProject!)
                  .HasForeignKey(layout => layout.ComposeProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServiceLayoutEntity>(entity =>
        {
            entity.HasKey(layout => layout.Id);
            entity.Property(layout => layout.ServiceName).HasMaxLength(200).IsRequired();

            // One stored position per service inside a project.
            entity.HasIndex(layout => new { layout.ComposeProjectId, layout.ServiceName }).IsUnique();
        });

        modelBuilder.Entity<AppSettingEntity>(entity =>
        {
            entity.HasKey(setting => setting.Key);
            entity.Property(setting => setting.Key).HasMaxLength(100);
        });
    }

    // Seeded from the built-in catalog so the shipped rules and the stored
    // rules cannot disagree. Ids are positional, which keeps HasData stable
    // as long as rows are appended rather than reordered.
    private static IEnumerable<ImageMappingEntity> SeedRows() =>
        InMemoryImageCatalog.Defaults.Select((mapping, index) => new ImageMappingEntity
        {
            Id = index + 1,
            Pattern = mapping.Pattern,
            MatchMode = mapping.MatchMode,
            Category = mapping.Category,
            DisplayName = mapping.DisplayName,
            IconFileName = mapping.IconFileName,
            Priority = mapping.Priority
        });
}
