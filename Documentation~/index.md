# LLMConversationRuntime integration guide

LLMConversationRuntime is a provider-neutral state machine. A consumer owns provider calls, presentation, persistence, and authoritative game effects.

## Integration flow

1. Create at least two `ConversationParticipant` values.
2. Create a session with `ConversationRuntime.CreateSession`.
3. Read the current `ConversationTurnOpportunity`.
4. Give that projection to the matching provider or human UI.
5. Convert the response into a `ConversationAction` and submit it.
6. Continue until `ConversationSnapshot.State` is `Completed`.

## Authority boundary

```text
ConversationTurnOpportunity
        ↓
Provider or player input
        ↓
Optional ConversationQualityEvaluator
        ↓
ConversationAction
        ↓
ConversationRuntime.SubmitAction
        ↓
Accepted event and snapshot
        ↓
Game-owned validation of any ActionProposal
```

An accepted conversation action records dialogue state only. It does not authorize inventory, quest, wallet, combat, or world-state changes.

## Structured content visibility

Use `ConversationTurnContentProjection.Project` before presenting structured output:

| Visibility | Exposed fields |
|---|---|
| `Player` | Spoken text and presentation hints |
| `MindReading` | Player fields plus inner monologue |
| `Debug` | All fields, including action proposal and quality result |

## Lifecycle and stop reasons

Sessions complete when a participant requests stop, the host stops the session, every active participant passes, fewer than two participants remain, or `MaxTurns` is reached. Read `ConversationSnapshot.StopReason` for the deterministic reason code.

## Further reading

- [English README](../README.md)
- [한국어 README](../README.ko.md)
- [Changelog](../CHANGELOG.md)
- [Security policy](../SECURITY.md)
