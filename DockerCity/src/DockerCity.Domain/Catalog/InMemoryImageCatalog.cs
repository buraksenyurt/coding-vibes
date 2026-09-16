namespace DockerCity.Domain.Catalog;

// The built-in rules. Also the source the database is seeded from, so the
// two never drift apart.
public sealed class InMemoryImageCatalog : ImageCatalog
{
    private const int ExactPriority = 100;
    private const int FamilyPriority = 10;

    private static readonly ImageMapping[] Table =
    [
        // Exact matches for images we know by name.
        new("postgres", MatchMode.Exact, ServiceCategory.Database, "PostgreSQL", "postgres.png", ExactPriority),
        new("mysql", MatchMode.Exact, ServiceCategory.Database, "MySQL", "mysql.png", ExactPriority),
        new("mariadb", MatchMode.Exact, ServiceCategory.Database, "MariaDB", "mariadb.png", ExactPriority),
        new("mongo", MatchMode.Exact, ServiceCategory.Database, "MongoDB", "mongo.png", ExactPriority),
        new("redis", MatchMode.Exact, ServiceCategory.Database, "Redis", "redis.png", ExactPriority),
        new("qdrant/qdrant", MatchMode.Exact, ServiceCategory.Database, "Qdrant", "qdrant.png", ExactPriority),
        new("rabbitmq", MatchMode.Exact, ServiceCategory.Messaging, "RabbitMQ", "rabbitmq.png", ExactPriority),
        new("nats", MatchMode.Exact, ServiceCategory.Messaging, "NATS", "nats.png", ExactPriority),
        new("minio/minio", MatchMode.Exact, ServiceCategory.Storage, "MinIO", "minio.png", ExactPriority),
        new("delfer/alpine-ftp-server", MatchMode.Exact, ServiceCategory.Storage, "FTP Server", "ftp.png", ExactPriority),
        new("keycloak/keycloak", MatchMode.Exact, ServiceCategory.Identity, "Keycloak", "keycloak.png", ExactPriority),
        new("dpage/pgadmin4", MatchMode.Exact, ServiceCategory.Tooling, "pgAdmin", "pgadmin.png", ExactPriority),
        new("sonarqube", MatchMode.Exact, ServiceCategory.Tooling, "SonarQube", "sonarqube.png", ExactPriority),
        new("nginx", MatchMode.Exact, ServiceCategory.Gateway, "Nginx", "nginx.png", ExactPriority),
        new("traefik", MatchMode.Exact, ServiceCategory.Gateway, "Traefik", "traefik.png", ExactPriority),

        // Family fallbacks for forks and vendor builds of the same engine.
        new("postgres", MatchMode.Contains, ServiceCategory.Database, "PostgreSQL", "postgres.png", FamilyPriority),
        new("mysql", MatchMode.Contains, ServiceCategory.Database, "MySQL", "mysql.png", FamilyPriority),
        new("redis", MatchMode.Contains, ServiceCategory.Database, "Redis", "redis.png", FamilyPriority),
        new("elasticsearch", MatchMode.Contains, ServiceCategory.Database, "Elasticsearch", "elasticsearch.png", FamilyPriority),
        new("kafka", MatchMode.Contains, ServiceCategory.Messaging, "Kafka", "kafka.png", FamilyPriority),
        new("rabbitmq", MatchMode.Contains, ServiceCategory.Messaging, "RabbitMQ", "rabbitmq.png", FamilyPriority),
        new("keycloak", MatchMode.Contains, ServiceCategory.Identity, "Keycloak", "keycloak.png", FamilyPriority),
        new("minio", MatchMode.Contains, ServiceCategory.Storage, "MinIO", "minio.png", FamilyPriority),
        new("nginx", MatchMode.Contains, ServiceCategory.Gateway, "Nginx", "nginx.png", FamilyPriority),
    ];

    public static IReadOnlyList<ImageMapping> Defaults => Table;

    public override IReadOnlyList<ImageMapping> All => Table;
}
