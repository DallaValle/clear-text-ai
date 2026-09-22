namespace ClearTextAI.Core.Models;

/// <summary>Everything the user can tune about a rewrite. Serialized as part of settings and history.</summary>
public sealed class RewriteOptions
{
    public const double MinTemperature = 0.0;
    public const double MaxTemperature = 1.0;
    public const double DefaultTemperature = 0.3;
    public const int MaxCustomInstructionsLength = 2000;

    public RewritePreset Preset { get; set; } = RewritePreset.Natural;

    public double Temperature { get; set; } = DefaultTemperature;

    public WritingStyle Style { get; set; } = WritingStyle.HumanProfessional;

    /// <summary>On by default: the rewrite may never alter facts, numbers, names or URLs.</summary>
    public bool PreserveMeaning { get; set; } = true;

    public RewriteTone Tone { get; set; } = RewriteTone.PreserveOriginal;

    /// <summary>Restricts the model to spelling, grammar and punctuation fixes only.</summary>
    public bool GrammarOnly { get; set; }

    public string CustomInstructions { get; set; } = string.Empty;

    public RewriteOptions Clone() => new()
    {
        Preset = Preset,
        Temperature = Temperature,
        Style = Style,
        PreserveMeaning = PreserveMeaning,
        Tone = Tone,
        GrammarOnly = GrammarOnly,
        CustomInstructions = CustomInstructions,
    };

    /// <summary>Clamps anything a slider, a hand-edited settings file or an older app version could have left out of range.</summary>
    public RewriteOptions Normalize()
    {
        Temperature = Math.Clamp(double.IsNaN(Temperature) ? DefaultTemperature : Temperature, MinTemperature, MaxTemperature);

        if (!Enum.IsDefined(Style))
        {
            Style = WritingStyle.HumanProfessional;
        }

        if (!Enum.IsDefined(Tone))
        {
            Tone = RewriteTone.PreserveOriginal;
        }

        if (!Enum.IsDefined(Preset))
        {
            Preset = RewritePreset.Custom;
        }

        CustomInstructions = (CustomInstructions ?? string.Empty).Trim();
        if (CustomInstructions.Length > MaxCustomInstructionsLength)
        {
            CustomInstructions = CustomInstructions[..MaxCustomInstructionsLength];
        }

        return this;
    }

    /// <summary>Compares everything a preset controls, so the UI can tell "still Natural" from "edited".</summary>
    public bool MatchesPresetDefaults(RewritePreset preset)
    {
        var reference = RewritePresets.Create(preset);
        return Math.Abs(reference.Temperature - Temperature) < 0.0001
               && reference.Style == Style
               && reference.PreserveMeaning == PreserveMeaning
               && reference.Tone == Tone
               && reference.GrammarOnly == GrammarOnly;
    }
}
