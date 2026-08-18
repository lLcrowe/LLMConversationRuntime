# LLMConversationRuntime

[한국어 README](README.ko.md)

A provider-neutral Unity Package Manager (UPM) runtime for deterministic conversations between LLM, human, and system participants.

```text
Provider adapter / Human UI / System event
                    ↓ ParticipantAction
ConversationRuntime
├─ session and participant state
├─ scene and private-context projection
├─ turn, recipient, and lifecycle validation
├─ quality and experiment evaluation
└─ events and snapshots
                    ↓
Consumer-owned game authority and presentation
```

The package does not call an LLM API, host MCP transport, or mutate game state. Consumers supply actions and decide how accepted proposals affect their game.

## Requirements

- Unity 6000.3 or newer
- No external runtime dependencies

## Install

In Unity Package Manager, select **Add package from git URL** and enter a fixed release tag:

```text
https://github.com/lLcrowe/LLMConversationRuntime.git#v0.1.3
```

For local development, select **Add package from disk** and choose this repository's `package.json`.

## Quick start

```csharp
using System.Collections.Generic;
using lLCroweTool.LLMConversation;

var runtime = new ConversationRuntime();
var snapshot = runtime.CreateSession(
    new List<ConversationParticipant>
    {
        new ConversationParticipant
        {
            ParticipantId = "buyer",
            DisplayName = "Buyer",
            Provider = "human",
            Kind = ConversationParticipantKind.Human
        },
        new ConversationParticipant
        {
            ParticipantId = "seller",
            DisplayName = "Seller",
            Provider = "local-model",
            Kind = ConversationParticipantKind.Llm
        }
    });

ConversationTurnOpportunity turn = runtime.GetTurnOpportunity(snapshot.SessionId);
ConversationAction action = ConversationAction.Speak(
    snapshot.SessionId,
    turn.ParticipantId,
    "I would like to discuss the delivery terms.");
ConversationOperationResult result = runtime.SubmitAction(action);
```

The provider or player UI reads the current `ConversationTurnOpportunity`, creates a `ConversationAction`, and submits it. Repeat until the returned snapshot is completed.

## Core API

| API | Responsibility |
|---|---|
| `ConversationRuntime.CreateSession` | Validate participants and create an autonomous or scene-guided session |
| `GetTurnOpportunity` | Project the current speaker, recent events, and allowed private scene context |
| `SubmitAction` | Validate and record speak, pass, leave, participant-request, or stop actions |
| `Pause`, `Resume`, `Stop` | Control the session lifecycle |
| `ConversationQualityEvaluator` | Detect configured role drift, scene violations, meta leakage, and repetition |
| `ConversationExperimentEvaluator` | Aggregate provider-neutral model metrics from a completed snapshot |
| `ConversationTurnContentProjection` | Limit spoken, inner-monologue, proposal, and debug data by visibility |

See [Documentation~/index.md](Documentation~/index.md) for the integration flow and authority boundary.

## Scene-guided conversations

`ConversationMode.SceneGuided` requires a `ConversationSceneContract`. The turn opportunity exposes public context to every participant and only the current participant's matching private context.

The runtime never interprets free text as an executable game command. Validate `ConversationTurnContent.ActionProposal` in a game-owned adapter before changing authoritative state.

## Sample

Import **Six Participant Negotiation** from Package Manager. Add `SixParticipantNegotiationExample` to a GameObject, enter Play Mode, and invoke **Advance Negotiation** from the component context menu to submit deterministic sample turns.

## Testing

Open **Window > General > Test Runner**, select Edit Mode, and run `lLcrowe.LLMConversation.Tests.EditMode`.

## Troubleshooting

| Symptom | Check |
|---|---|
| Package Manager cannot install the URL | Use the full Git URL with an existing `v0.1.3` tag and Unity 6000.3 or newer. |
| `CreateSession` throws | Supply at least two participants with unique IDs, unique display names, and non-empty providers. |
| `GetTurnOpportunity` returns `null` | The session is paused or completed. Inspect `GetSnapshot(sessionId).State`. |
| `SubmitAction` returns `out_of_turn` | Use the `ParticipantId` from the latest turn opportunity. |
| Scene-guided creation throws | Supply a non-null `ConversationSceneContract`. |
| Private context is missing | Add a `ParticipantPrivateContext` whose `ParticipantId` exactly matches the current participant. |

## Scope and limitations

- Included: in-memory sessions, turn scheduling, recipient validation, snapshots, structured content projection, quality checks, and experiment metrics.
- Excluded: provider SDKs, prompt construction, MCP transport, persistence, moderation policy, and authoritative game-state mutation.
- Threading: call the current runtime API from one owner thread; concurrent mutation is not supported.

## Related project

[GameRuntimeMcpHost](https://github.com/lLcrowe/GameRuntimeMcpHost) can expose a game-owned conversation adapter to MCP clients without adding transport concerns to this package.

## Contributing and security

See [CONTRIBUTING.md](CONTRIBUTING.md) before submitting changes. Report security issues according to [SECURITY.md](SECURITY.md).

## License

MIT — see [LICENSE](LICENSE).
