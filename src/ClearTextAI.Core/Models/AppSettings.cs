namespace ClearTextAI.Core.Models;

/// <summary>The full persisted configuration. Written to a JSON file in the user's local app data folder.</summary>
public sealed class AppSettings
{
    public AiProviderKind ActiveProvider { get; set; } = AiProviderKind.Claude;

    /// <summary>Kept per provider so switching back and forth does not lose model or endpoint choices.</summary>
    public List<AiProviderSettings> Providers { get; set; } = [];

    public RewriteOptions RewriteOptions { get; set; } = RewritePresets.Create(RewritePreset.Natural);

    public bool HistoryEnabled { get; set; } = true;

    /// <summary>The system wide shortcut that rewrites the selected text in any application.</summary>
    public bool GlobalShortcutEnabled { get; set; } = true;

    /// <summary>
    /// On Windows, closing the window hides it in the notification area instead of quitting,
    /// so the global shortcut keeps working. Quit from that icon.
    /// </summary>
    public bool RunInBackground { get; set; } = true;

    /// <summary>Opt-in only, and off by default. Nothing is collected while this is false.</summary>
    public bool TelemetryEnabled { get; set; }

    public AiProviderSettings ActiveProviderSettings => GetOrCreate(ActiveProvider);

    public AiProviderSettings GetOrCreate(AiProviderKind provider)
    {
        var existing = Providers.FirstOrDefault(p => p.Provider == provider);
        if (existing is not null)
        {
            return existing;
        }

        var created = AiProviderSettings.CreateDefault(provider);
        Providers.Add(created);
        return created;
    }

    public AppSettings Normalize()
    {
        Providers ??= [];
        Providers.RemoveAll(p => p is null || !Enum.IsDefined(p.Provider));

        if (!Enum.IsDefined(ActiveProvider))
        {
            ActiveProvider = AiProviderKind.Claude;
        }

        foreach (var provider in Providers)
        {
            provider.Normalize();
        }

        // Make sure every known provider has an entry so the settings UI never binds to null.
        foreach (var kind in Enum.GetValues<AiProviderKind>())
        {
            GetOrCreate(kind);
        }

        RewriteOptions = (RewriteOptions ?? new RewriteOptions()).Normalize();
        return this;
    }

    public static AppSettings CreateDefault() => new AppSettings().Normalize();
}
