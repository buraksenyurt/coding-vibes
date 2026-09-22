using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DockerCity.Data.Migrations
{
    /// <inheritdoc />
    public partial class HideProjectsFromRecent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHiddenFromRecent",
                table: "ComposeProjects",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHiddenFromRecent",
                table: "ComposeProjects");
        }
    }
}
