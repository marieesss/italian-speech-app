using FluentAssertions;
using ItalianApp.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace ItalianApp.Api.Tests.Configuration;

public class DotEnvTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("dotenv").FullName;

    [Fact]
    public void Upper_snake_names_map_onto_nested_keys()
    {
        WriteEnvFile(
            "JWT_ISSUER=italian-app",
            "QUOTA_LLM_CALLS_PER_DAY=42");

        var configuration = Build();

        configuration["Jwt:Issuer"].Should().Be("italian-app");
        configuration.GetValue<int>("Quota:LlmCallsPerDay").Should().Be(42);
    }

    [Fact]
    public void Connection_string_keeps_its_equals_signs()
    {
        WriteEnvFile("DB_CONNECTION=Host=localhost;Port=5434;Database=italianapp");

        Build().GetConnectionString("Default")
            .Should().Be("Host=localhost;Port=5434;Database=italianapp");
    }

    [Fact]
    public void Comments_blank_lines_and_empty_values_are_ignored()
    {
        WriteEnvFile(
            "# a comment",
            "",
            "JWT_ISSUER=",
            "JWT_AUDIENCE=tablet");

        var configuration = Build();

        configuration["Jwt:Issuer"].Should().BeNull();
        configuration["Jwt:Audience"].Should().Be("tablet");
    }

    [Fact]
    public void Unknown_names_are_dropped()
    {
        WriteEnvFile("SOMETHING_ELSE=value");

        Build().AsEnumerable().Should().NotContain(entry => entry.Value == "value");
    }

    [Fact]
    public void File_is_found_from_a_subdirectory()
    {
        WriteEnvFile("JWT_ISSUER=from-parent");
        var nested = Directory.CreateDirectory(Path.Combine(_directory, "src", "Api")).FullName;

        new ConfigurationBuilder().AddDotEnv(nested).Build()["Jwt:Issuer"]
            .Should().Be("from-parent");
    }

    private void WriteEnvFile(params string[] lines) =>
        File.WriteAllLines(Path.Combine(_directory, ".env"), lines);

    private IConfigurationRoot Build() =>
        new ConfigurationBuilder().AddDotEnv(_directory).Build();

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }
}
