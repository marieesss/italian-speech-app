using ItalianApp.Api.Infrastructure.Configuration;
using ItalianApp.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddDotEnv(builder.Environment.ContentRootPath);

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

// Model MP3s are served from wwwroot/audio/it/.
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("Health")
   .WithTags("Diagnostics");

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
