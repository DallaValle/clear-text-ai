using ClearTextAI.Core.Models;

namespace ClearTextAI.Core.Services.AI;

/// <summary>Picks the provider the user selected in settings.</summary>
public interface IAiProviderResolver
{
    IAiProvider Resolve();

    IAiProvider Resolve(AiProviderKind kind);
}
