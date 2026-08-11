using ItalianApp.Api.Features.Catalog;
using ItalianApp.Api.Features.Identity;
using ItalianApp.Api.Infrastructure.Configuration;
using ItalianApp.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddDotEnv(builder.Environment.ContentRootPath);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(SwaggerSetup.Configure);
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddPersistence();
builder.Services.AddIdentityFeature(builder.Configuration);

var app = builder.Build();

await app.InitialiseDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Model MP3s are served from wwwroot/audio/it/.
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("Health")
   .WithTags("Diagnostics");

app.MapIdentityEndpoints();
app.MapCatalogEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in the integration tests.
public partial class Program;
