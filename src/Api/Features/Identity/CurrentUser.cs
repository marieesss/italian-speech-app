using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ItalianApp.Api.Features.Identity;

public static class CurrentUser
{
    public static Guid Id(this ClaimsPrincipal principal)
    {
        // JwtBearer remaps "sub" to ClaimTypes.NameIdentifier unless told otherwise.
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                    ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("Authenticated principal carries no usable subject claim.");
    }

    public static string? Email(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(JwtRegisteredClaimNames.Email)
        ?? principal.FindFirstValue(ClaimTypes.Email);
}
