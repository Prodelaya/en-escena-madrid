using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnEscenaMadrid.Migrations
{
    /// <inheritdoc />
    public partial class EventosDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserEventos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    EventoId = table.Column<string>(type: "TEXT", nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: true),
                    Notas = table.Column<string>(type: "TEXT", nullable: true),
                    FechaAgregado = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaVisita = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEventos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserEventos_UserId",
                table: "UserEventos",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserEventos_UserId_EventoId",
                table: "UserEventos",
                columns: new[] { "UserId", "EventoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserEventos");
        }
    }
}
