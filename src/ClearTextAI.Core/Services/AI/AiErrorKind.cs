namespace ClearTextAI.Core.Services.AI;

public enum AiErrorKind
{
    Unknown = 0,
    EmptyInput,
    MissingApiKey,
    MissingEndpoint,
    MissingModel,
    InvalidApiKey,
    AccessDenied,
    RateLimited,
    Timeout,
    Network,
    ProviderUnavailable,
    InvalidRequest,
    EmptyResponse,
    ResponseTruncated,
    Refused,
    Cancelled,
    CredentialStoreUnavailable,
}
