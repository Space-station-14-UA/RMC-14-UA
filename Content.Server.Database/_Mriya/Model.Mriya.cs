using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Content.Server.Database
{
    public abstract partial class ServerDbContext
    {
        public DbSet<MriyaSponsor> MriyaSponsors { get; set; } = null!;
        public DbSet<SponsorRank> SponsorRanks { get; set; } = null!;
        public DbSet<RankTag> RankTags { get; set; } = null!;
        public DbSet<SponsorRoleAssignment> SponsorRoleAssignments { get; set; } = default!;

        public void ConfigureMriya(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MriyaSponsor>()
               .HasOne(s => s.SelectedGhostRank)
               .WithMany()
               .HasForeignKey(s => s.SelectedGhostRankId)
               .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<MriyaSponsor>()
                .HasOne(s => s.SelectedOocRank)
                .WithMany()
                .HasForeignKey(s => s.SelectedOocRankId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<SponsorRoleAssignment>()
                .HasKey(sra => new { sra.UserId, sra.RankId });

            modelBuilder.Entity<SponsorRoleAssignment>()
                .HasOne(sra => sra.Sponsor)
                .WithMany(s => s.RoleAssignments)
                .HasForeignKey(sra => sra.UserId);

            modelBuilder.Entity<SponsorRoleAssignment>()
                .HasOne(sra => sra.Rank)
                .WithMany(r => r.RoleAssignments)
                .HasForeignKey(sra => sra.RankId);
        }
    }

    public class MriyaSponsor
    {
        [Key]
        public Guid UserId { get; set; }
        public string? SelectedGhostColor { get; set; }
        public string? SelectedOocColor { get; set; }

        public int? SelectedGhostRankId { get; set; }
        public SponsorRank? SelectedGhostRank { get; set; }

        public int? SelectedOocRankId { get; set; }
        public SponsorRank? SelectedOocRank { get; set; }

        public List<SponsorRoleAssignment> RoleAssignments { get; set; } = new();
    }

    public class SponsorRoleAssignment
    {
        public Guid UserId { get; set; }
        public MriyaSponsor Sponsor { get; set; } = default!;
        public int RankId { get; set; }
        public SponsorRank Rank { get; set; } = default!;
    }

    public class SponsorRank
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public string DefaultColor { get; set; } = "#FFFFFF";

        public string? DefaultGhostColor { get; set; }
        public string? DefaultOocColor { get; set; }

        public bool CanSetGhostColor { get; set; }
        public bool CanSetOocColor { get; set; }

        public bool ShowInSponsorWindow { get; set; } = true;
        public int Priority { get; set; } = 0;

        public List<SponsorRoleAssignment> RoleAssignments { get; set; } = new();
        public List<RankTag> Tags { get; set; } = new();
    }

    public class RankTag
    {
        [Key]
        public int Id { get; set; }
        public int SponsorRankId { get; set; }
        public SponsorRank SponsorRank { get; set; } = default!;
        public string TagValue { get; set; } = default!;
    }
}
