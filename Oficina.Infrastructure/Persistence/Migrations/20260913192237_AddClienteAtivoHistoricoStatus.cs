using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable



namespace Oficina.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteAtivoHistoricoStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "OrdensServico",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Ativo",
                table: "Clientes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "HistoricoStatusOrdemServico",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrdemServicoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequencia = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IniciadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinalizadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegistradaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoStatusOrdemServico", x => x.Id);
                    table.CheckConstraint("CK_HistoricoStatus_Periodo", "\"FinalizadaEm\" IS NULL OR \"FinalizadaEm\" >= COALESCE(\"IniciadaEm\", \"RegistradaEm\")");
                    table.CheckConstraint("CK_HistoricoStatus_Sequencia", "\"Sequencia\" > 0");
                    table.ForeignKey(
                        name: "FK_HistoricoStatusOrdemServico_OrdensServico_OrdemServicoId",
                        column: x => x.OrdemServicoId,
                        principalTable: "OrdensServico",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoStatusOrdemServico_OrdemServicoId_Sequencia",
                table: "HistoricoStatusOrdemServico",
                columns: new[] { "OrdemServicoId", "Sequencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoStatusOrdemServico_Status_IniciadaEm",
                table: "HistoricoStatusOrdemServico",
                columns: new[] { "Status", "IniciadaEm" });

            // O estado atual das OS antigas e conhecido; a data de entrada nele nao.
            // Nao inventar duracoes nem excluir os servicos/pecas de seed preexistentes.
            migrationBuilder.Sql("""
                INSERT INTO "HistoricoStatusOrdemServico"
                    ("Id", "OrdemServicoId", "Sequencia", "Status", "IniciadaEm", "FinalizadaEm", "RegistradaEm")
                SELECT "Id", "Id", 1, "Status", NULL, NULL, CURRENT_TIMESTAMP
                FROM "OrdensServico";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricoStatusOrdemServico");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "OrdensServico");

            migrationBuilder.DropColumn(
                name: "Ativo",
                table: "Clientes");


        }
    }
}
