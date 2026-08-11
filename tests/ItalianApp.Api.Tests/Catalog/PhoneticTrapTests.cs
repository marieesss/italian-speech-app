using FluentAssertions;
using ItalianApp.Api.Features.Catalog;

namespace ItalianApp.Api.Tests.Catalog;

public class PhoneticTrapTests
{
    [Fact]
    public void Bare_code_has_no_argument()
    {
        var trap = PhoneticTrap.Parse("gli");

        trap.Code.Should().Be("gli");
        trap.Argument.Should().BeNull();
    }

    [Fact]
    public void Argument_follows_a_colon()
    {
        var trap = PhoneticTrap.Parse("double_consonant:tt");

        trap.Code.Should().Be("double_consonant");
        trap.Argument.Should().Be("tt");
    }

    [Fact]
    public void Argument_keeps_accented_characters()
    {
        var trap = PhoneticTrap.Parse("stress:prenotàre");

        trap.Code.Should().Be("stress");
        trap.Argument.Should().Be("prenotàre");
    }

    [Theory]
    [InlineData("  gn  ", "gn", null)]
    [InlineData("gn:", "gn", null)]
    [InlineData("sc_soft : sce", "sc_soft", "sce")]
    public void Whitespace_and_empty_arguments_are_normalised(string raw, string code, string? argument)
    {
        var trap = PhoneticTrap.Parse(raw);

        trap.Code.Should().Be(code);
        trap.Argument.Should().Be(argument);
    }

    [Fact]
    public void ToString_round_trips()
    {
        PhoneticTrap.Parse("double_consonant:tt").ToString().Should().Be("double_consonant:tt");
        PhoneticTrap.Parse("rolled_r").ToString().Should().Be("rolled_r");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_code_is_rejected(string raw)
    {
        var act = () => PhoneticTrap.Parse(raw);

        act.Should().Throw<ArgumentException>();
    }
}
