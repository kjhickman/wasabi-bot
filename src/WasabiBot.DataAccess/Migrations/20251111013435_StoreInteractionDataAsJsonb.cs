using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WasabiBot.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class StoreInteractionDataAsJsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Interactions"" 
                ALTER COLUMN ""Data"" TYPE jsonb 
                USING ""Data""::jsonb;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Data",
                table: "Interactions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);
        }
    }
}
