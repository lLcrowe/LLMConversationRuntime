using System;
using System.Collections.Generic;

namespace lLCroweTool.LLMConversation
{
    /// <summary>Defines how strongly a session is constrained by a scene contract.</summary>
    public enum ConversationMode
    {
        /// <summary>Participants converse without a required scene contract.</summary>
        Autonomous,
        /// <summary>Participants receive objective, role, context, and stop-condition projections.</summary>
        SceneGuided
    }

    /// <summary>Describes the lifecycle state of a conversation session.</summary>
    public enum ConversationState
    {
        /// <summary>The session accepts actions.</summary>
        Active,
        /// <summary>The session is temporarily suspended.</summary>
        Paused,
        /// <summary>The session reached a terminal state.</summary>
        Completed
    }

    /// <summary>Identifies the source category of a conversation participant.</summary>
    public enum ConversationParticipantKind
    {
        /// <summary>A participant driven by a language-model adapter.</summary>
        Llm,
        /// <summary>A participant driven by human input.</summary>
        Human,
        /// <summary>A participant driven by deterministic system logic.</summary>
        System
    }

    /// <summary>Defines the actions a participant can submit during its turn.</summary>
    public enum ConversationActionKind
    {
        /// <summary>Records a non-empty utterance.</summary>
        Speak,
        /// <summary>Yields the current turn without speaking.</summary>
        Pass,
        /// <summary>Removes the submitting participant from active scheduling.</summary>
        Leave,
        /// <summary>Requests a specific active participant as the next speaker.</summary>
        RequestParticipant,
        /// <summary>Requests deterministic completion of the session.</summary>
        RequestStop
    }

    /// <summary>Identifies an immutable event recorded in a conversation session.</summary>
    public enum ConversationEventKind
    {
        /// <summary>The session was created.</summary>
        SessionStarted,
        /// <summary>A participant spoke.</summary>
        Utterance,
        /// <summary>A participant passed its turn.</summary>
        Passed,
        /// <summary>A participant left the active session.</summary>
        ParticipantLeft,
        /// <summary>A participant requested another speaker.</summary>
        ParticipantRequested,
        /// <summary>A participant requested session completion.</summary>
        StopRequested,
        /// <summary>The host paused the session.</summary>
        SessionPaused,
        /// <summary>The host resumed the session.</summary>
        SessionResumed,
        /// <summary>The session reached a terminal state.</summary>
        SessionCompleted
    }

    /// <summary>Defines an LLM, human, or system participant in a session.</summary>
    [Serializable]
    public sealed class ConversationParticipant
    {
        /// <summary>Gets or sets the stable identifier used by actions and events.</summary>
        public string ParticipantId;
        /// <summary>Gets or sets the user-facing participant name.</summary>
        public string DisplayName;
        /// <summary>Gets or sets the provider or adapter identifier.</summary>
        public string Provider;
        /// <summary>Gets or sets the persona or model grouping identifier.</summary>
        public string PersonaReference;
        /// <summary>Gets or sets the role projected into scene-guided turns.</summary>
        public string Role;
        /// <summary>Gets or sets the participant source category.</summary>
        public ConversationParticipantKind Kind;

        /// <summary>Creates a detached copy of this participant.</summary>
        /// <returns>A participant with the same field values.</returns>
        public ConversationParticipant Clone()
        {
            return new ConversationParticipant
            {
                ParticipantId = ParticipantId,
                DisplayName = DisplayName,
                Provider = Provider,
                PersonaReference = PersonaReference,
                Role = Role,
                Kind = Kind
            };
        }
    }

    /// <summary>Associates private scene context with one participant.</summary>
    [Serializable]
    public sealed class ParticipantPrivateContext
    {
        /// <summary>Gets or sets the participant that may receive the context.</summary>
        public string ParticipantId;
        /// <summary>Gets or sets the participant-only context.</summary>
        public string Context;
    }

    /// <summary>Defines the objective and context projected by a scene-guided session.</summary>
    [Serializable]
    public sealed class ConversationSceneContract
    {
        /// <summary>Gets or sets the shared conversational objective.</summary>
        public string Objective;
        /// <summary>Gets or sets context visible to every active participant.</summary>
        public string PublicContext;
        /// <summary>Gets the participant-specific context entries.</summary>
        public List<ParticipantPrivateContext> PrivateContextList =
            new List<ParticipantPrivateContext>();
        /// <summary>Gets the human-readable conditions that indicate the scene may stop.</summary>
        public List<string> StopConditionList = new List<string>();

        /// <summary>Returns private context for an exact participant identifier.</summary>
        /// <param name="participantId">The participant identifier to match.</param>
        /// <returns>The matching context, or an empty string when none exists.</returns>
        public string GetPrivateContext(string participantId)
        {
            for (int i = 0; i < PrivateContextList.Count; i++)
            {
                ParticipantPrivateContext entry = PrivateContextList[i];
                if (entry != null && string.Equals(
                        entry.ParticipantId,
                        participantId,
                        StringComparison.Ordinal))
                    return entry.Context ?? string.Empty;
            }

            return string.Empty;
        }
    }

    /// <summary>Controls turn limits and context projection for a session.</summary>
    [Serializable]
    public sealed class ConversationPolicy
    {
        /// <summary>Gets or sets the maximum number of submitted participant turns.</summary>
        public int MaxTurns = 20;
        /// <summary>Gets or sets the maximum number of recent events projected into a turn.</summary>
        public int ContextWindowEvents = 12;
        /// <summary>Gets or sets the maximum consecutive directed turns for one speaker.</summary>
        public int MaxConsecutiveSpeaks = 2;

        /// <summary>Validates that every numeric policy value is positive.</summary>
        /// <exception cref="ArgumentOutOfRangeException">A policy value is less than one.</exception>
        public void Validate()
        {
            if (MaxTurns < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxTurns));
            if (ContextWindowEvents < 1)
                throw new ArgumentOutOfRangeException(nameof(ContextWindowEvents));
            if (MaxConsecutiveSpeaks < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxConsecutiveSpeaks));
        }
    }

    /// <summary>Represents one participant request submitted to a conversation session.</summary>
    [Serializable]
    public sealed class ConversationAction
    {
        /// <summary>Gets or sets the idempotency key for this action.</summary>
        public string ActionId;
        /// <summary>Gets or sets the target session identifier.</summary>
        public string SessionId;
        /// <summary>Gets or sets the submitting participant identifier.</summary>
        public string ParticipantId;
        /// <summary>Gets or sets the requested action kind.</summary>
        public ConversationActionKind Kind;
        /// <summary>Gets or sets the spoken content when <see cref="Kind"/> is <see cref="ConversationActionKind.Speak"/>.</summary>
        public string Content;
        /// <summary>Gets the optional addressed participant identifiers.</summary>
        public List<string> RecipientIdList = new List<string>();
        /// <summary>Gets or sets the requested next participant identifier.</summary>
        public string RequestedParticipantId;
        /// <summary>Gets or sets an optional event identifier this action replies to.</summary>
        public string ReplyToEventId;

        /// <summary>Creates a speak action with a generated idempotency key.</summary>
        /// <param name="sessionId">The target session identifier.</param>
        /// <param name="participantId">The submitting participant identifier.</param>
        /// <param name="content">The non-empty utterance.</param>
        /// <param name="recipientId">An optional addressed participant identifier.</param>
        /// <returns>A new speak action.</returns>
        public static ConversationAction Speak(
            string sessionId,
            string participantId,
            string content,
            string recipientId = null)
        {
            var action = new ConversationAction
            {
                ActionId = Guid.NewGuid().ToString("N"),
                SessionId = sessionId,
                ParticipantId = participantId,
                Kind = ConversationActionKind.Speak,
                Content = content
            };
            if (!string.IsNullOrWhiteSpace(recipientId))
                action.RecipientIdList.Add(recipientId);
            return action;
        }
    }

    /// <summary>Represents an immutable event returned from a conversation session.</summary>
    [Serializable]
    public sealed class ConversationEvent
    {
        /// <summary>Gets or sets the generated event identifier.</summary>
        public string EventId;
        /// <summary>Gets or sets the owning session identifier.</summary>
        public string SessionId;
        /// <summary>Gets or sets the one-based order in the session event stream.</summary>
        public int Sequence;
        /// <summary>Gets or sets the event kind.</summary>
        public ConversationEventKind Kind;
        /// <summary>Gets or sets the participant that caused the event.</summary>
        public string ActorId;
        /// <summary>Gets or sets the actor display name captured at record time.</summary>
        public string ActorName;
        /// <summary>Gets or sets the event content.</summary>
        public string Content;
        /// <summary>Gets or sets the source action idempotency key.</summary>
        public string ActionId;
        /// <summary>Gets or sets the participant targeted by a request.</summary>
        public string TargetParticipantId;
        /// <summary>Gets or sets a deterministic lifecycle reason code.</summary>
        public string Reason;
        /// <summary>Gets or sets the UTC Unix timestamp in milliseconds.</summary>
        public long CreatedAtUnixMs;

        /// <summary>Creates a detached copy of this event.</summary>
        /// <returns>An event with the same field values.</returns>
        public ConversationEvent Clone()
        {
            return (ConversationEvent)MemberwiseClone();
        }
    }

    /// <summary>Provides the public identity fields projected into a turn.</summary>
    [Serializable]
    public sealed class ConversationParticipantView
    {
        /// <summary>Gets or sets the participant identifier.</summary>
        public string ParticipantId;
        /// <summary>Gets or sets the display name.</summary>
        public string DisplayName;
        /// <summary>Gets or sets the participant source category.</summary>
        public ConversationParticipantKind Kind;
    }

    /// <summary>Provides the scene information visible to the current participant.</summary>
    [Serializable]
    public sealed class ConversationSceneView
    {
        /// <summary>Gets or sets the shared objective.</summary>
        public string Objective;
        /// <summary>Gets or sets the current participant's role.</summary>
        public string Role;
        /// <summary>Gets or sets context visible to all participants.</summary>
        public string PublicContext;
        /// <summary>Gets or sets context visible only to the current participant.</summary>
        public string PrivateContext;
        /// <summary>Gets the projected human-readable stop conditions.</summary>
        public List<string> StopConditionList = new List<string>();
    }

    /// <summary>Contains the bounded context required to produce the next participant action.</summary>
    [Serializable]
    public sealed class ConversationTurnOpportunity
    {
        /// <summary>Gets or sets the session identifier.</summary>
        public string SessionId;
        /// <summary>Gets or sets the participant that owns the turn.</summary>
        public string ParticipantId;
        /// <summary>Gets or sets the one-based turn number.</summary>
        public int TurnNumber;
        /// <summary>Gets the active participant views.</summary>
        public List<ConversationParticipantView> ParticipantList =
            new List<ConversationParticipantView>();
        /// <summary>Gets the bounded recent event history.</summary>
        public List<ConversationEvent> RecentEventList = new List<ConversationEvent>();
        /// <summary>Gets or sets the optional scene projection.</summary>
        public ConversationSceneView Scene;
    }

    /// <summary>Provides a detached, serializable view of a complete conversation session.</summary>
    [Serializable]
    public sealed class ConversationSnapshot
    {
        /// <summary>Gets or sets the session identifier.</summary>
        public string SessionId;
        /// <summary>Gets or sets the session mode.</summary>
        public ConversationMode Mode;
        /// <summary>Gets or sets the lifecycle state.</summary>
        public ConversationState State;
        /// <summary>Gets or sets the number of submitted participant turns.</summary>
        public int TurnCount;
        /// <summary>Gets or sets the participant that owns the next turn.</summary>
        public string NextParticipantId;
        /// <summary>Gets or sets the terminal reason code.</summary>
        public string StopReason;
        /// <summary>Gets or sets the copied session policy.</summary>
        public ConversationPolicy Policy;
        /// <summary>Gets or sets the public scene snapshot.</summary>
        public ConversationSceneView Scene;
        /// <summary>Gets the copied participant list.</summary>
        public List<ConversationParticipant> ParticipantList =
            new List<ConversationParticipant>();
        /// <summary>Gets the inactive participant identifiers.</summary>
        public List<string> InactiveParticipantIdList = new List<string>();
        /// <summary>Gets the copied event stream.</summary>
        public List<ConversationEvent> EventList = new List<ConversationEvent>();
    }

    /// <summary>Reports the result of an action or lifecycle operation.</summary>
    [Serializable]
    public sealed class ConversationOperationResult
    {
        /// <summary>Gets or sets whether the operation succeeded.</summary>
        public bool Success;
        /// <summary>Gets or sets the stable machine-readable result code.</summary>
        public string Code;
        /// <summary>Gets or sets the human-readable result message.</summary>
        public string Message;
        /// <summary>Gets or sets the event recorded by the operation, when applicable.</summary>
        public ConversationEvent Event;
        /// <summary>Gets or sets the resulting session snapshot.</summary>
        public ConversationSnapshot Snapshot;
    }
}
