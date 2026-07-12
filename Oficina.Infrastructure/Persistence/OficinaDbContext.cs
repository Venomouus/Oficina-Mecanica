using Microsoft.EntityFrameworkCore;
using Oficina.Domain.Entities;

namespace Oficina.Infrastructure.Persistence
{
    public class OficinaDbContext : DbContext
    {
        public OficinaDbContext(DbContextOptions<OficinaDbContext> options)
            : base(options)
        {
        }

        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<Veiculo> Veiculos => Set<Veiculo>();
        public DbSet<Servico> Servicos => Set<Servico>();
        public DbSet<PecaInsumo> PecasInsumos => Set<PecaInsumo>();
        public DbSet<OrdemServico> OrdensServico => Set<OrdemServico>();
        public DbSet<OrdemServicoItem> OrdemServicoItens => Set<OrdemServicoItem>();
        public DbSet<OrdemServicoPeca> OrdemServicoPecas => Set<OrdemServicoPeca>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigurarCliente(modelBuilder);
            ConfigurarVeiculo(modelBuilder);
            ConfigurarServico(modelBuilder);
            ConfigurarPecaInsumo(modelBuilder);
            ConfigurarOrdemServico(modelBuilder);
            ConfigurarOrdemServicoItem(modelBuilder);
            ConfigurarOrdemServicoPeca(modelBuilder);
        }

        private static void ConfigurarCliente(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.HasKey(cliente => cliente.Id);

                entity.Property(cliente => cliente.Nome)
                    .HasMaxLength(160)
                    .IsRequired();

                entity.Property(cliente => cliente.CpfCnpj)
                    .HasMaxLength(14)
                    .IsRequired();

                entity.Property(cliente => cliente.Telefone)
                    .HasMaxLength(30);

                entity.Property(cliente => cliente.Email)
                    .HasMaxLength(160);

                entity.HasIndex(cliente => cliente.CpfCnpj)
                    .IsUnique();
            });
        }

        private static void ConfigurarVeiculo(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Veiculo>(entity =>
            {
                entity.HasKey(veiculo => veiculo.Id);

                entity.Property(veiculo => veiculo.Placa)
                    .HasMaxLength(7)
                    .IsRequired();

                entity.Property(veiculo => veiculo.Marca)
                    .HasMaxLength(80)
                    .IsRequired();

                entity.Property(veiculo => veiculo.Modelo)
                    .HasMaxLength(80)
                    .IsRequired();

                entity.HasIndex(veiculo => veiculo.Placa)
                    .IsUnique();

                entity.HasOne(veiculo => veiculo.Cliente)
                    .WithMany(cliente => cliente.Veiculos)
                    .HasForeignKey(veiculo => veiculo.ClienteId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigurarServico(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Servico>(entity =>
            {
                entity.HasKey(servico => servico.Id);

                entity.Property(servico => servico.Nome)
                    .HasMaxLength(120)
                    .IsRequired();

                entity.Property(servico => servico.Descricao)
                    .HasMaxLength(500);

                entity.Property(servico => servico.Preco)
                    .HasPrecision(12, 2);
            });
        }

        private static void ConfigurarPecaInsumo(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PecaInsumo>(entity =>
            {
                entity.HasKey(peca => peca.Id);

                entity.Property(peca => peca.Nome)
                    .HasMaxLength(120)
                    .IsRequired();

                entity.Property(peca => peca.Codigo)
                    .HasMaxLength(60)
                    .IsRequired();

                entity.Property(peca => peca.PrecoUnitario)
                    .HasPrecision(12, 2);

                entity.HasIndex(peca => peca.Codigo)
                    .IsUnique();
            });
        }

        private static void ConfigurarOrdemServico(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrdemServico>(entity =>
            {
                entity.HasKey(ordem => ordem.Id);

                entity.Property(ordem => ordem.Numero)
                    .HasMaxLength(40)
                    .IsRequired();

                entity.Property(ordem => ordem.Status)
                    .HasConversion<string>()
                    .HasMaxLength(30)
                    .IsRequired();

                entity.Property(ordem => ordem.ValorTotal)
                    .HasPrecision(12, 2);

                entity.Property(ordem => ordem.Observacoes)
                    .HasMaxLength(1000);

                entity.Property(ordem => ordem.MotivoRecusaOrcamento)
                    .HasMaxLength(500);

                entity.HasIndex(ordem => ordem.Numero)
                    .IsUnique();

                entity.HasOne(ordem => ordem.Cliente)
                    .WithMany()
                    .HasForeignKey(ordem => ordem.ClienteId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ordem => ordem.Veiculo)
                    .WithMany()
                    .HasForeignKey(ordem => ordem.VeiculoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigurarOrdemServicoItem(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrdemServicoItem>(entity =>
            {
                entity.ToTable("OrdemServicoServico");

                entity.HasKey(item => item.Id);

                entity.Property(item => item.Nome)
                    .HasMaxLength(120)
                    .IsRequired();

                entity.Property(item => item.ValorUnitario)
                    .HasPrecision(12, 2);

                entity.HasOne(item => item.OrdemServico)
                    .WithMany(ordem => ordem.Servicos)
                    .HasForeignKey(item => item.OrdemServicoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.Servico)
                    .WithMany()
                    .HasForeignKey(item => item.ServicoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigurarOrdemServicoPeca(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrdemServicoPeca>(entity =>
            {
                entity.ToTable("OrdemServicoPeca");

                entity.HasKey(item => item.Id);

                entity.Property(item => item.Nome)
                    .HasMaxLength(120)
                    .IsRequired();

                entity.Property(item => item.ValorUnitario)
                    .HasPrecision(12, 2);

                entity.HasOne(item => item.OrdemServico)
                    .WithMany(ordem => ordem.Pecas)
                    .HasForeignKey(item => item.OrdemServicoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(item => item.PecaInsumo)
                    .WithMany()
                    .HasForeignKey(item => item.PecaInsumoId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
