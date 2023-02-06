using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TokenBasedScript.Migrations
{
    /// <inheritdoc />
    public partial class ScriptExecutionProgress1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "InternalProgress",
                table: "ScriptExecutions",
                type: "float",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InternalProgress",
                table: "ScriptExecutions");
        }
    }
}
