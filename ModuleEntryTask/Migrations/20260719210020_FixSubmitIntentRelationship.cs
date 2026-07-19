using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModuleEntryTask.Migrations
{
    /// <inheritdoc />
    public partial class FixSubmitIntentRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubmitIntents_Operations_OperationId1",
                table: "SubmitIntents");

            migrationBuilder.DropIndex(
                name: "IX_SubmitIntents_OperationId1",
                table: "SubmitIntents");

            migrationBuilder.DropColumn(
                name: "OperationId1",
                table: "SubmitIntents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationId1",
                table: "SubmitIntents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubmitIntents_OperationId1",
                table: "SubmitIntents",
                column: "OperationId1",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmitIntents_Operations_OperationId1",
                table: "SubmitIntents",
                column: "OperationId1",
                principalTable: "Operations",
                principalColumn: "Id");
        }
    }
}
