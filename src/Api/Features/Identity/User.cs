using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItalianApp.Api.Features.Identity;

public class User
{
    public Guid Id { get; set; }

    /// <summary>Stocké en minuscules, sert d'identifiant de connexion.</summary>
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.PasswordHash).HasMaxLength(512);
        builder.Property(x => x.DisplayName).HasMaxLength(128);
    }
}
