using System;
using System.Collections.Generic;

namespace lLCroweTool.LLMConversation
{
    public enum ConversationMode
    {
        Autonomous,
        SceneGuided
    }

    public enum ConversationState
    {
        Active,
        Paused,
        Completed
    }

    public enum ConversationParticipantKind
    {
        Llm,
        Human,
        System
    }

    public enum ConversationActionKind
    {
        Speak,
        Pass,
        Leave,
        RequestParticipant,
        RequestStop
    }

    public enum ConversationEventKind
    {
        SessionStarted,
        Utterance,
        Passed,
        ParticipantLeft,
        ParticipantRequested,
        StopRequested,
        SessionPaused,
        SessionResumed,
        SessionCompleted
    }

    [Serializable]
    public sealed class ConversationParticipant
    {
        public string ParticipantId;
        public string DisplayName;
        public string Provider;
        public string PersonaReference;
        public string Role;
        public ConversationParticipantKind Kind;

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

    [Serializable]
    public sealed class ParticipantPrivateContext
    {
        public string ParticipantId;
        public string Context;
    }

    [Serializable]
    public sealed class ConversationSceneContract
    {
        public string Objective;
        public string PublicContext;
        public List<ParticipantPrivateContext> PrivateContextList =
            new List<ParticipantPrivateContext>();
        public List<string> StopConditionList = new List<string>();

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

    [Serializable]
    public sealed class ConversationPolicy
    {
        public int MaxTurns = 20;
        public int ContextWindowEvents = 12;
        public int MaxConsecutiveSpeaks = 2;

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

    [Serializable]
    public sealed class ConversationAction
    {
        public string ActionId;
        public string SessionId;
        public string ParticipantId;
        public ConversationActionKind Kind;
        public string Content;
        public List<string> RecipientIdList = new List<string>();
        public string RequestedParticipantId;
        public string ReplyToEventId;

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

    [Serializable]
    public sealed class ConversationEvent
    {
        public string EventId;
        public string SessionId;
        public int Sequence;
        public ConversationEventKind Kind;
        public string ActorId;
        public string ActorName;
        public string Content;
        public string ActionId;
        public string TargetParticipantId;
        public string Reason;
        public long CreatedAtUnixMs;

        public ConversationEvent Clone()
        {
            return (ConversationEvent)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class ConversationParticipantView
    {
        public string ParticipantId;
        public string DisplayName;
        public ConversationParticipantKind Kind;
    }

    [Serializable]
    public sealed class ConversationSceneView
    {
        public string Objective;
        public string Role;
        public string PublicContext;
        public string PrivateContext;
        public List<string> StopConditionList = new List<string>();
    }

    [Serializable]
    public sealed class ConversationTurnOpportunity
    {
        public string SessionId;
        public string ParticipantId;
        public int TurnNumber;
        public List<ConversationParticipantView> ParticipantList =
            new List<ConversationParticipantView>();
        public List<ConversationEvent> RecentEventList = new List<ConversationEvent>();
        public ConversationSceneView Scene;
    }

    [Serializable]
    public sealed class ConversationSnapshot
    {
        public string SessionId;
        public ConversationMode Mode;
        public ConversationState State;
        public int TurnCount;
        public string NextParticipantId;
        public string StopReason;
        public ConversationPolicy Policy;
        public ConversationSceneView Scene;
        public List<ConversationParticipant> ParticipantList =
            new List<ConversationParticipant>();
        public List<string> InactiveParticipantIdList = new List<string>();
        public List<ConversationEvent> EventList = new List<ConversationEvent>();
    }

    [Serializable]
    public sealed class ConversationOperationResult
    {
        public bool Success;
        public string Code;
        public string Message;
        public ConversationEvent Event;
        public ConversationSnapshot Snapshot;
    }
}
