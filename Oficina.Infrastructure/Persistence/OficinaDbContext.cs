using Microsoft.EntityFrameworkCore;
using Oficina.Domain.Entities;
using Oficina.Domain.Enums;

namespace Oficina.Infrastructure.Persistence
{
    public class OficinaDbContext : DbContext
    {
        public OficinaDbContext(DbContextOptions<OficinaDbContext> options) : base(options)
        {
        }

        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<Veiculo> Veiculos => Set<Veiculo>();
        public DbSet<Servico> Servicos => Set<Servico>();
        public DbSet<PecaInsumo> PecasInsumos => Set<PecaInsumo>();
        public DbSet<OrdemServico> OrdensServico => Set<OrdemServico>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Nome).HasMaxLength(160).IsRequired();
                entity.Property(item => item.CpfCnpj).HasMaxLength(14).IsRequired();
                entity.Property(item => item.Telefone).HasMaxLength(30);
                entity.Property(item => item.Email).HasMaxLength(160);
                entity.HasIndex(item => item.CpfCnpj).IsUnique();
            });

            modelBuilder.Entity<Veiculo>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Placa).HasMaxLength(7).IsRequired();
                entity.Property(item => item.Marca).HasMaxLength(80).IsRequired();
                entity.Property(item => item.Modelo).HasMaxLength(80).IsRequired();
                entity.HasIndex(item => item.Placa).IsUnique();
                entity.HasOne(item => item.Cliente)
                    .WithMany(item => item.Veiculos)
                    .HasForeignKey(item => item.ClienteId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Servico>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Nome).HasMaxLength(120).IsRequired();
                entity.Property(item => item.Descricao).HasMaxLength(500);
                entity.Property(item => item.Preco).HasPrecision(12, 2);
            });

            modelBuilder.Entity<PecaInsumo>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Nome).HasMaxLength(120).IsRequired();
                entity.Property(item => item.Codigo).HasMaxLength(60).IsRequired();
                entity.Property(item => item.PrecoUnitario).HasPrecision(12, 2);
                entity.HasIndex(item => item.Codigo).IsUnique();
            });

            modelBuilder.Entity<OrdemServico>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Numero).HasMaxLength(40).IsRequired();
                entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(30);
                entity.Property(item => item.ValorTotal).HasPrecision(12, 2);
                entity.Property(item => item.Observacoes).HasMaxLength(1000);
                entity.HasIndex(item => item.Numero).IsUnique();
                entity.HasOne(item => item.Cliente)
                    .WithMany()
                    .HasForeignKey(item => item.ClienteId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(item => item.Veiculo)
                    .WithMany()
                    .HasForeignKey(item => item.VeiculoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OrdemServicoServico>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Nome).HasMaxLength(120).IsRequired();
                entity.Property(item => item.ValorUnitario).HasPrecision(12, 2);
                entity.HasOne(item => item.OrdemServico)
                    .WithMany(item => item.Servicos)
                    .HasForeignKey(item => item.OrdemServicoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.Servico)
                    .WithMany()
                    .HasForeignKey(item => item.ServicoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OrdemServicoPeca>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Nome).HasMaxLength(120).IsRequired();
                entity.Property(item => item.ValorUnitario).HasPrecision(12, 2);
                entity.HasOne(item => item.OrdemServico)
                    .WithMany(item => item.Pecas)
                    .HasForeignKey(item => item.OrdemServicoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.PecaInsumo)
                    .WithMany()
                    .HasForeignKey(item => item.PecaInsumoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Servico>().HasData(
                new Servico { Id = Guid.Parse("4ad293a3-4d16-4568-9ddf-b73c632e9d3a"), Nome = "Troca de oleo", Descricao = "Substituicao de oleo e filtro", Preco = 120, TempoEstimadoMinutos = 45 },
                new Servico { Id = Guid.Parse("5abf94fb-c1f6-432b-9e02-9c25513a08d8"), Nome = "Alinhamento", Descricao = "Alinhamento de direcao", Preco = 90, TempoEstimadoMinutos = 60 });

            modelBuilder.Entity<PecaInsumo>().HasData(
                new PecaInsumo { Id = Guid.Parse("90381d04-8e34-42d2-b428-532cbe152fd4"), Nome = "Filtro de oleo", Codigo = "FLT-OLEO-001", PrecoUnitario = 45, QuantidadeEstoque = 20, EstoqueMinimo = 5 },
                new PecaInsumo { Id = Guid.Parse("2aa4d11e-65b6-4ab1-bf89-457afc6c1127"), Nome = "Oleo 5W30", Codigo = "OLEO-5W30", PrecoUnitario = 38, QuantidadeEstoque = 50, EstoqueMinimo = 10 });

            base.OnModelCreating(modelBuilder);
        }
    }
}
