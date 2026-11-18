using ELibrary.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELibrary.DataAccess.Configurations;

internal sealed class PublisherConfiguration : IEntityTypeConfiguration<Publisher>
{
    public void Configure(EntityTypeBuilder<Publisher> b)
    {
        b.ToTable("Publishers");
        b.Property(p => p.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(p => p.Name).IsUnique();
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories");
        b.Property(c => c.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(c => c.Name).IsUnique();
    }
}

internal sealed class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> b)
    {
        b.ToTable("Authors");
        b.Property(a => a.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(a => a.Name).IsUnique();
    }
}

internal sealed class TitleConfiguration : IEntityTypeConfiguration<Title>
{
    public void Configure(EntityTypeBuilder<Title> b)
    {
        b.ToTable("Titles");
        b.Property(t => t.Isbn).HasMaxLength(13);
        b.HasIndex(t => t.Isbn).IsUnique().HasFilter("[Isbn] IS NOT NULL");
        b.Property(t => t.Name).HasMaxLength(300).IsRequired();
        b.HasIndex(t => t.Name); // catalogue search (NFR-01)
        b.Property(t => t.Description).HasMaxLength(2000);
        b.HasOne<Publisher>().WithMany().HasForeignKey(t => t.PublisherId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Category>().WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TitleAuthorConfiguration : IEntityTypeConfiguration<TitleAuthor>
{
    public void Configure(EntityTypeBuilder<TitleAuthor> b)
    {
        b.ToTable("TitleAuthors");
        b.HasIndex(ta => new { ta.TitleId, ta.AuthorId }).IsUnique();
        b.HasOne<Title>().WithMany().HasForeignKey(ta => ta.TitleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Author>().WithMany().HasForeignKey(ta => ta.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EResourceConfiguration : IEntityTypeConfiguration<EResource>
{
    public void Configure(EntityTypeBuilder<EResource> b)
    {
        b.ToTable("EResources");
        b.Property(e => e.Label).HasMaxLength(150).IsRequired();
        b.Property(e => e.Url).HasMaxLength(500).IsRequired();
        b.HasOne<Title>().WithMany().HasForeignKey(e => e.TitleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CopyConfiguration : IEntityTypeConfiguration<Copy>
{
    public void Configure(EntityTypeBuilder<Copy> b)
    {
        b.ToTable("Copies");
        b.Property(c => c.Barcode).HasMaxLength(30).IsRequired();
        b.HasIndex(c => c.Barcode).IsUnique();
        b.Property(c => c.Location).HasMaxLength(50).IsRequired();
        b.HasOne<Title>().WithMany().HasForeignKey(c => c.TitleId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(c => c.IsAvailable);
    }
}
