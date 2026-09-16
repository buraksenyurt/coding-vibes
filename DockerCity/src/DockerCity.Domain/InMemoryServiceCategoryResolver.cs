using DockerCity.Domain.Values;

namespace DockerCity.Domain;

public sealed class InMemoryServiceCategoryResolver : IServiceCategoryResolver
{
    private static readonly (string Pattern, ServiceCategory Category)[] Table =
    [
        ("postgres",                     ServiceCategory.Database),
        ("mysql",                        ServiceCategory.Database),
        ("mariadb",                      ServiceCategory.Database),
        ("mongo",                        ServiceCategory.Database),
        ("redis",                        ServiceCategory.Database),
        ("qdrant/qdrant",                ServiceCategory.Database),
        ("rabbitmq",                     ServiceCategory.Messaging),
        ("nats",                         ServiceCategory.Messaging),
        ("minio/minio",                  ServiceCategory.Storage),
        ("delfer/alpine-ftp-server",     ServiceCategory.Storage),
        ("keycloak/keycloak",            ServiceCategory.Identity),
        ("dpage/pgadmin4",               ServiceCategory.Tooling),
        ("sonarqube",                    ServiceCategory.Tooling),
    ];

    public ServiceCategory Resolve(ImageRef image)
    {
        // Longest pattern wins so "redis" does not shadow "redis/redis-stack".
        foreach (var (pattern, category) in Table.OrderByDescending(row => row.Pattern.Length))
        {
            if (image.Repository.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                image.Repository.EndsWith('/' + pattern, StringComparison.OrdinalIgnoreCase))
            {
                return category;
            }
        }

        return ServiceCategory.Unknown;
    }
}