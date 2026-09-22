using ClearTextAI.Core.Models;
using Xunit;

namespace ClearTextAI.Core.Tests;

public class RewriteOptionsTests
{
    [Fact]
    public void Defaults_PreserveMeaningIsOn()
    {
        var options = new RewriteOptions();

        Assert.True(options.PreserveMeaning);
        Assert.Equal(RewriteTone.PreserveOriginal, options.Tone);
        Assert.Equal(RewriteOptions.DefaultTemperature, options.Temperature);
        Assert.False(options.GrammarOnly);
    }

    [Theory]
    [InlineData(-5.0, RewriteOptions.MinTemperature)]
    [InlineData(42.0, RewriteOptions.MaxTemperature)]
    [InlineData(double.NaN, RewriteOptions.DefaultTemperature)]
    [InlineData(0.55, 0.55)]
    public void Normalize_ClampsTemperature(double input, double expected)
    {
        var options = new RewriteOptions { Temperature = input };

        options.Normalize();

        Assert.Equal(expected, options.Temperature);
    }

    [Fact]
    public void Normalize_RepairsOutOfRangeEnums()
    {
        var options = new RewriteOptions
        {
            Tone = (RewriteTone)99,
            Style = (WritingStyle)99,
            Preset = (RewritePreset)99,
        };

        options.Normalize();

        Assert.Equal(RewriteTone.PreserveOriginal, options.Tone);
        Assert.Equal(WritingStyle.HumanProfessional, options.Style);
        Assert.Equal(RewritePreset.Custom, options.Preset);
    }

    [Fact]
    public void Normalize_TrimsAndCapsCustomInstructions()
    {
        var options = new RewriteOptions
        {
            CustomInstructions = "  " + new string('x', RewriteOptions.MaxCustomInstructionsLength + 50) + "  ",
        };

        options.Normalize();

        Assert.Equal(RewriteOptions.MaxCustomInstructionsLength, options.CustomInstructions.Length);
    }

    [Fact]
    public void Clone_IsIndependentOfTheOriginal()
    {
        var original = new RewriteOptions { Temperature = 0.7, CustomInstructions = "keep it short" };

        var clone = original.Clone();
        clone.Temperature = 0.1;
        clone.CustomInstructions = "changed";

        Assert.Equal(0.7, original.Temperature);
        Assert.Equal("keep it short", original.CustomInstructions);
    }

    [Theory]
    [InlineData(RewritePreset.Natural)]
    [InlineData(RewritePreset.Professional)]
    [InlineData(RewritePreset.Friendly)]
    [InlineData(RewritePreset.Concise)]
    [InlineData(RewritePreset.GrammarOnly)]
    public void Presets_AlwaysPreserveMeaningAndCarryTheirOwnPresetValue(RewritePreset preset)
    {
        var options = RewritePresets.Create(preset);

        Assert.True(options.PreserveMeaning);
        Assert.Equal(preset, options.Preset);
        Assert.InRange(options.Temperature, RewriteOptions.MinTemperature, RewriteOptions.MaxTemperature);
    }

    [Fact]
    public void GrammarOnlyPreset_IsDeterministicAndRestrictive()
    {
        var options = RewritePresets.Create(RewritePreset.GrammarOnly);

        Assert.True(options.GrammarOnly);
        Assert.Equal(0.0, options.Temperature);
    }

    [Fact]
    public void ConcisePreset_UsesTheConciseTone()
    {
        Assert.Equal(RewriteTone.Concise, RewritePresets.Create(RewritePreset.Concise).Tone);
    }

    [Fact]
    public void Apply_OverwritesPresetControlledValuesButKeepsCustomInstructions()
    {
        var options = new RewriteOptions
        {
            Temperature = 0.9,
            Tone = RewriteTone.Casual,
            CustomInstructions = "never touch code snippets",
        };

        RewritePresets.Apply(RewritePreset.Professional, options);

        Assert.Equal(RewritePreset.Professional, options.Preset);
        Assert.Equal(RewriteTone.Professional, options.Tone);
        Assert.Equal(0.2, options.Temperature);
        Assert.Equal("never touch code snippets", options.CustomInstructions);
    }

    [Fact]
    public void Apply_Custom_LeavesEverythingElseAlone()
    {
        var options = new RewriteOptions { Temperature = 0.9, Tone = RewriteTone.Casual };

        RewritePresets.Apply(RewritePreset.Custom, options);

        Assert.Equal(RewritePreset.Custom, options.Preset);
        Assert.Equal(0.9, options.Temperature);
        Assert.Equal(RewriteTone.Casual, options.Tone);
    }

    [Fact]
    public void MatchesPresetDefaults_DetectsManualEdits()
    {
        var options = RewritePresets.Create(RewritePreset.Friendly);
        Assert.True(options.MatchesPresetDefaults(RewritePreset.Friendly));

        options.Temperature = 0.95;
        Assert.False(options.MatchesPresetDefaults(RewritePreset.Friendly));
    }

    [Fact]
    public void EveryPresetHasADisplayNameAndDescription()
    {
        foreach (var preset in RewritePresets.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(RewritePresets.DisplayName(preset)));
            Assert.False(string.IsNullOrWhiteSpace(RewritePresets.Description(preset)));
        }
    }
}
