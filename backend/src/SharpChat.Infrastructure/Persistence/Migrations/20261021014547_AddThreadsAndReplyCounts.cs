using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharpChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddThreadsAndReplyCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ParentMessageId",
                table: "Messages");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReplyAt",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastReplySeq",
                table: "Messages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplyCount",
                table: "Messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ParentMessageId_Seq",
                table: "Messages",
                columns: new[] { "ParentMessageId", "Seq" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_ParentMessageId_Seq",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "LastReplyAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "LastReplySeq",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReplyCount",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ParentMessageId",
                table: "Messages",
                column: "ParentMessageId");
        }
    }
}
