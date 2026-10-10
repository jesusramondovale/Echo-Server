using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Echo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReachBoost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReachBoost",
                table: "posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "post_boosts",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_boosts", x => new { x.PostId, x.UserId });
                    table.ForeignKey(
                        name: "FK_post_boosts_anonymous_users_UserId",
                        column: x => x.UserId,
                        principalTable: "anonymous_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_post_boosts_posts_PostId",
                        column: x => x.PostId,
                        principalTable: "posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_post_boosts_CreatedAt",
                table: "post_boosts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_post_boosts_UserId",
                table: "post_boosts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "post_boosts");

            migrationBuilder.DropColumn(
                name: "ReachBoost",
                table: "posts");
        }
    }
}
