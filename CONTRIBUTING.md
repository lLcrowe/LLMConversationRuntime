# Contributing

LLMConversationRuntime accepts focused fixes and provider-neutral conversation features. Provider SDKs, MCP transport, persistence, and game-specific authority belong in consumer adapters.

## Development setup

1. Clone this repository.
2. Add it to a Unity 6.3 project with Package Manager's **Add package from disk** and select `package.json`.
3. Enable test assemblies and run `lLcrowe.LLMConversation.Tests.EditMode` in the Unity Test Runner.

## Contribution rules

1. Keep `Runtime/` independent from provider SDKs and game projects.
2. Preserve deterministic session, turn, and stop behavior.
3. Add XML documentation to new public APIs.
4. Add Edit Mode tests for contract changes.
5. Update `CHANGELOG.md` and keep its version aligned with `package.json`.

Open an issue before proposing a breaking public API or serialized-data change.
