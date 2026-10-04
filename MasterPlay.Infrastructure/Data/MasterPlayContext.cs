using Microsoft.EntityFrameworkCore;
using MasterPlay.Core.Entities;
using MasterPlay.Core.Taxonomy;
using MasterPlay.Infrastructure.Configuration;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MasterPlay.Infrastructure.Data
{
    public partial class MasterPlayContext : DbContext
    {
        public MasterPlayContext() { }
        public MasterPlayContext(DbContextOptions<MasterPlayContext> options) : base(options) { }

        public virtual DbSet<DetectedProcess> DetectedProcesses { get; set; }
        public virtual DbSet<Game> Games { get; set; }
        public virtual DbSet<GamePosition> GamePositions { get; set; }
        public virtual DbSet<GameOwnershipInfo> GameOwnershipInfos { get; set; }
        public virtual DbSet<Genre> Genres { get; set; }
        public virtual DbSet<OwnershipStatus> OwnershipStatuses { get; set; }
        public virtual DbSet<ProgressStatus> ProgressStatuses { get; set; }
        public virtual DbSet<Series> Series { get; set; }
        public virtual DbSet<Theme> Themes { get; set; }
        public virtual DbSet<ThemeGroup> ThemeGroups { get; set; }
        public DbSet<GameGenre> GameGenres { get; set; }
        public DbSet<GameTheme> GameThemes { get; set; }
        public virtual DbSet<GameFinish> GameFinishes { get; set; }
        public virtual DbSet<PlayDiary> PlayDiaries { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbPath = JsonService.GetDatabasePath();
                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                optionsBuilder.UseSqlite($"Data Source={dbPath};Default Timeout=10;Pooling=True");
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
                entity.Property(e => e.LibraryOwnerKey)
                    .HasColumnName("LibraryOwnerKey")
                    .HasDefaultValue("legacy:local")
                    .IsRequired();
                entity.Property(e => e.MetadataGameId).HasColumnName("MetadataGameId");
                entity.Property(e => e.MetadataRevision).HasColumnName("MetadataRevision");
                entity.Property(e => e.MetadataSyncedAt).HasColumnName("MetadataSyncedAt");
                entity.Property(e => e.MetadataBaseline).HasColumnName("MetadataBaseline");
                entity.HasIndex(e => new { e.LibraryOwnerKey, e.MetadataGameId })
                    .HasDatabaseName("IX_Games_LibraryOwnerKey_MetadataGameId")
                    .HasFilter("MetadataGameId IS NOT NULL")
                    .IsUnique();
                entity.HasIndex(e => new { e.LibraryOwnerKey, e.IsDisabled })
                    .HasDatabaseName("IX_Games_LibraryOwnerKey_IsDisabled");
                entity.HasIndex(e => e.MetadataGameId)
                    .HasDatabaseName("IX_Games_MetadataGameId");
                entity.Property(e => e.AvgPlayTime).HasDefaultValue(0);
                entity.Property(e => e.Excited).HasDefaultValue(0.0);
                entity.Property(e => e.BannerPath).HasColumnName("BannerPath");
                entity.Property(e => e.BackdropPath).HasColumnName("BackdropPath");
                entity.Property(e => e.PosterPath).HasColumnName("PosterPath");
                entity.Property(e => e.ImageSources).HasColumnName("ImageSources");
                entity.Property(e => e.IsDisabled).HasDefaultValue(0);
                entity.Property(e => e.IsDlc).HasDefaultValue(0);
                entity.Property(e => e.MetaScore).HasDefaultValue(0);
                entity.Property(e => e.Ready).HasDefaultValue(0);
                entity.Property(e => e.Score).HasDefaultValue(0.0);
                entity.Property(e => e.TotalPlayTimeSeconds).HasDefaultValue(0);
                entity.Property(e => e.LastPlayedAt).HasColumnName("LastPlayedAt");

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

            modelBuilder.Entity<GameOwnershipInfo>(entity =>
            {
                entity.HasKey(e => e.GameId);
                entity.ToTable("GameOwnershipInfo");
                entity.HasOne(e => e.Game)
                    .WithOne(e => e.OwnershipInfo)
                    .HasForeignKey<GameOwnershipInfo>(e => e.GameId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Genre>(entity =>
            {
                // Unique theo (chu so huu, ten da chuan hoa) — khong con unique tren Name.
                // Xem spec 2026-10-04-theme-groups-design §4.1.
                entity.HasIndex(e => new { e.LibraryOwnerKey, e.NameKey })
                    .HasDatabaseName("IX_Genres_LibraryOwnerKey_NameKey")
                    .IsUnique();
                entity.Property(e => e.IconData);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
                entity.Property(e => e.LibraryOwnerKey).HasDefaultValue(TaxonomyRules.SharedOwnerKey).IsRequired();
                entity.Property(e => e.NameKey).IsRequired();
            });

            modelBuilder.Entity<OwnershipStatus>(entity =>
            {
                entity.ToTable("OwnershipStatus");
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.IconData);
            });

            modelBuilder.Entity<ProgressStatus>(entity =>
            {
                entity.ToTable("ProgressStatus");
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.IconData);
            });

            modelBuilder.Entity<Series>(entity =>
            {
                entity.HasIndex(e => e.Name).IsUnique();
            });

            modelBuilder.Entity<Theme>(entity =>
            {
                entity.HasIndex(e => new { e.LibraryOwnerKey, e.NameKey })
                    .HasDatabaseName("IX_Themes_LibraryOwnerKey_NameKey")
                    .IsUnique();
                entity.Property(e => e.IconData);
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
                entity.Property(e => e.LibraryOwnerKey).HasDefaultValue(TaxonomyRules.SharedOwnerKey).IsRequired();
                entity.Property(e => e.NameKey).IsRequired();
                entity.HasOne(e => e.Group).WithMany(g => g.Themes)
                    .HasForeignKey(e => e.GroupId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ThemeGroup>(entity =>
            {
                entity.ToTable("ThemeGroups");
                entity.Property(e => e.LibraryOwnerKey).HasDefaultValue(TaxonomyRules.SharedOwnerKey).IsRequired();
                entity.Property(e => e.NameEn).IsRequired();
                entity.Property(e => e.NameVi).IsRequired();
                entity.Property(e => e.SortOrder).HasDefaultValue(0);
                entity.HasIndex(e => new { e.LibraryOwnerKey, e.SortOrder })
                    .HasDatabaseName("IX_ThemeGroups_LibraryOwnerKey_SortOrder");
            });

            modelBuilder.Entity<GameGenre>(entity =>
            {
                entity.HasKey(gg => new { gg.GameId, gg.GenreId });
            });

            modelBuilder.Entity<GameTheme>(entity =>
            {
                entity.HasKey(gt => new { gt.GameId, gt.ThemeId });
            });
            modelBuilder.Entity<PlayDiary>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Date).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(d => d.Game)
                    .WithMany()
                    .HasForeignKey(d => d.GameId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<GameFinish>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.FinishedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.HasMonth).HasDefaultValue(true);

                entity.HasOne(e => e.Game)
                    .WithMany(e => e.Finishes)
                    .HasForeignKey(e => e.GameId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

        // NameKey luon tinh tu Name o MOT cho, cho moi duong ghi (Host, WPF, Tray):
        // de ai do quen gan thi unique index (owner, NameKey) se va nhau tren chuoi rong.
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            SyncTaxonomyNameKeys();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            SyncTaxonomyNameKeys();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void SyncTaxonomyNameKeys()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
                switch (entry.Entity)
                {
                    case Theme theme:
                        theme.NameKey = TaxonomyRules.NormalizeName(theme.Name);
                        break;
                    case Genre genre:
                        genre.NameKey = TaxonomyRules.NormalizeName(genre.Name);
                        break;
                }
            }
        }
    }
}
