using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WasabiBot.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildTrackPlays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuildTrackPlays",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Artist = table.Column<string>(type: "text", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    ArtworkUrl = table.Column<string>(type: "text", nullable: false),
                    PlayCount = table.Column<long>(type: "bigint", nullable: false),
                    FirstPlayedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastPlayedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildTrackPlays", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildTrackPlays_GuildId_ExternalId",
                table: "GuildTrackPlays",
                columns: new[] { "GuildId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildTrackPlays_GuildId_PlayCount_LastPlayedAt",
                table: "GuildTrackPlays",
                columns: new[] { "GuildId", "PlayCount", "LastPlayedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildTrackPlays");
        }
    }
}
