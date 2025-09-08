using Spectre.Console;
using System.Text;

namespace DockyApp;

public class DockerComposeGenerator
{
    public void Generate(GenerationConfig config)
    {
        var services = new Dictionary<string, string>();

        // Model-based service configurations
        switch (config.Model.ToLower())
        {
            case "base":
                services.Add("postgresql", GetPostgreSQLService());
                services.Add("pgadmin", GetPgAdminService());
                break;

            case "full":
                services.Add("postgresql", GetPostgreSQLService());
                services.Add("pgadmin", GetPgAdminService());
                services.Add("elasticsearch", GetElasticsearchService());
                services.Add("kibana", GetKibanaService());
                services.Add("redis", GetRedisService(6379));
                services.Add("rabbitmq", GetRabbitMQService());
                services.Add("sonarqube", GetSonarQubeService());
                break;

            case "microservices":
                services.Add("postgresql", GetPostgreSQLService());
                services.Add("redis", GetRedisService(6379));
                services.Add("rabbitmq", GetRabbitMQService());
                services.Add("kong-gateway", GetKongService());
                services.Add("consul", GetConsulService());
                services.Add("prometheus", GetPrometheusService());
                services.Add("grafana", GetGrafanaService());
                services.Add("jaeger", GetJaegerService());
                break;

            case "ai-ml":
                services.Add("jupyter", GetJupyterService());
                services.Add("mlflow", GetMLflowService());
                services.Add("minio", GetMinIOService());
                services.Add("postgresql", GetPostgreSQLService());
                services.Add("redis", GetRedisService(6379));
                break;

            case "frontend":
                services.Add("nodejs", GetNodeJSService());
                services.Add("redis", GetRedisService(6379));
                services.Add("nginx", GetNginxService());
                services.Add("postgresql", GetPostgreSQLService());
                break;

            case "security":
                services.Add("vault", GetVaultService());
                services.Add("owasp-zap", GetOWASPZapService());
                services.Add("prometheus", GetPrometheusService());
                services.Add("grafana", GetGrafanaService());
                services.Add("postgresql", GetPostgreSQLService());
                break;

            case "analytics":
                services.Add("kafka", GetKafkaService());
                services.Add("zookeeper", GetZookeeperService());
                services.Add("clickhouse", GetClickHouseService());
                services.Add("grafana", GetGrafanaService());
                services.Add("minio", GetMinIOService());
                services.Add("redis", GetRedisService(6379));
                break;

            case "mobile":
                services.Add("postgresql", GetPostgreSQLService());
                services.Add("redis", GetRedisService(6379));
                services.Add("minio", GetMinIOService());
                services.Add("rabbitmq", GetRabbitMQService());
                services.Add("firebase-admin", GetFirebaseAdminService());
                break;

            case "testing":
                services.Add("selenium-hub", GetSeleniumHubService());
                services.Add("selenium-chrome", GetSeleniumChromeService());
                services.Add("selenium-firefox", GetSeleniumFirefoxService());
                services.Add("wiremock", GetWiremockService());
                services.Add("postgresql", GetPostgreSQLService());
                services.Add("redis", GetRedisService(6379));
                break;

            case "minimal":
                services.Add("redis", GetRedisService(6379));
                services.Add("sqlite-web", GetSQLiteWebService());
                break;

            default:
                throw new ArgumentException($"Unknown model: {config.Model}. Available models: base, full, microservices, ai-ml, frontend, security, analytics, mobile, testing, minimal");
        }

        // Additional services based on flags
        if (config.AddRedis && !services.ContainsKey("redis"))
        {
            services.Add("redis", GetRedisService(config.RedisPort));
        }

        if (config.AddRabbitMQ && !services.ContainsKey("rabbitmq"))
        {
            services.Add("rabbitmq", GetRabbitMQService());
        }

        if (config.AddOpenSearch && !services.ContainsKey("opensearch"))
        {
            services.Add("opensearch", GetOpenSearchService());
        }

        // Add custom service from file if provided
        if (!string.IsNullOrEmpty(config.AdditionalServicePath))
        {
            AddCustomService(services, config.AdditionalServicePath);
        }

        // Generate docker-compose content
        var dockerComposeContent = GenerateDockerComposeContent(services);

        // Write to file
        File.WriteAllText(config.OutputPath, dockerComposeContent);

        // Display summary
        DisplaySummary(services, config.OutputPath);
    }

    private void DisplaySummary(Dictionary<string, string> services, string outputPath)
    {
        var table = new Table();
        table.AddColumn("Service");
        table.AddColumn("Port(s)");
        table.AddColumn("Description");

        var serviceInfo = new Dictionary<string, (string ports, string description)>
        {
            ["postgresql"] = ("5432", "PostgreSQL Database"),
            ["pgadmin"] = ("8080", "PgAdmin Web Interface"),
            ["elasticsearch"] = ("9200, 9300", "Elasticsearch Search Engine"),
            ["kibana"] = ("5601", "Kibana Dashboard"),
            ["redis"] = ("6379", "Redis Cache"),
            ["rabbitmq"] = ("5672, 15672", "RabbitMQ Message Broker"),
            ["sonarqube"] = ("9000", "SonarQube Code Quality"),
            ["opensearch"] = ("9200, 9600", "OpenSearch Search Engine"),
            ["kong-gateway"] = ("8000, 8001", "Kong API Gateway"),
            ["consul"] = ("8500", "Consul Service Discovery"),
            ["prometheus"] = ("9090", "Prometheus Monitoring"),
            ["grafana"] = ("3000", "Grafana Dashboard"),
            ["jaeger"] = ("16686", "Jaeger Tracing"),
            ["jupyter"] = ("8888", "Jupyter Notebook"),
            ["mlflow"] = ("5000", "MLflow ML Platform"),
            ["minio"] = ("9000, 9001", "MinIO Object Storage"),
            ["nodejs"] = ("3000", "Node.js Runtime"),
            ["nginx"] = ("80", "Nginx Web Server"),
            ["vault"] = ("8200", "HashiCorp Vault"),
            ["owasp-zap"] = ("8080", "OWASP ZAP Security Scanner"),
            ["kafka"] = ("9092", "Apache Kafka"),
            ["zookeeper"] = ("2181", "Apache Zookeeper"),
            ["clickhouse"] = ("8123, 9000", "ClickHouse Analytics DB"),
            ["firebase-admin"] = ("9099", "Firebase Admin Emulator"),
            ["selenium-hub"] = ("4444", "Selenium Grid Hub"),
            ["selenium-chrome"] = ("7900", "Selenium Chrome Node"),
            ["selenium-firefox"] = ("7901", "Selenium Firefox Node"),
            ["wiremock"] = ("8080", "WireMock API Mock"),
            ["sqlite-web"] = ("8080", "SQLite Web Interface")
        };

        foreach (var service in services.Keys)
        {
            if (serviceInfo.ContainsKey(service))
            {
                var info = serviceInfo[service];
                table.AddRow(service, info.ports, info.description);
            }
            else
            {
                table.AddRow(service, "Custom", "Custom Service");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]📋 Generated Services:[/]");
        AnsiConsole.Write(table);
    }

    private string GenerateDockerComposeContent(Dictionary<string, string> services)
    {
        var sb = new StringBuilder();
        sb.AppendLine("version: '3.8'");
        sb.AppendLine();
        sb.AppendLine("services:");

        foreach (var service in services)
        {
            sb.AppendLine($"  {service.Key}:");
            var serviceLines = service.Value.Split('\n');
            foreach (var line in serviceLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine($"    {line}");
                }
            }
            sb.AppendLine();
        }

        // Add networks and volumes
        sb.AppendLine("networks:");
        sb.AppendLine("  docky-network:");
        sb.AppendLine("    driver: bridge");
        sb.AppendLine();
        sb.AppendLine("volumes:");
        sb.AppendLine("  postgres_data:");
        sb.AppendLine("  pgadmin_data:");
        if (services.ContainsKey("elasticsearch"))
        {
            sb.AppendLine("  elasticsearch_data:");
        }
        if (services.ContainsKey("sonarqube"))
        {
            sb.AppendLine("  sonarqube_data:");
            sb.AppendLine("  sonarqube_logs:");
            sb.AppendLine("  sonarqube_extensions:");
        }
        if (services.ContainsKey("opensearch"))
        {
            sb.AppendLine("  opensearch_data:");
        }
        if (services.ContainsKey("minio"))
        {
            sb.AppendLine("  minio_data:");
        }
        if (services.ContainsKey("consul"))
        {
            sb.AppendLine("  consul_data:");
        }
        if (services.ContainsKey("prometheus"))
        {
            sb.AppendLine("  prometheus_data:");
        }
        if (services.ContainsKey("grafana"))
        {
            sb.AppendLine("  grafana_data:");
        }
        if (services.ContainsKey("vault"))
        {
            sb.AppendLine("  vault_data:");
        }
        if (services.ContainsKey("kafka"))
        {
            sb.AppendLine("  kafka_data:");
        }
        if (services.ContainsKey("zookeeper"))
        {
            sb.AppendLine("  zookeeper_data:");
        }
        if (services.ContainsKey("clickhouse"))
        {
            sb.AppendLine("  clickhouse_data:");
        }
        if (services.ContainsKey("jupyter"))
        {
            sb.AppendLine("  jupyter_data:");
        }
        if (services.ContainsKey("mlflow"))
        {
            sb.AppendLine("  mlflow_data:");
        }
        if (services.ContainsKey("sqlite-web"))
        {
            sb.AppendLine("  sqlite_data:");
        }

        return sb.ToString();
    }

    private string GetPostgreSQLService()
    {
        return @"image: postgres:15
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
restart: unless-stopped";
    }

    private string GetPgAdminService()
    {
        return @"image: dpage/pgadmin4:latest
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
  - postgresql";
    }

    private string GetElasticsearchService()
    {
        return @"image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
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
restart: unless-stopped";
    }

    private string GetKibanaService()
    {
        return @"image: docker.elastic.co/kibana/kibana:8.11.0
container_name: docky_kibana
environment:
  - ELASTICSEARCH_HOSTS=http://elasticsearch:9200
ports:
  - ""5601:5601""
networks:
  - docky-network
restart: unless-stopped
depends_on:
  - elasticsearch";
    }

    private string GetRedisService(int port = 6379)
    {
        return $@"image: redis:7-alpine
container_name: docky_redis
ports:
  - ""{port}:6379""
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetRabbitMQService()
    {
        return @"image: rabbitmq:3-management-alpine
container_name: docky_rabbitmq
environment:
  RABBITMQ_DEFAULT_USER: docky_user
  RABBITMQ_DEFAULT_PASS: docky_pass
ports:
  - ""5672:5672""
  - ""15672:15672""
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetSonarQubeService()
    {
        return @"image: sonarqube:community
container_name: docky_sonarqube
environment:
  - SONAR_ES_BOOTSTRAP_CHECKS_DISABLE=true
ports:
  - ""9000:9000""
volumes:
  - sonarqube_data:/opt/sonarqube/data
  - sonarqube_logs:/opt/sonarqube/logs
  - sonarqube_extensions:/opt/sonarqube/extensions
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetOpenSearchService()
    {
        return @"image: opensearchproject/opensearch:latest
container_name: docky_opensearch
environment:
  - discovery.type=single-node
  - ""OPENSEARCH_JAVA_OPTS=-Xms512m -Xmx512m""
  - plugins.security.disabled=true
ports:
  - ""9200:9200""
  - ""9600:9600""
volumes:
  - opensearch_data:/usr/share/opensearch/data
networks:
  - docky-network
restart: unless-stopped";
    }

    // New service methods for additional models
    private string GetKongService()
    {
        return @"image: kong:latest
container_name: docky_kong
environment:
  KONG_DATABASE: ""off""
  KONG_DECLARATIVE_CONFIG: ""/kong/declarative/kong.yml""
  KONG_PROXY_ACCESS_LOG: ""/dev/stdout""
  KONG_ADMIN_ACCESS_LOG: ""/dev/stdout""
  KONG_PROXY_ERROR_LOG: ""/dev/stderr""
  KONG_ADMIN_ERROR_LOG: ""/dev/stderr""
  KONG_ADMIN_LISTEN: ""0.0.0.0:8001""
ports:
  - ""8000:8000""
  - ""8443:8443""
  - ""8001:8001""
  - ""8444:8444""
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetConsulService()
    {
        return @"image: consul:latest
container_name: docky_consul
command: agent -server -ui -node=server-1 -bootstrap-expect=1 -client=0.0.0.0
ports:
  - ""8500:8500""
  - ""8600:8600/udp""
volumes:
  - consul_data:/consul/data
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetPrometheusService()
    {
        return @"image: prom/prometheus:latest
container_name: docky_prometheus
command:
  - '--config.file=/etc/prometheus/prometheus.yml'
  - '--storage.tsdb.path=/prometheus'
  - '--web.console.libraries=/etc/prometheus/console_libraries'
  - '--web.console.templates=/etc/prometheus/consoles'
  - '--storage.tsdb.retention.time=200h'
  - '--web.enable-lifecycle'
ports:
  - ""9090:9090""
volumes:
  - prometheus_data:/prometheus
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetGrafanaService()
    {
        return @"image: grafana/grafana:latest
container_name: docky_grafana
environment:
  GF_SECURITY_ADMIN_USER: admin
  GF_SECURITY_ADMIN_PASSWORD: admin123
ports:
  - ""3000:3000""
volumes:
  - grafana_data:/var/lib/grafana
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetJaegerService()
    {
        return @"image: jaegertracing/all-in-one:latest
container_name: docky_jaeger
environment:
  COLLECTOR_ZIPKIN_HOST_PORT: "":9411""
ports:
  - ""5775:5775/udp""
  - ""6831:6831/udp""
  - ""6832:6832/udp""
  - ""5778:5778""
  - ""16686:16686""
  - ""14268:14268""
  - ""14250:14250""
  - ""9411:9411""
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetJupyterService()
    {
        return @"image: jupyter/datascience-notebook:latest
container_name: docky_jupyter
environment:
  JUPYTER_ENABLE_LAB: ""yes""
  JUPYTER_TOKEN: ""docker""
ports:
  - ""8888:8888""
volumes:
  - jupyter_data:/home/jovyan/work
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetMLflowService()
    {
        return @"image: python:3.11-slim
container_name: docky_mlflow
command: >
  bash -c ""pip install mlflow psycopg2-binary &&
           mlflow server --host 0.0.0.0 --port 5000
           --backend-store-uri postgresql://docky_user:docky_pass@postgresql:5432/docky_db
           --default-artifact-root ./mlruns""
ports:
  - ""5000:5000""
volumes:
  - mlflow_data:/mlruns
networks:
  - docky-network
restart: unless-stopped
depends_on:
  - postgresql";
    }

    private string GetMinIOService()
    {
        return @"image: minio/minio:latest
container_name: docky_minio
command: server /data --console-address "":9001""
environment:
  MINIO_ROOT_USER: docky_user
  MINIO_ROOT_PASSWORD: docky_pass123
ports:
  - ""9000:9000""
  - ""9001:9001""
volumes:
  - minio_data:/data
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetNodeJSService()
    {
        return @"image: node:18-alpine
container_name: docky_nodejs
working_dir: /app
command: sh -c ""npm install -g nodemon && tail -f /dev/null""
ports:
  - ""3000:3000""
volumes:
  - ./app:/app
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetNginxService()
    {
        return @"image: nginx:alpine
container_name: docky_nginx
ports:
  - ""80:80""
  - ""443:443""
volumes:
  - ./nginx.conf:/etc/nginx/nginx.conf
  - ./static:/usr/share/nginx/html
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetVaultService()
    {
        return @"image: vault:latest
container_name: docky_vault
cap_add:
  - IPC_LOCK
environment:
  VAULT_DEV_ROOT_TOKEN_ID: ""dev-token-123""
  VAULT_DEV_LISTEN_ADDRESS: ""0.0.0.0:8200""
ports:
  - ""8200:8200""
volumes:
  - vault_data:/vault/data
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetOWASPZapService()
    {
        return @"image: owasp/zap2docker-stable:latest
container_name: docky_zap
command: zap-webswing.sh
ports:
  - ""8080:8080""
  - ""8090:8090""
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetKafkaService()
    {
        return @"image: confluentinc/cp-kafka:latest
container_name: docky_kafka
environment:
  KAFKA_BROKER_ID: 1
  KAFKA_ZOOKEEPER_CONNECT: zookeeper:2181
  KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://localhost:9092
  KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
  KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 1
  KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 1
ports:
  - ""9092:9092""
volumes:
  - kafka_data:/var/lib/kafka/data
networks:
  - docky-network
restart: unless-stopped
depends_on:
  - zookeeper";
    }

    private string GetZookeeperService()
    {
        return @"image: confluentinc/cp-zookeeper:latest
container_name: docky_zookeeper
environment:
  ZOOKEEPER_CLIENT_PORT: 2181
  ZOOKEEPER_TICK_TIME: 2000
ports:
  - ""2181:2181""
volumes:
  - zookeeper_data:/var/lib/zookeeper/data
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetClickHouseService()
    {
        return @"image: clickhouse/clickhouse-server:latest
container_name: docky_clickhouse
environment:
  CLICKHOUSE_DB: docky_analytics
  CLICKHOUSE_USER: docky_user
  CLICKHOUSE_DEFAULT_ACCESS_MANAGEMENT: 1
  CLICKHOUSE_PASSWORD: docky_pass
ports:
  - ""8123:8123""
  - ""9000:9000""
volumes:
  - clickhouse_data:/var/lib/clickhouse
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetFirebaseAdminService()
    {
        return @"image: andreysenov/firebase-tools:latest
container_name: docky_firebase
command: firebase emulators:start --only auth,firestore --host 0.0.0.0
environment:
  FIREBASE_TOKEN: ""demo-token""
ports:
  - ""9099:9099""
  - ""8080:8080""
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetSeleniumHubService()
    {
        return @"image: selenium/hub:latest
container_name: docky_selenium_hub
ports:
  - ""4442:4442""
  - ""4443:4443""
  - ""4444:4444""
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetSeleniumChromeService()
    {
        return @"image: selenium/node-chrome:latest
container_name: docky_selenium_chrome
shm_size: 2gb
environment:
  HUB_HOST: selenium-hub
  HUB_PORT: 4444
ports:
  - ""7900:7900""
networks:
  - docky-network
restart: unless-stopped
depends_on:
  - selenium-hub";
    }

    private string GetSeleniumFirefoxService()
    {
        return @"image: selenium/node-firefox:latest
container_name: docky_selenium_firefox
shm_size: 2gb
environment:
  HUB_HOST: selenium-hub
  HUB_PORT: 4444
ports:
  - ""7901:7900""
networks:
  - docky-network
restart: unless-stopped
depends_on:
  - selenium-hub";
    }

    private string GetWiremockService()
    {
        return @"image: wiremock/wiremock:latest
container_name: docky_wiremock
command: --global-response-templating --verbose
ports:
  - ""8080:8080""
volumes:
  - ./wiremock:/home/wiremock
networks:
  - docky-network
restart: unless-stopped";
    }

    private string GetSQLiteWebService()
    {
        return @"image: coleifer/sqlite-web:latest
container_name: docky_sqlite_web
command: sqlite_web -H 0.0.0.0 -x /data/database.db
ports:
  - ""8080:8080""
volumes:
  - sqlite_data:/data
networks:
  - docky-network
restart: unless-stopped";
    }

    private void AddCustomService(Dictionary<string, string> services, string servicePath)
    {
        try
        {
            if (File.Exists(servicePath))
            {
                var customServiceContent = File.ReadAllText(servicePath);
                var serviceName = Path.GetFileNameWithoutExtension(servicePath);
                services.Add(serviceName, customServiceContent);
                AnsiConsole.MarkupLine($"[yellow]📁 Added custom service from: {servicePath}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]⚠️  Custom service file not found: {servicePath}[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Error adding custom service: {ex.Message}[/]");
        }
    }
}
