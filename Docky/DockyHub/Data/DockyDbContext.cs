using DockyHub.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DockyHub.Data;

public class DockyDbContext : DbContext
{
    public DockyDbContext(DbContextOptions<DockyDbContext> options) : base(options)
    {
    }

    public DbSet<ServiceEntity> Services { get; set; }
    public DbSet<ModelEntity> Models { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ServiceEntity
        modelBuilder.Entity<ServiceEntity>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.DockerComposeContent).IsRequired();
        });

        // Configure ModelEntity
        modelBuilder.Entity<ModelEntity>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Services).IsRequired();
        });

        // Seed initial data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Seed Services
        modelBuilder.Entity<ServiceEntity>().HasData(
            new ServiceEntity
            {
                Id = 1,
                Name = "postgresql",
                Description = "PostgreSQL Database",
                Ports = "[\"5432\"]",
                Version = "15",
                Environment = "{\"POSTGRES_DB\":\"docky_db\",\"POSTGRES_USER\":\"docky_user\",\"POSTGRES_PASSWORD\":\"docky_pass\"}",
                Volumes = "[\"postgres_data:/var/lib/postgresql/data\"]",
                Networks = "[\"docky-network\"]",
                DockerComposeContent = @"image: postgres:15
container_name: docky_postgres
environment:
  POSTGRES_DB: docky_db
  POSTGRES_USER: docky_user
  POSTGRES_PASSWORD: docky_pass
ports:
  - ""5432:5432""
volumes:
  - postgres_data:/var/lib/postgresql/data
networks:
  - docky-network
restart: unless-stopped"
            },
            new ServiceEntity
            {
                Id = 2,
                Name = "pgadmin",
                Description = "PgAdmin Web Interface",
                Ports = "[\"8080\"]",
                Version = "latest",
                Environment = "{\"PGADMIN_DEFAULT_EMAIL\":\"admin@docky.local\",\"PGADMIN_DEFAULT_PASSWORD\":\"admin123\"}",
                Volumes = "[\"pgadmin_data:/var/lib/pgadmin\"]",
                Networks = "[\"docky-network\"]",
                DockerComposeContent = @"image: dpage/pgadmin4:latest
container_name: docky_pgadmin
environment:
  PGADMIN_DEFAULT_EMAIL: admin@docky.local
  PGADMIN_DEFAULT_PASSWORD: admin123
ports:
  - ""8080:80""
volumes:
  - pgadmin_data:/var/lib/pgadmin
networks:
  - docky-network
restart: unless-stopped
depends_on:
  - postgresql"
            },
            new ServiceEntity
            {
                Id = 3,
                Name = "redis",
                Description = "Redis Cache",
                Ports = "[\"6379\"]",
                Version = "7-alpine",
                Environment = "{}",
                Volumes = "[]",
                Networks = "[\"docky-network\"]",
                DockerComposeContent = @"image: redis:7-alpine
container_name: docky_redis
ports:
  - ""6379:6379""
networks:
  - docky-network
restart: unless-stopped"
            },
            new ServiceEntity
            {
                Id = 4,
                Name = "rabbitmq",
                Description = "RabbitMQ Message Broker",
                Ports = "[\"5672\",\"15672\"]",
                Version = "3-management-alpine",
                Environment = "{\"RABBITMQ_DEFAULT_USER\":\"docky_user\",\"RABBITMQ_DEFAULT_PASS\":\"docky_pass\"}",
                Volumes = "[]",
                Networks = "[\"docky-network\"]",
                DockerComposeContent = @"image: rabbitmq:3-management-alpine
container_name: docky_rabbitmq
environment:
  RABBITMQ_DEFAULT_USER: docky_user
  RABBITMQ_DEFAULT_PASS: docky_pass
ports:
  - ""5672:5672""
  - ""15672:15672""
networks:
  - docky-network
restart: unless-stopped"
            },
            new ServiceEntity
            {
                Id = 5,
                Name = "elasticsearch",
                Description = "Elasticsearch Search Engine",
                Ports = "[\"9200\",\"9300\"]",
                Version = "8.11.0",
                Environment = "{\"discovery.type\":\"single-node\",\"ES_JAVA_OPTS\":\"-Xms512m -Xmx512m\",\"xpack.security.enabled\":\"false\"}",
                Volumes = "[\"elasticsearch_data:/usr/share/elasticsearch/data\"]",
                Networks = "[\"docky-network\"]",
                DockerComposeContent = @"image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
container_name: docky_elasticsearch
environment:
  - discovery.type=single-node
  - ""ES_JAVA_OPTS=-Xms512m -Xmx512m""
  - xpack.security.enabled=false
ports:
  - ""9200:9200""
  - ""9300:9300""
volumes:
  - elasticsearch_data:/usr/share/elasticsearch/data
networks:
  - docky-network
restart: unless-stopped"
            }
        );

        // Seed Models
        modelBuilder.Entity<ModelEntity>().HasData(
            new ModelEntity
            {
                Id = 1,
                Name = "base",
                Description = "Basic development environment with PostgreSQL and PgAdmin",
                Services = "[\"postgresql\",\"pgadmin\"]",
                TotalServices = 2,
                Ports = "[\"5432\",\"8080\"]"
            },
            new ModelEntity
            {
                Id = 2,
                Name = "full",
                Description = "Full development stack with database, search, cache, and monitoring",
                Services = "[\"postgresql\",\"pgadmin\",\"elasticsearch\",\"redis\",\"rabbitmq\"]",
                TotalServices = 5,
                Ports = "[\"5432\",\"8080\",\"9200\",\"9300\",\"6379\",\"5672\",\"15672\"]"
            },
            new ModelEntity
            {
                Id = 3,
                Name = "microservices",
                Description = "Microservices architecture with API Gateway, Service Discovery, and Monitoring",
                Services = "[\"postgresql\",\"redis\",\"rabbitmq\"]",
                TotalServices = 3,
                Ports = "[\"5432\",\"6379\",\"5672\",\"15672\"]"
            }
        );
    }
}
