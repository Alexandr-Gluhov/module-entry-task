using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModuleEntryTask.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptAndEventsSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationId1",
                table: "SubmitIntents",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ToStatus",
                table: "OperationEvents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AlterColumn<string>(
                name: "ToStatus",
                table: "OperationEvents",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
