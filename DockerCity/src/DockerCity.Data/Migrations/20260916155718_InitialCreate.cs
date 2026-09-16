using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DockerCity.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "ComposeProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FileHash = table.Column<string>(type: "TEXT", nullable: true),
                    LastOpenedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComposeProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImageMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Pattern = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MatchMode = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IconFileName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLayouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ComposeProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    ServiceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: false),
                    Y = table.Column<double>(type: "REAL", nullable: false),
                    IsPinned = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceLayouts_ComposeProjects_ComposeProjectId",
                        column: x => x.ComposeProjectId,
                        principalTable: "ComposeProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ImageMappings",
                columns: new[] { "Id", "Category", "DisplayName", "IconFileName", "MatchMode", "Pattern", "Priority" },
                values: new object[,]
                {
                    { 1, 1, "PostgreSQL", "postgres.png", 0, "postgres", 100 },
                    { 2, 1, "MySQL", "mysql.png", 0, "mysql", 100 },
                    { 3, 1, "MariaDB", "mariadb.png", 0, "mariadb", 100 },
                    { 4, 1, "MongoDB", "mongo.png", 0, "mongo", 100 },
                    { 5, 1, "Redis", "redis.png", 0, "redis", 100 },
                    { 6, 1, "Qdrant", "qdrant.png", 0, "qdrant/qdrant", 100 },
                    { 7, 2, "RabbitMQ", "rabbitmq.png", 0, "rabbitmq", 100 },
                    { 8, 2, "NATS", "nats.png", 0, "nats", 100 },
                    { 9, 3, "MinIO", "minio.png", 0, "minio/minio", 100 },
                    { 10, 3, "FTP Server", "ftp.png", 0, "delfer/alpine-ftp-server", 100 },
                    { 11, 4, "Keycloak", "keycloak.png", 0, "keycloak/keycloak", 100 },
                    { 12, 5, "pgAdmin", "pgadmin.png", 0, "dpage/pgadmin4", 100 },
                    { 13, 5, "SonarQube", "sonarqube.png", 0, "sonarqube", 100 },
                    { 14, 6, "Nginx", "nginx.png", 0, "nginx", 100 },
                    { 15, 6, "Traefik", "traefik.png", 0, "traefik", 100 },
                    { 16, 1, "PostgreSQL", "postgres.png", 2, "postgres", 10 },
                    { 17, 1, "MySQL", "mysql.png", 2, "mysql", 10 },
                    { 18, 1, "Redis", "redis.png", 2, "redis", 10 },
                    { 19, 1, "Elasticsearch", "elasticsearch.png", 2, "elasticsearch", 10 },
                    { 20, 2, "Kafka", "kafka.png", 2, "kafka", 10 },
                    { 21, 2, "RabbitMQ", "rabbitmq.png", 2, "rabbitmq", 10 },
                    { 22, 4, "Keycloak", "keycloak.png", 2, "keycloak", 10 },
                    { 23, 3, "MinIO", "minio.png", 2, "minio", 10 },
                    { 24, 6, "Nginx", "nginx.png", 2, "nginx", 10 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComposeProjects_FilePath",
                table: "ComposeProjects",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageMappings_Pattern_MatchMode",
                table: "ImageMappings",
                columns: new[] { "Pattern", "MatchMode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLayouts_ComposeProjectId_ServiceName",
                table: "ServiceLayouts",
                columns: new[] { "ComposeProjectId", "ServiceName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "ImageMappings");

            migrationBuilder.DropTable(
                name: "ServiceLayouts");

            migrationBuilder.DropTable(
                name: "ComposeProjects");
        }
    }
}
