using System.ComponentModel.DataAnnotations;
using ItalianApp.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ItalianApp.Api.Features.Identity;

public record RegisterRequest(string Email, string Password, string DisplayName);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, DateTimeOffset ExpiresAt, UserResponse User);

public record UserResponse(Guid Id, string Email, string DisplayName);

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Identity");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapGet("/me", MeAsync).RequireAuthorization();

        return routes;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        AppDbContext db,
        IPasswordHasher<User> hasher,
        TokenIssuer tokens,
        IOptions<AccountOptions> accountOptions,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var options = accountOptions.Value;

        if (!options.AllowRegistration)
        {
            return Results.Problem(
                title: "Registration closed",
                detail: "This instance no longer accepts new accounts.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var email = Normalise(request.Email);

        var errors = new Dictionary<string, string[]>();

        if (!new EmailAddressAttribute().IsValid(email))
        {
            errors["email"] = ["Not a valid email address."];
        }

        if (request.Password.Length < options.MinimumPasswordLength)
        {
            errors["password"] = [$"At least {options.MinimumPasswordLength} characters required."];
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors["displayName"] = ["Required."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return Results.Problem(
                title: "Email already registered",
                statusCode: StatusCodes.Status409Conflict);
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = string.Empty,
            CreatedAt = clock.GetUtcNow()
        };
        newUser.PasswordHash = hasher.HashPassword(newUser, request.Password);

        db.Users.Add(newUser);
        await db.SaveChangesAsync(cancellationToken);

        var token = tokens.Issue(newUser);

        return Results.Created($"/api/auth/me", ToResponse(token, newUser));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AppDbContext db,
        IPasswordHasher<User> hasher,
        TokenIssuer tokens,
        CancellationToken cancellationToken)
    {
        var email = Normalise(request.Email);

        var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);

        if (user is null)
        {
            // Same response as a wrong password, so the endpoint doesn't leak which emails exist.
            return InvalidCredentials();
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(ToResponse(tokens.Issue(user), user));
    }

    private static async Task<IResult> MeAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([principal.Id()], cancellationToken);

        return user is null
            ? Results.Unauthorized()
            : Results.Ok(new UserResponse(user.Id, user.Email, user.DisplayName));
    }

    private static IResult InvalidCredentials() => Results.Problem(
        title: "Invalid credentials",
        statusCode: StatusCodes.Status401Unauthorized);

    private static AuthResponse ToResponse(AccessToken token, User user) =>
        new(token.Token, token.ExpiresAt, new UserResponse(user.Id, user.Email, user.DisplayName));

    private static string Normalise(string email) => email.Trim().ToLowerInvariant();
}
