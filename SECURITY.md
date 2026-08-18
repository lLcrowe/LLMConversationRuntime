# Security Policy

## Supported versions

Security fixes are applied to the latest tagged release.

## Reporting a vulnerability

Please report vulnerabilities privately through the repository's GitHub Security Advisory page. Do not open a public issue for private-context disclosure, authorization-boundary bypasses, or unsafe action execution.

Include the affected version, reproduction steps, expected impact, and any suggested mitigation. Do not include real prompts, credentials, tokens, or player data.

## Security boundary

- The runtime schedules and validates conversation actions; it does not call an LLM provider.
- Private scene context is projected only to the current participant's turn opportunity.
- `ActionProposal` is data and must not directly mutate wallet, inventory, quest, or world state.
- Consumers remain responsible for authorization, persistence, content moderation, and authoritative game-state validation.
