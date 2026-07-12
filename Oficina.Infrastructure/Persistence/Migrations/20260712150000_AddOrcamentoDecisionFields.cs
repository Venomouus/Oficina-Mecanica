using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Oficina.Infrastructure.Persistence;

#nullable disable

namespace Oficina.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(OficinaDbContext))]
    [Migration("20260712150000_AddOrcamentoDecisionFields")]
    public partial class AddOrcamentoDecisionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoRecusaOrcamento",
                table: "OrdensServico",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OrcamentoAprovado",
                table: "OrdensServico",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OrcamentoRespondidoEm",
                table: "OrdensServico",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotivoRecusaOrcamento",
                table: "OrdensServico");

            migrationBuilder.DropColumn(
                name: "OrcamentoAprovado",
                table: "OrdensServico");

            migrationBuilder.DropColumn(
                name: "OrcamentoRespondidoEm",
                table: "OrdensServico");
        }
    }
}