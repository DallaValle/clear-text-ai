using System.Text.Json;
using ClearTextAI.Core.Models;
using ClearTextAI.Core.Services.Platform;

namespace ClearTextAI.Core.Services.Settings;

/// <summary>Settings persisted as JSON next to the history file. Holds no secrets.</summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IAppPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SettingsService(IAppPaths paths)
    {
        _paths = paths;

        // Read on construction rather than waiting for someone to call LoadAsync: a rewrite can be
        // triggered by the global shortcut before any page has opened, and it must use the real settings.
        Current = ReadFromDisk() ?? AppSettings.CreateDefault();
    }

    private AppSettings? ReadFromDisk()
    {
        try
        {
            return File.Exists(_paths.SettingsFilePath)
                ? JsonSerializer.Deserialize<AppSettings>(
                    File.ReadAllText(_paths.SettingsFilePath), JsonDefaults.Options)?.Normalize()
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public AppSettings Current { get; private set; }

    public event EventHandler? SettingsChanged;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(_paths.SettingsFilePath))
            {
                var json = await AtomicFile.ReadAllTextAsync(_paths.SettingsFilePath, cancellationToken).ConfigureAwait(false);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonDefaults.Options);
                Current = (loaded ?? AppSettings.CreateDefault()).Normalize();
            }
            else
            {
                Current = AppSettings.CreateDefault();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable file must not stop the app from starting.
            Current = AppSettings.CreateDefault();
        }
        finally
        {
            _gate.Release();
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
        return Current;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Current.Normalize();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(Current, JsonDefaults.Options);
            await AtomicFile.WriteAllTextAsync(_paths.SettingsFilePath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
