using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageSequenceAndReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId_SentAt_Id",
                table: "Messages");

            migrationBuilder.AddColumn<long>(
                name: "ChangeSeq",
                table: "Messages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Seq",
                table: "Messages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "LastDeliveredSeq",
                table: "ConversationMembers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "LastReadSeq",
                table: "ConversationMembers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
                UPDATE "Messages" AS m
                SET "Seq" = numbered.seq, "ChangeSeq" = numbered.seq
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "ConversationId" ORDER BY "SentAt", "Id") AS seq
                    FROM "Messages"
                ) AS numbered
                WHERE m."Id" = numbered."Id";
                """);

            migrationBuilder.Sql("""
                UPDATE "ConversationMembers" AS cm
                SET "LastDeliveredSeq" = latest.seq, "LastReadSeq" = latest.seq
                FROM (
                    SELECT "ConversationId", MAX("Seq") AS seq
                    FROM "Messages"
                    GROUP BY "ConversationId"
                ) AS latest
                WHERE cm."ConversationId" = latest."ConversationId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_ChangeSeq",
                table: "Messages",
                columns: new[] { "ConversationId", "ChangeSeq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_Seq",
                table: "Messages",
                columns: new[] { "ConversationId", "Seq" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId_ChangeSeq",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId_Seq",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ChangeSeq",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Seq",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "LastDeliveredSeq",
                table: "ConversationMembers");

            migrationBuilder.DropColumn(
                name: "LastReadSeq",
                table: "ConversationMembers");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_SentAt_Id",
                table: "Messages",
                columns: new[] { "ConversationId", "SentAt", "Id" });
        }
    }
}
