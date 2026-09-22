using ClearTextAI.Core.Models;

namespace ClearTextAI.Core.Services.Settings;

public interface ISettingsService
{
    /// <summary>Always usable: defaults until <see cref="LoadAsync"/> has run.</summary>
    AppSettings Current { get; }

    event EventHandler? SettingsChanged;

    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}
