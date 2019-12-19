using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Database.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    FaceitMatchId = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.FaceitMatchId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    SteamId = table.Column<long>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FaceitId = table.Column<string>(nullable: true),
                    FaceitName = table.Column<string>(nullable: true),
                    FaceitMembership = table.Column<int>(nullable: false),
                    LastChecked = table.Column<DateTime>(nullable: false),
                    Token = table.Column<string>(nullable: true),
                    TokenExpires = table.Column<DateTime>(nullable: false),
                    RefreshToken = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.SteamId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
