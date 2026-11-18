using ELibrary.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELibrary.DataAccess.Configurations;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles");
        b.Property(r => r.Id).ValueGeneratedNever(); // Id = RoleCode
        b.Property(r => r.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(r => r.Name).IsUnique();
        b.Ignore(r => r.Code);
    }
}

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> b)
    {
        b.ToTable("UserAccounts");
        b.Property(a => a.Username).HasMaxLength(50).IsRequired();
        b.HasIndex(a => a.Username).IsUnique();
        b.Property(a => a.PasswordHash).HasMaxLength(200).IsRequired();
        b.Property(a => a.FullName).HasMaxLength(100).IsRequired();
        b.HasOne<Role>().WithMany().HasForeignKey(a => a.RoleId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(a => a.Role);
        b.Ignore(a => a.IsStaff);
    }
}

internal sealed class MembershipTypeConfiguration : IEntityTypeConfiguration<MembershipType>
{
    public void Configure(EntityTypeBuilder<MembershipType> b)
    {
        b.ToTable("MembershipTypes", t =>
        {
            t.HasCheckConstraint("CK_MembershipTypes_MaxLoans", "[MaxLoans] > 0");
            t.HasCheckConstraint("CK_MembershipTypes_LoanPeriodDays", "[LoanPeriodDays] > 0");
            t.HasCheckConstraint("CK_MembershipTypes_MaxRenewals", "[MaxRenewals] >= 0");
        });
        b.Property(t => t.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(t => t.Name).IsUnique();
    }
}

internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> b)
    {
        b.ToTable("Members");
        b.Property(m => m.MemberCode).HasMaxLength(20).IsRequired();
        b.HasIndex(m => m.MemberCode).IsUnique();
        b.Property(m => m.FullName).HasMaxLength(100).IsRequired();
        b.Property(m => m.Email).HasMaxLength(100);
        b.Property(m => m.Phone).HasMaxLength(20);
        b.HasOne<MembershipType>().WithMany().HasForeignKey(m => m.MembershipTypeId).OnDelete(DeleteBehavior.Restrict);
        // 0..1 account per member and 0..1 member per account (unique index created by EF).
        b.HasOne<UserAccount>().WithOne().HasForeignKey<Member>(m => m.UserAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
