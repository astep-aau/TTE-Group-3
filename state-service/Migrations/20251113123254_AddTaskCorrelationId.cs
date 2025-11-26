using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace stateservice.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskCorrelationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "Tasks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_CorrelationId",
                table: "Tasks",
                column: "CorrelationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_CorrelationId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "Tasks");
        }
    }
}
