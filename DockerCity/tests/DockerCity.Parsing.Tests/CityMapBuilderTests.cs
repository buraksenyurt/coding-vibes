using DockerCity.Domain;
using DockerCity.Domain.Services;
using DockerCity.Parsing.Compose;
using DockerCity.Parsing.Dto;
using DockerCity.Parsing.Mapping;

namespace DockerCity.Parsing.Tests;

public class CityMapBuilderTests
{
    private static CityMap Sample() => BuildFrom(
        new ComposeFileReader().ReadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "docker-compose.yml")));

    private static CityMap BuildFromText(string yaml) =>
        BuildFrom(new ComposeFileReader().ReadFromText(yaml));

    private static CityMap BuildFrom(ComposeFileDto file) =>
        new CityMapBuilder(new ServiceFactory(new InMemoryServiceCategoryResolver())).Build(file);

    [Fact]
    public void Every_service_reaches_the_map()
    {
        Assert.Equal(10, Sample().Services.Count);
    }

    [Fact]
    public void Services_map_to_the_right_subtype()
    {
        var map = Sample();

        Assert.IsType<DatabaseService>(map.Find("postgres"));
        Assert.IsType<DatabaseService>(map.Find("redis"));
        Assert.IsType<DatabaseService>(map.Find("qdrant"));
        Assert.IsType<MessagingService>(map.Find("rabbitmq"));
        Assert.IsType<MessagingService>(map.Find("nats"));
        Assert.IsType<StorageService>(map.Find("minio"));
        Assert.IsType<StorageService>(map.Find("ftp-server"));
        Assert.IsType<IdentityService>(map.Find("keycloak"));
        Assert.IsType<ToolingService>(map.Find("pgadmin"));
        Assert.IsType<ToolingService>(map.Find("sonarqube"));
    }

    [Fact]
    public void Unknown_images_fall_back_to_the_generic_service()
    {
        const string yaml = """
            services:
              mystery:
                image: some-vendor/unheard-of:1.0
            """;

        var service = Assert.IsType<GenericService>(BuildFromText(yaml).Find("mystery"));
        Assert.Equal(ServiceCategory.Unknown, service.Category);
    }

    // --- The implicit default network ---

    [Fact]
    public void Two_districts_are_produced_and_one_is_implicit()
    {
        var districts = Sample().Districts;

        Assert.Equal(2, districts.Count);
        Assert.False(districts.Single(d => d.Name == "fnp-network").IsImplicit);
        Assert.True(districts.Single(d => d.Name == "default").IsImplicit);
    }

    [Fact]
    public void Services_without_a_network_join_the_implicit_district()
    {
        var members = Sample().Districts.Single(d => d.IsImplicit)
            .Members.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(new[] { "keycloak", "minio" }, members);
    }

    [Fact]
    public void Implicit_membership_stays_distinguishable_from_what_the_file_says()
    {
        var map = Sample();

        // The file does not mention a network for keycloak...
        Assert.Empty(map.Find("keycloak")!.NetworkNames);

        // ...but Compose semantics put it in the default network.
        Assert.Contains(
            map.Districts.Single(d => d.IsImplicit).Members,
            member => member.Name == "keycloak");
    }

    [Fact]
    public void An_explicitly_declared_default_network_is_not_implicit()
    {
        const string yaml = """
            services:
              alone:
                image: alpine
              wired:
                image: alpine
                networks:
                  - default
            networks:
              default:
                driver: bridge
            """;

        var district = Assert.Single(BuildFromText(yaml).Districts);

        Assert.Equal("default", district.Name);
        Assert.False(district.IsImplicit);
        Assert.Equal(2, district.Members.Count);
    }

    [Fact]
    public void Declared_network_holds_only_its_declared_members()
    {
        Assert.Equal(8, Sample().Districts.Single(d => d.Name == "fnp-network").Members.Count);
    }

    // --- Links ---

    [Fact]
    public void Depends_on_produces_a_directed_link()
    {
        var link = Assert.Single(Sample().Links.Where(l => l.Kind == CityLinkKind.DependsOn));

        Assert.Equal("pgadmin", link.From);
        Assert.Equal("postgres", link.To);
    }

    [Fact]
    public void No_shared_volume_link_when_every_volume_has_one_user()
    {
        Assert.Empty(Sample().Links.Where(l => l.Kind == CityLinkKind.SharedVolume));
    }

    [Fact]
    public void Shared_named_volume_produces_one_undirected_link()
    {
        const string yaml = """
            services:
              writer:
                image: alpine
                volumes:
                  - shared_data:/data
              reader:
                image: alpine
                volumes:
                  - shared_data:/data
            volumes:
              shared_data:
            """;

        var link = Assert.Single(
            BuildFromText(yaml).Links.Where(l => l.Kind == CityLinkKind.SharedVolume));

        Assert.Equal("reader", link.From);
        Assert.Equal("writer", link.To);
    }

    [Fact]
    public void Undefined_dependency_is_rejected()
    {
        const string yaml = """
            services:
              web:
                image: nginx
                depends_on:
                  - no-such-service
            """;

        Assert.Throws<InvalidOperationException>(() => { BuildFromText(yaml); });
    }

    // --- Subtype behaviour ---

    [Fact]
    public void Messaging_service_separates_broker_and_management_ports()
    {
        var rabbit = Assert.IsType<MessagingService>(Sample().Find("rabbitmq"));

        Assert.Equal(5672, rabbit.BrokerPort!.ContainerStart);
        Assert.Equal(15672, rabbit.WebPort!.ContainerStart);
        Assert.Equal(15672, rabbit.WebUrl!.Port);
    }

    [Fact]
    public void Storage_service_separates_api_and_console_ports()
    {
        var minio = Assert.IsType<StorageService>(Sample().Find("minio"));

        // 9008:9000 is the API, 9009:9001 is the browser console.
        Assert.Equal(9000, minio.ApiPort!.ContainerStart);
        Assert.Equal(9001, minio.WebPort!.ContainerStart);
        Assert.Equal(9009, minio.WebUrl!.Port);
    }

    [Fact]
    public void Storage_service_without_a_console_has_no_web_url()
    {
        var ftp = Assert.IsType<StorageService>(Sample().Find("ftp-server"));

        Assert.Null(ftp.WebPort);
        Assert.Null(ftp.WebUrl);
        Assert.True(ftp.HasPersistentData);
    }

    [Fact]
    public void Identity_and_tooling_services_expose_their_console()
    {
        var keycloak = Assert.IsType<IdentityService>(Sample().Find("keycloak"));
        var pgadmin = Assert.IsType<ToolingService>(Sample().Find("pgadmin"));

        Assert.Equal(8380, keycloak.WebUrl!.Port);
        Assert.Equal(5050, pgadmin.WebUrl!.Port);
    }

    [Fact]
    public void Persistent_data_is_detected()
    {
        Assert.True(Assert.IsType<DatabaseService>(Sample().Find("postgres")).HasPersistentData);
        Assert.False(Assert.IsType<DatabaseService>(Sample().Find("redis")).HasPersistentData);
    }

    [Fact]
    public void Supervised_service_is_flagged()
    {
        Assert.True(Sample().Find("qdrant")!.IsSupervised);
        Assert.False(Sample().Find("redis")!.IsSupervised);
    }

    [Fact]
    public void Sensitive_environment_values_are_masked()
    {
        var postgres = Sample().Find("postgres")!;
        var password = postgres.Environment.Single(e => e.Key == "POSTGRES_PASSWORD");

        Assert.True(password.IsSensitive);
        Assert.DoesNotContain("somew0rds", password.DisplayValue);
    }
}
