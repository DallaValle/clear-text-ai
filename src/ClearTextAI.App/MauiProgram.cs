using ClearTextAI.App.Services;
using ClearTextAI.App.Views;
using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.AI;
using ClearTextAI.Core.Services.History;
using ClearTextAI.Core.Services.Platform;
using ClearTextAI.Core.Services.Prompt;
using ClearTextAI.Core.Services.Quick;
using ClearTextAI.Core.Services.SecureStorage;
using ClearTextAI.Core.Services.Settings;
using ClearTextAI.Core.ViewModels;

namespace ClearTextAI.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        // No custom fonts: the app deliberately uses each platform's system font.
        builder.UseMauiApp<App>();

        RegisterServices(builder.Services);
        return builder.Build();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // Local storage and settings.
        services.AddSingleton<IAppPaths>(_ => AppPaths.CreateDefault());
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IHistoryStore>(sp => new JsonHistoryStore(sp.GetRequiredService<IAppPaths>()));

        // Secrets: the OS credential store, never a configuration file.
        services.AddSingleton<ISecureCredentialStore, PlatformCredentialStore>();
        services.AddSingleton<IApiKeyStore, ApiKeyStore>();

        // Platform adapters the view models depend on.
        services.AddSingleton<IClipboardService, MauiClipboardService>();
        services.AddSingleton<IDialogService, ShellDialogService>();
        services.AddSingleton<INavigationService, ShellNavigationService>();
        services.AddSingleton<ILinkOpener, MauiLinkOpener>();

        // AI stack. Adding a provider is one more AddSingleton<IAiProvider> line.
        services.AddSingleton<IPromptBuilder, PromptBuilder>();
        services.AddSingleton<IAnthropicClientFactory, AnthropicClientFactory>();
        services.AddSingleton(_ => new HttpClient());
        services.AddSingleton<IAiProvider, ClaudeAiProvider>();
        services.AddSingleton<IAiProvider>(sp => CreateOpenAiProvider(sp, AiProviderKind.OpenAi));
        services.AddSingleton<IAiProvider>(sp => CreateOpenAiProvider(sp, AiProviderKind.Grok));
        services.AddSingleton<IAiProvider>(sp => CreateOpenAiProvider(sp, AiProviderKind.OpenAiCompatible));
        services.AddSingleton<IAiProviderResolver, AiProviderResolver>();
        services.AddSingleton<IRewriteService, RewriteService>();

        // The system wide shortcut and the pieces it drives.
#if WINDOWS
        services.AddSingleton<IGlobalHotkeyService, Platforms.Windows.WindowsGlobalHotkeyService>();
        services.AddSingleton<ISelectionTextService, Platforms.Windows.WindowsSelectionTextService>();
        services.AddSingleton<IQuickRewriteOverlay, Platforms.Windows.WindowsQuickRewriteOverlay>();
#else
        services.AddSingleton<IGlobalHotkeyService, UnsupportedGlobalHotkeyService>();
        services.AddSingleton<ISelectionTextService, UnsupportedSelectionTextService>();
        services.AddSingleton<IQuickRewriteOverlay, NoQuickRewriteOverlay>();
#endif
        services.AddSingleton<QuickRewriteCoordinator>();
        services.AddSingleton<QuickRewriteHost>();

        // Shell, pages and view models.
        services.AddSingleton<AppShell>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainPage>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<HistoryPage>();
    }

    private static OpenAiCompatibleProvider CreateOpenAiProvider(IServiceProvider sp, AiProviderKind kind) =>
        new(kind,
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<IPromptBuilder>(),
            sp.GetRequiredService<IApiKeyStore>(),
            sp.GetRequiredService<ISettingsService>());
}
