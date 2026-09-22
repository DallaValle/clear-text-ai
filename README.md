# ClearText AI

A small, fast desktop writing assistant for Windows and macOS, built with .NET MAUI.

Paste text, press **Improve Text**, get a cleaner version back.
It corrects grammar and spelling, improves structure and readability, and keeps your meaning, your facts and your tone intact.
It is meant for work messages, emails, Slack and Teams posts, comments and other short pieces of text, not for chatting with a model.

---

## What it does

- Rewrites text while preserving meaning, facts, numbers, names, URLs and technical terminology.
- Corrects grammar, spelling and punctuation.
- Improves sentence structure, clarity and readability.
- Keeps your tone unless you ask for a different one.
- Makes the smallest set of changes that achieves the above.
- Sends your text to the configured AI provider only when you press **Improve Text**.

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download) or newer.
- The .NET MAUI workload for your platform.
- Windows 10 version 1809 (build 17763) or newer, or macOS 12 or newer.
- An API key for the provider you want to use.

Install the workload once:

```bash
dotnet workload restore
```

On Windows this installs `maui-windows`, on macOS `maui-maccatalyst`.

## Running it

```bash
git clone <this repository>
cd clear-text-ai
dotnet restore
dotnet run --project src/ClearTextAI.App
```

The app project targets only the platform you are building on: the Windows head on Windows, the Mac Catalyst head on macOS.
That means the solution builds cleanly on either machine without a cross-platform toolchain.

The Windows build is unpackaged (`WindowsPackageType=None`), so `dotnet run` starts it directly.
You can also open `ClearTextAI.slnx` in Visual Studio or Visual Studio Code and press run.

Run the tests with:

```bash
dotnet test
```

## First run

1. Open **Settings**, or press `Ctrl`/`Cmd` + `,`.
2. Pick a provider. Claude is selected by default.
3. Paste your API key and press **Save provider**.
   The key goes straight into the operating system credential store, never into a file in this repository or in your profile.
4. Optionally press **Test connection** to confirm the key, the model and the endpoint work together.
5. Go back, paste some text and press **Improve Text**.

Each settings section has its own save button and saves independently.
Saving the provider never writes half-edited rewrite options, and saving rewrite options never touches your provider or key.

Get a Claude API key from the [Anthropic Console](https://platform.claude.com/settings/keys).
Settings links straight to it, so you do not have to look the address up.

## Keyboard shortcuts

| Action | Windows | macOS |
| --- | --- | --- |
| Improve text | `Ctrl` + `Enter` | `Cmd` + `Enter` |
| Copy result | `Ctrl` + `Shift` + `C` | `Cmd` + `Shift` + `C` |
| Clear input | `Ctrl` + `L` | `Cmd` + `L` |
| Open settings | `Ctrl` + `,` | `Cmd` + `,` |
| Open history | `Ctrl` + `H` | `Cmd` + `H` |

## Rewrite options

Presets configure everything below in one click:

| Preset | What it does |
| --- | --- |
| Natural | Clear and human, keeps your voice. |
| Professional | Polished wording for work communication. |
| Friendly | Warm and approachable, still to the point. |
| Concise | Says the same thing in fewer words. |
| Grammar only | Fixes mistakes, changes nothing else. |
| Custom | Whatever combination you set by hand. |

Individual settings:

- **Creativity** controls how far the rewrite may move from your original wording.
- **More human to more polished** controls the register, from conversational to formal.
- **Preserve meaning** is on by default and forbids any change to facts, numbers, names or URLs.
- **Tone** can preserve your original tone or switch to friendly, professional, casual, formal or concise.
- **Custom instructions** are added to every rewrite, for example:

  > Keep my messages short and natural. I work in software development, so do not replace technical terms with generic language.

### A note on creativity and Claude models

Current Claude models no longer accept a `temperature` request parameter and reject it with an error.
The creativity slider therefore reaches the model through the prompt instead of through the sampling parameter, which keeps the control meaningful on every model.
For OpenAI and OpenAI-compatible endpoints the value is still sent as `temperature`.

## Providers

The app talks to providers through a single interface, so adding one does not touch the UI:

```csharp
public interface IAiProvider
{
    AiProviderKind Kind { get; }
    string DisplayName { get; }

    Task<string> RewriteTextAsync(
        string text,
        RewriteOptions options,
        CancellationToken cancellationToken = default);
}
```

Shipped implementations:

| Provider | Implementation | Default endpoint | Get a key |
| --- | --- | --- | --- |
| Claude / Anthropic | `ClaudeAiProvider`, via the official `Anthropic` SDK | `https://api.anthropic.com` | [platform.claude.com/settings/keys](https://platform.claude.com/settings/keys) |
| OpenAI | `OpenAiCompatibleProvider` | `https://api.openai.com/v1` | [platform.openai.com/api-keys](https://platform.openai.com/api-keys) |
| Grok / xAI | `OpenAiCompatibleProvider` | `https://api.x.ai/v1` | [console.x.ai](https://console.x.ai/team/default/api-keys) |
| Custom / OpenAI-compatible | `OpenAiCompatibleProvider` | You supply it | No key required |

Grok and OpenAI share one implementation because xAI serves the OpenAI chat completions shape.
A custom endpoint covers anything else that does too: a gateway, Ollama, LM Studio, a self-hosted model.
Settings links straight to the key page of whichever provider you select.

To add another provider, implement `IAiProvider` and register it in `MauiProgram.RegisterServices`.
`AiProviderResolver` picks the one the user selected, and nothing else changes.

## Testing without paying for anything

Claude, OpenAI and Grok all bill per token, and a brand new xAI or Anthropic account starts with no credits.
To try the app without spending anything, point the **Custom / OpenAI-compatible** provider at a free endpoint.

### Google Gemini, free key, no credit card

1. Get a key at [aistudio.google.com/apikey](https://aistudio.google.com/apikey).
2. In Settings choose **Custom / OpenAI-compatible** and fill in:

   | Field | Value |
   | --- | --- |
   | API endpoint | `https://generativelanguage.googleapis.com/v1beta/openai` |
   | Model | `gemini-3.8-flash` |
   | API key | the key from AI Studio |

   The endpoint is the OpenAI-compatible **base URL**, not the `.../models/<model>:generateContent`
   address shown in Google's own curl examples.
   The app appends `/chat/completions` itself.

3. Press **Save provider**, then **Test connection**.

### Groq, fast and free to start

Same idea, with endpoint `https://api.groq.com/openai/v1` and a model such as `llama-3.3-70b-versatile` or `openai/gpt-oss-20b`.
Get a key at [console.groq.com](https://console.groq.com/keys).
Note that Groq and Grok are unrelated companies despite the names.

### A local model, no account at all

Install [Ollama](https://ollama.com), run `ollama pull llama3.2`, then use endpoint `http://localhost:11434/v1`, model `llama3.2`, and leave the API key empty.
Everything stays on your machine and nothing is ever sent anywhere.

Expect weaker rewrites from a small local model than from a frontier one.
These options are for exercising the app end to end, not for judging the quality of the result.

## Where your data lives

| Data | Location |
| --- | --- |
| API keys | Windows Credential Manager, macOS Keychain. Never in a settings file. |
| Settings | `settings.json` in the local application data folder. |
| History | `history.json` in the same folder, capped at the 200 most recent rewrites. |

On Windows that folder is `%LOCALAPPDATA%\ClearTextAI`, on macOS it is inside the app container under `~/Library/Application Support`.

## Privacy

- Your text is sent to the configured provider only when you press **Improve Text**, and the main screen says so.
- Nothing is sent automatically, and there is no background processing.
- History is local. You can turn it off, delete single entries or clear all of it.
- Telemetry is off by default and collects nothing while it is off.
- API keys and request contents never appear in error messages or logs.

## Project structure

```text
clear-text-ai/
├── src/
│   ├── ClearTextAI.Core/          net10.0 library: everything that is not platform specific
│   │   ├── Models/                RewriteOptions, RewriteResult, AiProviderSettings, RewriteHistoryItem, ...
│   │   ├── Services/
│   │   │   ├── AI/                IAiProvider, ClaudeAiProvider, OpenAiCompatibleProvider, RewriteService
│   │   │   ├── Prompt/            IPromptBuilder, PromptBuilder
│   │   │   ├── Settings/          ISettingsService, SettingsService
│   │   │   ├── History/           IHistoryStore, JsonHistoryStore
│   │   │   ├── SecureStorage/     ISecureCredentialStore, IApiKeyStore
│   │   │   └── Platform/          Clipboard, dialog, navigation and path abstractions
│   │   └── ViewModels/            MainViewModel, SettingsViewModel, HistoryViewModel
│   │
│   └── ClearTextAI.App/           The .NET MAUI application
│       ├── Views/                 MainPage, SettingsPage, HistoryPage
│       ├── Services/              MAUI implementations of the Core abstractions
│       ├── Platforms/Windows/     WinUI head, Windows Credential Manager store
│       ├── Platforms/MacCatalyst/ Mac Catalyst head, Keychain store
│       └── Resources/             Colors, styles, icon, splash
│
└── tests/
    └── ClearTextAI.Core.Tests/    Unit tests for prompts, options, providers, errors and history
```

The split exists so the logic that matters can be unit tested on a plain `net10.0` target, without a MAUI host.
The app project holds views, platform implementations and dependency injection wiring only.

## Error handling

Every failure reaching the UI is an `AiProviderException` carrying an `AiErrorKind` and a message written for a person:

| Situation | What the user sees |
| --- | --- |
| No API key | No API key is configured for Claude. Add one in Settings to start improving text. |
| Invalid key | Claude rejected the API key. Check the key in Settings, then try again. |
| Network failure | Unable to connect to Claude. Check your internet connection and try again. |
| Rate limited | Claude is rate limiting this key. Wait a moment and try again. |
| Timeout | Claude took too long to answer. Try again, or raise the timeout in Settings. |
| Provider down | Claude is temporarily unavailable. Try again in a minute. |
| Empty input | There is nothing to improve yet. Paste or type some text first. |
| No endpoint for a custom provider | No API endpoint is configured for the custom endpoint. Add one in Settings, for example http://localhost:11434/v1. |
| No model | No model is configured for Claude. Set one in Settings. |
| Result cut off | The result was cut off because it hit the output token limit. Raise it in Settings or improve a shorter piece of text. |

Raw exceptions, request bodies and API keys are never shown or logged.

## Tests

`dotnet test` covers:

- Prompt construction: every required instruction, tone and style wording, custom instructions, the delimiters around the original text.
- Rewrite options: defaults, clamping, cloning, and each preset.
- The provider abstraction: the Claude and OpenAI-compatible providers against a stubbed transport.
- Error handling: status code mapping, cancellation, truncation, and the guarantee that no key or user text leaks into a message.
- History storage: round-trip, ordering, the size cap, deletion, clearing, concurrent writes and corrupt files.
- View models: counts, command availability, error presentation, presets and navigation.

## Designed to grow into

The architecture leaves room for these without restructuring:

- A global shortcut and "improve selected text" from other applications.
- A tray or menu bar mode.
- Streaming output, using the provider's streaming API behind the same interface.
- Multiple saved writing profiles, since `RewriteOptions` is already a serializable value.
- Language detection, translation and custom prompt templates, through `IPromptBuilder`.
- Markdown rendering in the output pane.

## Licence

Not yet decided.
