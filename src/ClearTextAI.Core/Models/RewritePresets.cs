namespace ClearTextAI.Core.Models;

/// <summary>One-click configurations so the settings page stays optional.</summary>
public static class RewritePresets
{
    public static readonly IReadOnlyList<RewritePreset> All =
    [
        RewritePreset.Natural,
        RewritePreset.Professional,
        RewritePreset.Friendly,
        RewritePreset.Concise,
        RewritePreset.GrammarOnly,
        RewritePreset.Custom,
    ];

    public static string DisplayName(RewritePreset preset) => preset switch
    {
        RewritePreset.Natural => "Natural",
        RewritePreset.Professional => "Professional",
        RewritePreset.Friendly => "Friendly",
        RewritePreset.Concise => "Concise",
        RewritePreset.GrammarOnly => "Grammar only",
        _ => "Custom",
    };

    public static string Description(RewritePreset preset) => preset switch
    {
        RewritePreset.Natural => "Clear and human, keeps your voice.",
        RewritePreset.Professional => "Polished wording for work communication.",
        RewritePreset.Friendly => "Warm and approachable, still to the point.",
        RewritePreset.Concise => "Says the same thing in fewer words.",
        RewritePreset.GrammarOnly => "Fixes mistakes, changes nothing else.",
        _ => "Your own combination of settings.",
    };

    /// <summary>Builds the option set for a preset. Custom returns the Natural baseline as a starting point.</summary>
    public static RewriteOptions Create(RewritePreset preset) => preset switch
    {
        RewritePreset.Natural => new RewriteOptions
        {
            Preset = preset,
            Temperature = 0.3,
            Style = WritingStyle.HumanProfessional,
            Tone = RewriteTone.PreserveOriginal,
            GrammarOnly = false,
            PreserveMeaning = true,
        },
        RewritePreset.Professional => new RewriteOptions
        {
            Preset = preset,
            Temperature = 0.2,
            Style = WritingStyle.Polished,
            Tone = RewriteTone.Professional,
            GrammarOnly = false,
            PreserveMeaning = true,
        },
        RewritePreset.Friendly => new RewriteOptions
        {
            Preset = preset,
            Temperature = 0.4,
            Style = WritingStyle.Conversational,
            Tone = RewriteTone.Friendly,
            GrammarOnly = false,
            PreserveMeaning = true,
        },
        RewritePreset.Concise => new RewriteOptions
        {
            Preset = preset,
            Temperature = 0.2,
            Style = WritingStyle.Neutral,
            Tone = RewriteTone.Concise,
            GrammarOnly = false,
            PreserveMeaning = true,
        },
        RewritePreset.GrammarOnly => new RewriteOptions
        {
            Preset = preset,
            // Deterministic on purpose: a correction pass should not invent phrasing.
            Temperature = 0.0,
            Style = WritingStyle.Neutral,
            Tone = RewriteTone.PreserveOriginal,
            GrammarOnly = true,
            PreserveMeaning = true,
        },
        _ => new RewriteOptions { Preset = RewritePreset.Custom },
    };

    /// <summary>Applies a preset in place, keeping the custom instructions the user typed.</summary>
    public static RewriteOptions Apply(RewritePreset preset, RewriteOptions target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (preset == RewritePreset.Custom)
        {
            target.Preset = RewritePreset.Custom;
            return target;
        }

        var source = Create(preset);
        target.Preset = preset;
        target.Temperature = source.Temperature;
        target.Style = source.Style;
        target.Tone = source.Tone;
        target.GrammarOnly = source.GrammarOnly;
        target.PreserveMeaning = source.PreserveMeaning;
        return target;
    }
}
