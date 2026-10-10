using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPushNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LastLat",
                table: "anonymous_users",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLocationAt",
                table: "anonymous_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastLon",
                table: "anonymous_users",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "push_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_push_tokens_anonymous_users_UserId",
                        column: x => x.UserId,
                        principalTable: "anonymous_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_anonymous_users_LastLocationAt",
                table: "anonymous_users",
                column: "LastLocationAt");

            migrationBuilder.CreateIndex(
                name: "IX_push_tokens_Token",
                table: "push_tokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_push_tokens_UserId",
                table: "push_tokens",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "push_tokens");

            migrationBuilder.DropIndex(
                name: "IX_anonymous_users_LastLocationAt",
                table: "anonymous_users");

            migrationBuilder.DropColumn(
                name: "LastLat",
                table: "anonymous_users");

            migrationBuilder.DropColumn(
                name: "LastLocationAt",
                table: "anonymous_users");

            migrationBuilder.DropColumn(
                name: "LastLon",
                table: "anonymous_users");
        }
    }
}
