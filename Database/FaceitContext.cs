using Entities.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace Database
{
    public class FaceitContext : DbContext
    {
        public FaceitContext()
        {
        }

        public FaceitContext(DbContextOptions<FaceitContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Match> Matches { get; set; }
        public virtual DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Match>(entity =>
            {
                entity.HasKey(e => new { e.FaceitMatchId });
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => new { e.SteamId });
            });

        }
    }
}
