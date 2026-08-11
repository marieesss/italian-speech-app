using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using ItalianApp.Api.Features.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ItalianApp.Api.Tests.Identity;

public class TokenIssuerTests
{
    private static readonly User Anna = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Email = "anna@example.com",
        DisplayName = "Anna",
        PasswordHash = "irrelevant"
    };

    [Fact]
    public void Token_carries_the_user_id_as_subject()
    {
        var token = Read(Issue().Token);

        token.Subject.Should().Be(Anna.Id.ToString());
    }

    [Fact]
    public void Lifetime_comes_from_configuration()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 8, 11, 10, 0, 0, TimeSpan.Zero));

        var issued = Issue(lifetimeHours: 3, clock: clock);

        issued.ExpiresAt.Should().Be(clock.GetUtcNow().AddHours(3));
    }

    [Fact]
    public void Issuer_and_audience_are_stamped()
    {
        var token = Read(Issue().Token);

        token.Issuer.Should().Be("italian-app-tests");
        token.Audiences.Should().ContainSingle().Which.Should().Be("italian-app-tests");
    }

    private static AccessToken Issue(int lifetimeHours = 72, TimeProvider? clock = null)
    {
        var options = Options.Create(new JwtOptions
        {
            SigningSecret = "unit-tests-signing-secret-0123456789012",
            Issuer = "italian-app-tests",
            Audience = "italian-app-tests",
            LifetimeHours = lifetimeHours
        });

        return new TokenIssuer(options, clock ?? TimeProvider.System).Issue(Anna);
    }

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);
}
