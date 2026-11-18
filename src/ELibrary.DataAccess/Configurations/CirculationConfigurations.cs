using ELibrary.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELibrary.DataAccess.Configurations;

internal sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> b)
    {
        b.ToTable("Loans", t => t.HasCheckConstraint("CK_Loans_RenewalCount", "[RenewalCount] >= 0"));

        // A copy can never have two open loans, even if two librarians press "Check out" at the same time.
        b.HasIndex(l => l.CopyId).IsUnique().HasFilter("[ReturnedAt] IS NULL").HasDatabaseName("UX_Loans_OpenLoanPerCopy");
        b.HasIndex(l => new { l.MemberId, l.ReturnedAt });
        b.HasIndex(l => l.DueDate);

        b.HasOne<Copy>().WithMany().HasForeignKey(l => l.CopyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Member>().WithMany().HasForeignKey(l => l.MemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(l => l.IssuedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(l => l.ReturnedById).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(l => l.IsOpen);
    }
}

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> b)
    {
        b.ToTable("Reservations");

        // BR4 at database level: one active (Waiting = 1 or OnHold = 2) reservation per member per title.
        b.HasIndex(r => new { r.TitleId, r.MemberId }).IsUnique().HasFilter("[Status] IN (1, 2)")
            .HasDatabaseName("UX_Reservations_ActivePerMemberAndTitle");
        b.HasIndex(r => new { r.TitleId, r.Status, r.ReservedAt });

        b.HasOne<Title>().WithMany().HasForeignKey(r => r.TitleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Member>().WithMany().HasForeignKey(r => r.MemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Copy>().WithMany().HasForeignKey(r => r.HeldCopyId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(r => r.IsActive);
    }
}

internal sealed class FineConfiguration : IEntityTypeConfiguration<Fine>
{
    public void Configure(EntityTypeBuilder<Fine> b)
    {
        b.ToTable("Fines", t =>
        {
            t.HasCheckConstraint("CK_Fines_Amount", "[Amount] >= 0");
            t.HasCheckConstraint("CK_Fines_AmountPaid", "[AmountPaid] >= 0 AND [AmountPaid] <= [Amount]");
            t.HasCheckConstraint("CK_Fines_OverdueDays", "[OverdueDays] > 0");
        });
        b.HasOne<Loan>().WithOne().HasForeignKey<Fine>(f => f.LoanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Member>().WithMany().HasForeignKey(f => f.MemberId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(f => f.Balance);
        b.Ignore(f => f.IsSettled);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("Payments", t => t.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0"));
        b.HasOne<Fine>().WithMany().HasForeignKey(p => p.FineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserAccount>().WithMany().HasForeignKey(p => p.ReceivedById).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> b)
    {
        b.ToTable("Holidays");
        b.HasIndex(h => h.Date).IsUnique();
        b.Property(h => h.Description).HasMaxLength(100).IsRequired();
    }
}

internal sealed class FinePolicyConfiguration : IEntityTypeConfiguration<FinePolicy>
{
    public void Configure(EntityTypeBuilder<FinePolicy> b)
    {
        b.ToTable("FinePolicies", t =>
        {
            t.HasCheckConstraint("CK_FinePolicies_DailyRate", "[DailyRate] > 0");
            t.HasCheckConstraint("CK_FinePolicies_Cap", "[MaxFinePerLoan] >= [DailyRate]");
            t.HasCheckConstraint("CK_FinePolicies_HoldPeriodDays", "[HoldPeriodDays] BETWEEN 1 AND 30");
        });
    }
}
