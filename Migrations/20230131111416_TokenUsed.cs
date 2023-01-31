using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TokenBasedScript.Migrations
{
    /// <inheritdoc />
    public partial class TokenUsed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TokenUsed",
                table: "ScriptExecutions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TokenUsed",
                table: "ScriptExecutions");
        }
    }
}
