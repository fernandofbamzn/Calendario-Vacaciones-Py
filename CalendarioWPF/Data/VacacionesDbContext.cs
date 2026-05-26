using Microsoft.EntityFrameworkCore;
using CalendarioWPF.Data.Entities;
using System.IO;
using System;

namespace CalendarioWPF.Data
{
    public class VacacionesDbContext : DbContext
    {
        public DbSet<PlanEntity> Planes { get; set; } = null!;
        public DbSet<TrabajadorEntity> Trabajadores { get; set; } = null!;
        public DbSet<VacacionEntity> Vacaciones { get; set; } = null!;
        public DbSet<ImputacionEntity> Imputaciones { get; set; } = null!;
        public DbSet<FestivoEntity> Festivos { get; set; } = null!;

        public string DbPath { get; }

        public VacacionesDbContext()
        {
            var folder = AppDomain.CurrentDomain.BaseDirectory;
            DbPath = Path.Join(folder, "vacaciones.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite($"Data Source={DbPath}");
            
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlanEntity>()
                .HasIndex(p => p.Year)
                .IsUnique();
        }
    }
}
