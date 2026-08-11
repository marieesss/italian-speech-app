using ItalianApp.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

await app.InitialiseDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Les MP3 modèles sont servis en statique depuis wwwroot/audio/it/ (cf. §2.2).
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("Health")
   .WithTags("Diagnostics");

app.Run();

/// <summary>
/// Exposé pour <c>WebApplicationFactory&lt;Program&gt;</c> dans les tests d'intégration.
/// </summary>
public partial class Program;
