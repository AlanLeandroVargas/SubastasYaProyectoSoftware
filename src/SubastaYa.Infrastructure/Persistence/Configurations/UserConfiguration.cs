using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email).IsRequired().HasMaxLength(150);
        builder.Property(user => user.Name).IsRequired().HasMaxLength(120);
        builder.Property(user => user.Pseudonym).IsRequired().HasMaxLength(60);
        builder.Property(user => user.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(user => user.RegisteredAt).IsRequired();

        builder.HasIndex(user => user.Email).IsUnique();
        builder.HasIndex(user => user.Pseudonym).IsUnique();

        // Relación 1:1 con la billetera: no existe usuario sin respaldo económico.
        builder.HasOne(user => user.Wallet)
            .WithOne(wallet => wallet.User)
            .HasForeignKey<Wallet>(wallet => wallet.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Las colecciones se exponen como sólo lectura, así que EF accede al campo de respaldo.
        builder.Metadata
            .FindNavigation(nameof(User.PublishedAuctions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Metadata
            .FindNavigation(nameof(User.PlacedBids))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
