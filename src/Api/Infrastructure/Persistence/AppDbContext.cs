using ItalianApp.Api.Features.Catalog;
using ItalianApp.Api.Features.Identity;
using ItalianApp.Api.Features.Progress;
using ItalianApp.Api.Features.Quota;
using Microsoft.EntityFrameworkCore;

namespace ItalianApp.Api.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Scenario> Scenarios => Set<Scenario>();
    public DbSet<Phrase> Phrases => Set<Phrase>();
    public DbSet<PhoneticTip> PhoneticTips => Set<PhoneticTip>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<PhraseProgress> PhraseProgresses => Set<PhraseProgress>();

    public DbSet<DailyUsage> DailyUsages => Set<DailyUsage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Les configurations vivent à côté de leur entité, dans leur slice.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
