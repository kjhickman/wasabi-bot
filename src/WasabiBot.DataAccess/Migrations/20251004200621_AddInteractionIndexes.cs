using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WasabiBot.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddInteractionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Interactions_GuildId",
                table: "Interactions",
                column: "GuildId",
                filter: "\"GuildId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_UserId",
                table: "Interactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Interactions_GuildId",
                table: "Interactions");

            migrationBuilder.DropIndex(
                name: "IX_Interactions_UserId",
                table: "Interactions");
        }
    }
}
