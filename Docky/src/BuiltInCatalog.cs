namespace DockyApp;

/// <summary>
/// The models and services that ship inside Docky.
/// <c>generate</c> builds compose files from it, and <c>list-models</c> /
/// <c>list-services</c> fall back to it when DockyHub cannot be reached.
/// </summary>
public static class BuiltInCatalog
{
    public record Model(string Name, string Description, string[] Services);

    public record Service(string Name, string Ports, string Description);

    public static readonly IReadOnlyList<Model> Models = new Model[]
    {
        new("base", "Basic development environment with PostgreSQL and PgAdmin",
            new[] { "postgresql", "pgadmin" }),
        new("full", "Full development stack with database, search, cache, and monitoring",
            new[] { "postgresql", "pgadmin", "elasticsearch", "kibana", "redis", "rabbitmq", "sonarqube" }),
        new("microservices", "Microservices architecture with API Gateway, Service Discovery, and Monitoring",
            new[] { "postgresql", "redis", "rabbitmq", "kong-gateway", "consul", "prometheus", "grafana", "jaeger" }),
        new("ai-ml", "Machine learning workspace with notebooks, experiment tracking, and object storage",
            new[] { "jupyter", "mlflow", "minio", "postgresql", "redis" }),
        new("frontend", "Frontend stack with Node.js, Nginx, cache, and database",
            new[] { "nodejs", "redis", "nginx", "postgresql" }),
        new("security", "Security tooling with secrets management, scanning, and monitoring",
            new[] { "vault", "owasp-zap", "prometheus", "grafana", "postgresql" }),
        new("analytics", "Streaming and analytics stack with Kafka, ClickHouse, and dashboards",
            new[] { "kafka", "zookeeper", "clickhouse", "grafana", "minio", "redis" }),
        new("mobile", "Mobile backend with database, cache, storage, messaging, and Firebase emulator",
            new[] { "postgresql", "redis", "minio", "rabbitmq", "firebase-admin" }),
        new("testing", "Test environment with Selenium Grid, API mocks, database, and cache",
            new[] { "selenium-hub", "selenium-chrome", "selenium-firefox", "wiremock", "postgresql", "redis" }),
        new("minimal", "Minimal setup with Redis and a SQLite web interface",
            new[] { "redis", "sqlite-web" }),
    };

    public static readonly IReadOnlyList<Service> Services = new Service[]
    {
        new("postgresql", "5432", "PostgreSQL Database"),
        new("pgadmin", "8080", "PgAdmin Web Interface"),
        new("elasticsearch", "9200, 9300", "Elasticsearch Search Engine"),
        new("kibana", "5601", "Kibana Dashboard"),
        new("redis", "6379", "Redis Cache"),
        new("rabbitmq", "5672, 15672", "RabbitMQ Message Broker"),
        new("sonarqube", "9000", "SonarQube Code Quality"),
        new("opensearch", "9200, 9600", "OpenSearch Search Engine"),
        new("kong-gateway", "8000, 8001", "Kong API Gateway"),
        new("consul", "8500", "Consul Service Discovery"),
        new("prometheus", "9090", "Prometheus Monitoring"),
        new("grafana", "3000", "Grafana Dashboard"),
        new("jaeger", "16686", "Jaeger Tracing"),
        new("jupyter", "8888", "Jupyter Notebook"),
        new("mlflow", "5000", "MLflow ML Platform"),
        new("minio", "9000, 9001", "MinIO Object Storage"),
        new("nodejs", "3000", "Node.js Runtime"),
        new("nginx", "80", "Nginx Web Server"),
        new("vault", "8200", "HashiCorp Vault"),
        new("owasp-zap", "8080", "OWASP ZAP Security Scanner"),
        new("kafka", "9092", "Apache Kafka"),
        new("zookeeper", "2181", "Apache Zookeeper"),
        new("clickhouse", "8123, 9000", "ClickHouse Analytics DB"),
        new("firebase-admin", "9099", "Firebase Admin Emulator"),
        new("selenium-hub", "4444", "Selenium Grid Hub"),
        new("selenium-chrome", "7900", "Selenium Chrome Node"),
        new("selenium-firefox", "7901", "Selenium Firefox Node"),
        new("wiremock", "8080", "WireMock API Mock"),
        new("sqlite-web", "8080", "SQLite Web Interface"),
    };

    public static Model? FindModel(string name) =>
        Models.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

    public static Service? FindService(string name) =>
        Services.FirstOrDefault(s => s.Name == name);

    public static string ModelNames => string.Join(", ", Models.Select(m => m.Name));

    /// <summary>The models in the shape DockyHub's <c>/api/models</c> returns.</summary>
    public static ModelInfo[] ToModelInfos() => Models
        .Select(m => new ModelInfo
        {
            Name = m.Name,
            Description = m.Description,
            Services = m.Services,
            TotalServices = m.Services.Length,
            Ports = m.Services
                .Select(s => FindService(s)?.Ports)
                .Where(p => p is not null)
                .SelectMany(p => p!.Split(", "))
                .Distinct()
                .ToArray(),
        })
        .ToArray();

    /// <summary>The services in the shape DockyHub's <c>/api/services</c> returns.</summary>
    public static ServiceInfo[] ToServiceInfos() => Services
        .Select(s => new ServiceInfo
        {
            Name = s.Name,
            Description = s.Description,
            Ports = s.Ports.Split(", "),
        })
        .ToArray();
}
