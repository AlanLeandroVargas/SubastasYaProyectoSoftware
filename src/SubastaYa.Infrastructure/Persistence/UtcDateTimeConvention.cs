using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SubastaYa.Infrastructure.Persistence;

/// <summary>
/// Garantiza que toda fecha viaje en UTC en ambos sentidos.
/// SQL Server no almacena la zona horaria en datetime2, de modo que sin esta conversión las
/// fechas volverían con Kind=Unspecified y el frontend las interpretaría como hora local,
/// desfasando los contadores regresivos de la sala en vivo.
/// </summary>
internal static class UtcDateTimeConvention
{
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter = new(
        value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter = new(
        value => value.HasValue ? (value.Value.Kind == DateTimeKind.Utc ? value : value.Value.ToUniversalTime()) : value,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);

    public static void ApplyToAllDateTimes(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(UtcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(NullableUtcConverter);
                }
            }
        }
    }
}
