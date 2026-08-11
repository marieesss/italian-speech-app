using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ItalianApp.Api.Infrastructure.Persistence;

/// <summary>
/// Sérialise une propriété .NET vers une colonne <c>jsonb</c>.
/// <para>
/// Ces colonnes ne sont jamais interrogées côté SQL — elles portent des annotations
/// (pièges phonétiques) et des détails de score qu'on relit toujours en bloc.
/// Une conversion vers <c>string</c> suffit donc, et évite de figer une forme relationnelle
/// sur des données dont le contenu est susceptible d'évoluer.
/// </para>
/// </summary>
public static class JsonbConversion
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static PropertyBuilder<T> HasJsonbConversion<T>(this PropertyBuilder<T> builder)
    {
        var converter = new ValueConverter<T, string>(
            value => JsonSerializer.Serialize(value, Options),
            json => JsonSerializer.Deserialize<T>(json, Options)!);

        // Sans comparateur explicite, EF ne détecte pas les mutations internes d'une liste.
        var comparer = new ValueComparer<T>(
            (left, right) => JsonSerializer.Serialize(left, Options) == JsonSerializer.Serialize(right, Options),
            value => JsonSerializer.Serialize(value, Options).GetHashCode(StringComparison.Ordinal),
            value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Options), Options)!);

        return builder
            .HasConversion(converter, comparer)
            .HasColumnType("jsonb");
    }
}
