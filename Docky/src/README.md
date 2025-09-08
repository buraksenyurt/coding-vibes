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
