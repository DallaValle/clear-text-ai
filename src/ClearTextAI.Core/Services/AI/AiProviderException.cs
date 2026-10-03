namespace ClearTextAI.Core.Services.AI;

/// <summary>
/// Every failure the UI is allowed to show. The message is always human-readable and never
/// contains the API key, the request body or the user's text.
/// </summary>
public sealed class AiProviderException : Exception
{
    public AiProviderException(AiErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public AiErrorKind Kind { get; }

    /// <summary>True for failures that are worth retrying unchanged.</summary>
    public bool IsTransient => Kind is AiErrorKind.RateLimited
        or AiErrorKind.Timeout
        or AiErrorKind.Network
        or AiErrorKind.ProviderUnavailable;

    public static AiProviderException For(AiErrorKind kind, string providerName, Exception? inner = null) =>
        new(kind, Capitalize(Describe(kind, providerName)), inner);

    /// <summary>Some provider names are not proper nouns ("the custom endpoint"), so fix the sentence start.</summary>
    private static string Capitalize(string message) =>
        message.Length > 0 && char.IsLower(message[0])
            ? char.ToUpperInvariant(message[0]) + message[1..]
            : message;

    private static string Describe(AiErrorKind kind, string provider) => kind switch
    {
        AiErrorKind.EmptyInput =>
            "There is nothing to improve yet. Paste or type some text first.",
        AiErrorKind.MissingApiKey =>
            $"No API key is configured for {provider}. Add one in Settings to start improving text.",
        AiErrorKind.MissingEndpoint =>
            $"No API endpoint is configured for {provider}. Add one in Settings, for example http://localhost:11434/v1.",
        AiErrorKind.MissingModel =>
            $"No model is configured for {provider}. Set one in Settings.",
        AiErrorKind.InvalidApiKey =>
            $"{provider} rejected the API key. Check the key in Settings, then try again.",
        AiErrorKind.AccessDenied =>
            $"{provider} accepted the key but refused the request. The usual cause is an account with no credits or a key without permission for this model. Check the billing page of your {provider} account.",
        AiErrorKind.RateLimited =>
            $"{provider} is rate limiting this key. Wait a moment and try again.",
        AiErrorKind.Timeout =>
            $"{provider} took too long to answer. Try again, or raise the timeout in Settings.",
        AiErrorKind.Network =>
            $"Unable to connect to {provider}. Check your internet connection and try again.",
        AiErrorKind.ProviderUnavailable =>
            $"{provider} is temporarily unavailable. Try again in a minute.",
        AiErrorKind.InvalidRequest =>
            $"{provider} could not process this request. Check the endpoint, the model name and the output token limit in Settings.",
        AiErrorKind.EmptyResponse =>
            $"{provider} returned an empty result. Try again, or lower the creativity setting.",
        AiErrorKind.ResponseTruncated =>
            $"The result was cut off because it hit the output token limit. Raise it in Settings or improve a shorter piece of text.",
        AiErrorKind.Refused =>
            $"{provider} declined to rewrite this text.",
        AiErrorKind.Cancelled =>
            "The request was cancelled.",
        AiErrorKind.CredentialStoreUnavailable =>
            "The system credential store is not available, so the API key could not be saved or read. Check that the Keychain or Credential Manager is unlocked, then re-enter the key in Settings.",
        _ =>
            $"Something went wrong while talking to {provider}. Try again.",
    };
}
