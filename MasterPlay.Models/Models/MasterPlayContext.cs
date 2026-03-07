using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace MasterPlay.Models.Models
{
    public partial class MasterPlayContext : DbContext
    {
        public MasterPlayContext() { }
        public MasterPlayContext(DbContextOptions<MasterPlayContext> options) : base(options) { }

        public virtual DbSet<DetectedProcess> DetectedProcesses { get; set; }
        public virtual DbSet<Game> Games { get; set; }
        public virtual DbSet<GamePosition> GamePositions { get; set; }
        public virtual DbSet<Genre> Genres { get; set; }
        public virtual DbSet<OwnershipStatus> OwnershipStatuses { get; set; }
        public virtual DbSet<ProgressStatus> ProgressStatuses { get; set; }
        public virtual DbSet<Series> Series { get; set; }
        public virtual DbSet<Theme> Themes { get; set; }
        public DbSet<GameGenre> GameGenres { get; set; }
        public DbSet<GameTheme> GameThemes { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbPath = JsonService.GetDatabasePath();
                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DetectedProcess>(entity =>
            {
                entity.HasIndex(e => e.FullPath).IsUnique();
            });

            modelBuilder.Entity<Game>(entity =>
            {
                entity.Property(e => e.AvgPlayTime).HasDefaultValue(0);
                entity.Property(e => e.Excited).HasDefaultValue(0.0);
                entity.Property(e => e.GImagePath).HasColumnName("GImagePath");
                entity.Property(e => e.IsDisabled).HasDefaultValue(0);
                entity.Property(e => e.MetaScore).HasDefaultValue(0);
                entity.Property(e => e.Ready).HasDefaultValue(0);
                entity.Property(e => e.Score).HasDefaultValue(0.0);
                entity.Property(e => e.TotalPlayTimeSeconds).HasDefaultValue(0);

                entity.Property(e => e.PublishDate)
                    .HasConversion(
                        v => v.HasValue ? v.Value.ToString("yyyy-MM-dd") : null,
                        v => string.IsNullOrEmpty(v) ? (DateOnly?)null : DateOnly.Parse(v)
                    );

                entity.HasOne(d => d.Ownership).WithMany(p => p.Games)
                    .HasForeignKey(d => d.OwnershipId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(d => d.Progress).WithMany(p => p.Games)
                    .HasForeignKey(d => d.ProgressId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(d => d.Series).WithMany(p => p.Games)
                    .HasForeignKey(d => d.SeriesId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<GamePosition>(entity =>
            {
                entity.HasKey(e => e.GameId);
                entity.ToTable("GamePosition");
                entity.HasOne(d => d.Game).WithOne(p => p.GamePosition).HasForeignKey<GamePosition>(d => d.GameId);
            });

            modelBuilder.Entity<Genre>(entity =>
            {
                entity.HasIndex(e => e.Name).IsUnique();
            });

            modelBuilder.Entity<OwnershipStatus>(entity =>
            {
                entity.ToTable("OwnershipStatus");
                entity.HasIndex(e => e.Name).IsUnique();
            });

            modelBuilder.Entity<ProgressStatus>(entity =>
            {
                entity.ToTable("ProgressStatus");
                entity.HasIndex(e => e.Name).IsUnique();
            });

            modelBuilder.Entity<Series>(entity =>
            {
                entity.HasIndex(e => e.Name).IsUnique();
            });

            modelBuilder.Entity<Theme>(entity =>
            {
                entity.HasIndex(e => e.Name).IsUnique();
            });

            modelBuilder.Entity<GameGenre>(entity =>
            {
                entity.HasKey(gg => new { gg.GameId, gg.GenreId });
            });

            modelBuilder.Entity<GameTheme>(entity =>
            {
                entity.HasKey(gt => new { gt.GameId, gt.ThemeId });
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}