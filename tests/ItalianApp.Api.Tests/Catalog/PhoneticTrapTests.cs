using FluentAssertions;
using ItalianApp.Api.Features.Catalog;

namespace ItalianApp.Api.Tests.Catalog;

public class PhoneticTrapTests
{
    [Fact]
    public void Un_code_nu_na_pas_dargument()
    {
        var trap = PhoneticTrap.Parse("gli");

        trap.Code.Should().Be("gli");
        trap.Argument.Should().BeNull();
    }

    [Fact]
    public void Largument_est_separe_par_deux_points()
    {
        var trap = PhoneticTrap.Parse("double_consonant:tt");

        trap.Code.Should().Be("double_consonant");
        trap.Argument.Should().Be("tt");
    }

    [Fact]
    public void Largument_peut_porter_des_accents()
    {
        var trap = PhoneticTrap.Parse("stress:prenotàre");

        trap.Code.Should().Be("stress");
        trap.Argument.Should().Be("prenotàre");
    }

    [Theory]
    [InlineData("  gn  ", "gn", null)]
    [InlineData("gn:", "gn", null)]
    [InlineData("sc_soft : sce", "sc_soft", "sce")]
    public void Les_espaces_et_arguments_vides_sont_normalises(string raw, string code, string? argument)
    {
        var trap = PhoneticTrap.Parse(raw);

        trap.Code.Should().Be(code);
        trap.Argument.Should().Be(argument);
    }

    [Fact]
    public void ToString_restitue_la_forme_dorigine()
    {
        PhoneticTrap.Parse("double_consonant:tt").ToString().Should().Be("double_consonant:tt");
        PhoneticTrap.Parse("rolled_r").ToString().Should().Be("rolled_r");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_code_vide_est_rejete(string raw)
    {
        var act = () => PhoneticTrap.Parse(raw);

        act.Should().Throw<ArgumentException>();
    }
}
