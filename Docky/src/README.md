# DockyApp - Docker Compose Generator CLI Tool

DockyApp is a .NET CLI tool designed to easily generate docker-compose files for development environments.

## Features

- 🐳 Generate docker-compose files with predefined models
- 📦 Support for base and full development stack models
- 🔧 Add additional services like Redis, RabbitMQ, OpenSearch
- 📁 Import custom services from YAML files
- 🎨 Beautiful console output with Spectre.Console
- ⚙️ Configurable ports and settings

## Installation

### Prerequisites
- .NET 9.0 SDK

### Install as Global Tool

```bash
# Build the project
dotnet pack

# Install globally
dotnet tool install --global docky --add-source ./nupkg

# Or install locally
dotnet tool install --local docky --add-source ./nupkg
```

### Update Tool

```bash
dotnet tool update --global docky --add-source ./nupkg
```

### Uninstall Tool

```bash
dotnet tool uninstall --global docky
```

## Usage

### Basic Usage

```bash
# Generate base model (PostgreSQL + PgAdmin)
docky generate docker-compose --model base

# Generate full model (PostgreSQL, PgAdmin, Elasticsearch, Redis, RabbitMQ, Kibana, SonarQube)
docky generate docker-compose --model full

# Generate microservices model (API Gateway, Service Discovery, Monitoring)
docky generate docker-compose --model microservices

# Generate AI/ML model (Jupyter, MLflow, MinIO, PostgreSQL, Redis)
docky generate docker-compose --model ai-ml

# Generate analytics model (Kafka, ClickHouse, Grafana, MinIO, Redis)
docky generate docker-compose --model analytics

# Generate frontend development model
docky generate docker-compose --model frontend

# Generate security-focused model
docky generate docker-compose --model security

# Generate mobile backend model
docky generate docker-compose --model mobile

# Generate testing environment
docky generate docker-compose --model testing

# Generate minimal development environment
docky generate docker-compose --model minimal
```

### Add Additional Services

```bash
# Add Redis to base model
docky generate docker-compose --model base --add-redis

# Add RabbitMQ to base model
docky generate docker-compose --model base --add-rabbitmq

# Add OpenSearch to base model
docky generate docker-compose --model base --add-opensearch

# Customize Redis port
docky generate docker-compose --model base --add-redis --redis-port 6380
```

### Add Custom Services

```bash
# Add a custom service from YAML file
docky generate docker-compose --model base --add-service /path/to/minio-service.yml
```

### Specify Output Path

```bash
# Generate to custom path
docky generate docker-compose --model base --output ./docker/docker-compose.dev.yml
```

## Available Models

### Base Model
- **PostgreSQL** (Port: 5432)
- **PgAdmin** (Port: 8080)

### Full Model
- **PostgreSQL** (Port: 5432)
- **PgAdmin** (Port: 8080)
- **Elasticsearch** (Ports: 9200, 9300)
- **Kibana** (Port: 5601)
- **Redis** (Port: 6379)
- **RabbitMQ** (Ports: 5672, 15672)
- **SonarQube** (Port: 9000)

### Microservices Model
- **PostgreSQL** (Port: 5432)
- **Redis** (Port: 6379)
- **RabbitMQ** (Ports: 5672, 15672)
- **Kong API Gateway** (Ports: 8000, 8001)
- **Consul** (Port: 8500)
- **Prometheus** (Port: 9090)
- **Grafana** (Port: 3000)
- **Jaeger** (Port: 16686)

### AI-ML Model
- **Jupyter Notebook** (Port: 8888)
- **MLflow** (Port: 5000)
- **MinIO** (Ports: 9000, 9001)
- **PostgreSQL** (Port: 5432)
- **Redis** (Port: 6379)

### Frontend Model
- **Node.js** (Port: 3000)
- **Redis** (Port: 6379)
- **Nginx** (Port: 80)
- **PostgreSQL** (Port: 5432)

### Security Model
- **HashiCorp Vault** (Port: 8200)
- **OWASP ZAP** (Port: 8080)
- **Prometheus** (Port: 9090)
- **Grafana** (Port: 3000)
- **PostgreSQL** (Port: 5432)

### Analytics Model
- **Apache Kafka** (Port: 9092)
- **Apache Zookeeper** (Port: 2181)
- **ClickHouse** (Ports: 8123, 9000)
- **Grafana** (Port: 3000)
- **MinIO** (Ports: 9000, 9001)
- **Redis** (Port: 6379)

### Mobile Model
- **PostgreSQL** (Port: 5432)
- **Redis** (Port: 6379)
- **MinIO** (Ports: 9000, 9001)
- **RabbitMQ** (Ports: 5672, 15672)
- **Firebase Admin** (Port: 9099)

### Testing Model
- **Selenium Hub** (Port: 4444)
- **Selenium Chrome** (Port: 7900)
- **Selenium Firefox** (Port: 7901)
- **WireMock** (Port: 8080)
- **PostgreSQL** (Port: 5432)
- **Redis** (Port: 6379)

### Minimal Model
- **Redis** (Port: 6379)
- **SQLite Web** (Port: 8080)

## Additional Services

- **Redis** - In-memory data structure store
- **RabbitMQ** - Message broker
- **OpenSearch** - Search and analytics engine

## Default Credentials

- **PostgreSQL**: `docky_user` / `docky_pass`
- **PgAdmin**: `admin@docky.local` / `admin123`
- **RabbitMQ**: `docky_user` / `docky_pass`

## Network and Volumes

All services are connected to the `docky-network` bridge network and use named volumes for data persistence.

## Development

```bash
# Clone the repository
git clone <repository-url>
cd DockyApp

# Restore packages
dotnet restore

# Build the project
dotnet build

# Run locally
dotnet run -- generate docker-compose --model base

# Pack as tool
dotnet pack
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

This project is licensed under the MIT License.
