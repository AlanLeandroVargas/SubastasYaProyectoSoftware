using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name).IsRequired().HasMaxLength(80);
        builder.Property(category => category.IconUrl).IsRequired().HasMaxLength(200);

        builder.HasIndex(category => category.Name).IsUnique();

        builder.Metadata
            .FindNavigation(nameof(Category.Auctions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
