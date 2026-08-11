using System.Reflection;
using System.Text.Json;
using ItalianApp.Api.Features.Catalog;
using Microsoft.EntityFrameworkCore;

namespace ItalianApp.Api.Infrastructure.Persistence;

// Deliberately not HasData: the table is edited in place, and a migration would
// overwrite those edits. Only missing codes are inserted.
public static class PhoneticTipSeeder
{
    private const string ResourceName = "ItalianApp.Api.Infrastructure.Persistence.Seed.phonetic-tips.json";

    public static async Task SeedMissingAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var seeds = ReadSeedFile();

        var existingCodes = await db.PhoneticTips
            .Select(tip => tip.Code)
            .ToListAsync(cancellationToken);

        var missing = seeds
            .Where(seed => !existingCodes.Contains(seed.Code))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        db.PhoneticTips.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static List<PhoneticTip> ReadSeedFile()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {ResourceName}");

        return JsonSerializer.Deserialize<List<PhoneticTip>>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"Unreadable content in {ResourceName}");
    }
}
