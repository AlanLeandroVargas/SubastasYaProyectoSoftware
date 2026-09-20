using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.Configurations;

internal sealed class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("AuditLog");

        builder.HasKey(record => record.Id);

        builder.Property(record => record.Entity)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(record => record.Action)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        // Sin longitud máxima: el detalle es un JSON cuyo tamaño depende de la acción auditada.
        builder.Property(record => record.DetailsJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(record => record.OccurredAt).IsRequired();

        // El usuario es opcional porque los cierres los ejecutará el proceso en segundo plano.
        // Se restringe el borrado para que dar de baja un usuario no borre su rastro.
        builder.HasOne(record => record.User)
            .WithMany()
            .HasForeignKey(record => record.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(record => record.OccurredAt);
        builder.HasIndex(record => new { record.Entity, record.EntityId });
    }
}
