using HackathonData.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace HackathonData

{
    public class HackathonDbContext : DbContext
    {
        public HackathonDbContext(DbContextOptions<HackathonDbContext> options)
            : base(options)
        {
        }

        public DbSet<Project> Projects { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Project>(b =>
            {
                b.HasKey(p => p.Id);
                b.Property(p => p.TeamName).IsRequired().HasMaxLength(100);
                b.Property(p => p.ProjectName).IsRequired().HasMaxLength(120);
                b.Property(p => p.Category).IsRequired().HasMaxLength(50);
                b.Property(p => p.EventDate).IsRequired().HasColumnType("date");
                b.Property(p => p.Score).IsRequired().HasColumnType("decimal(5,2)");
                b.Property(p => p.Members).IsRequired();
                b.Property(p => p.Captain).IsRequired().HasMaxLength(100);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
