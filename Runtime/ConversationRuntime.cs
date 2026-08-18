using System;
using System.Collections.Generic;

namespace lLCroweTool.LLMConversation
{
    /// <summary>
    /// Owns in-memory conversation sessions and deterministically validates their turn lifecycle.
    /// </summary>
    public sealed class ConversationRuntime
    {
        private sealed class SessionState
        {
            public string SessionId;
            public ConversationMode Mode;
            public ConversationState State;
            public ConversationPolicy Policy;
            public ConversationSceneContract Scene;
            public List<ConversationParticipant> ParticipantList;
            public readonly HashSet<string> InactiveParticipantIdSet =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly List<ConversationEvent> EventList = new List<ConversationEvent>();
            public readonly Dictionary<string, ConversationEvent> EventByActionId =
                new Dictionary<string, ConversationEvent>(StringComparer.Ordinal);
            public string NextParticipantId;
            public int TurnCount;
            public int PassStreak;
            public string LastSpeakerId;
            public int ConsecutiveSpeakCount;
            public string StopReason;
        }

        private readonly Dictionary<string, SessionState> sessionById =
            new Dictionary<string, SessionState>(StringComparer.Ordinal);
        private readonly Func<long> nowUnixMs;

        /// <summary>Occurs after a detached copy of a new session event has been recorded.</summary>
        public event Action<ConversationEvent> OnEventRecorded;

        /// <summary>Creates an empty runtime.</summary>
        /// <param name="nowUnixMs">An optional clock used to timestamp events in milliseconds.</param>
        public ConversationRuntime(Func<long> nowUnixMs = null)
        {
            this.nowUnixMs = nowUnixMs ??
                (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        /// <summary>Creates and starts a conversation session.</summary>
        /// <param name="participantList">At least two participants with unique IDs and display names.</param>
        /// <param name="mode">The conversation mode.</param>
        /// <param name="scene">The required contract for scene-guided mode.</param>
        /// <param name="policy">An optional policy; default values are used when null.</param>
        /// <param name="initialParticipantId">An optional participant that owns the first turn.</param>
        /// <returns>A detached snapshot of the created session.</returns>
        /// <exception cref="ArgumentException">Participant or scene requirements are not satisfied.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A policy value is invalid.</exception>
        public ConversationSnapshot CreateSession(
            IReadOnlyList<ConversationParticipant> participantList,
            ConversationMode mode = ConversationMode.Autonomous,
            ConversationSceneContract scene = null,
            ConversationPolicy policy = null,
            string initialParticipantId = null)
        {
            ValidateParticipants(participantList);
            if (mode == ConversationMode.SceneGuided && scene == null)
                throw new ArgumentException(
                    "Scene-guided conversations require a scene contract.",
                    nameof(scene));

            ConversationPolicy resolvedPolicy = policy ?? new ConversationPolicy();
            resolvedPolicy.Validate();
            string firstParticipantId = string.IsNullOrWhiteSpace(initialParticipantId)
                ? participantList[0].ParticipantId
                : initialParticipantId;
            if (FindParticipant(participantList, firstParticipantId) == null)
                throw new ArgumentException(
                    "The initial participant must belong to the session.",
                    nameof(initialParticipantId));

            var state = new SessionState
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Mode = mode,
                State = ConversationState.Active,
                Policy = resolvedPolicy,
                Scene = scene,
                ParticipantList = CloneParticipants(participantList),
                NextParticipantId = firstParticipantId
            };
            sessionById.Add(state.SessionId, state);
            RecordEvent(state, new ConversationEvent
            {
                Kind = ConversationEventKind.SessionStarted,
                Reason = mode.ToString()
            });
            return BuildSnapshot(state);
        }

        /// <summary>Returns a detached snapshot of an existing session.</summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <returns>The current session snapshot.</returns>
        /// <exception cref="ArgumentException">The session does not exist.</exception>
        public ConversationSnapshot GetSnapshot(string sessionId)
        {
            return BuildSnapshot(RequireSession(sessionId));
        }

        /// <summary>Projects the bounded context for the participant that owns the next turn.</summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <returns>The current opportunity, or null when the session is paused or completed.</returns>
        /// <exception cref="ArgumentException">The session does not exist.</exception>
        public ConversationTurnOpportunity GetTurnOpportunity(string sessionId)
        {
            SessionState state = RequireSession(sessionId);
            if (state.State != ConversationState.Active ||
                string.IsNullOrWhiteSpace(state.NextParticipantId))
                return null;

            ConversationParticipant current = FindParticipant(
                state.ParticipantList,
                state.NextParticipantId);
            var opportunity = new ConversationTurnOpportunity
            {
                SessionId = state.SessionId,
                ParticipantId = current.ParticipantId,
                TurnNumber = state.TurnCount + 1
            };

            for (int i = 0; i < state.ParticipantList.Count; i++)
            {
                ConversationParticipant participant = state.ParticipantList[i];
                if (state.InactiveParticipantIdSet.Contains(participant.ParticipantId))
                    continue;
                opportunity.ParticipantList.Add(new ConversationParticipantView
                {
                    ParticipantId = participant.ParticipantId,
                    DisplayName = participant.DisplayName,
                    Kind = participant.Kind
                });
            }

            int start = Math.Max(
                0,
                state.EventList.Count - state.Policy.ContextWindowEvents);
            for (int i = start; i < state.EventList.Count; i++)
                opportunity.RecentEventList.Add(state.EventList[i].Clone());

            if (state.Scene != null)
            {
                opportunity.Scene = new ConversationSceneView
                {
                    Objective = state.Scene.Objective,
                    Role = current.Role,
                    PublicContext = state.Scene.PublicContext,
                    PrivateContext = state.Scene.GetPrivateContext(current.ParticipantId),
                    StopConditionList = new List<string>(state.Scene.StopConditionList)
                };
            }

            return opportunity;
        }

        /// <summary>Validates, records, and applies one participant action.</summary>
        /// <param name="action">The action submitted by the current participant.</param>
        /// <returns>A stable result code, recorded event when applicable, and current snapshot.</returns>
        public ConversationOperationResult SubmitAction(ConversationAction action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.SessionId))
                return Failure("invalid_action", "A session action is required.", null);

            SessionState state;
            if (!sessionById.TryGetValue(action.SessionId, out state))
                return Failure("unknown_session", "The conversation session does not exist.", null);

            if (!string.IsNullOrWhiteSpace(action.ActionId) &&
                state.EventByActionId.TryGetValue(action.ActionId, out ConversationEvent prior))
            {
                return Success("duplicate", "The existing action result was returned.", state, prior);
            }

            if (state.State != ConversationState.Active)
                return Failure("session_inactive", "The conversation is not active.", state);
            if (!string.Equals(
                    state.NextParticipantId,
                    action.ParticipantId,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "out_of_turn",
                    $"Expected {state.NextParticipantId}, got {action.ParticipantId}.",
                    state);
            }

            ConversationParticipant actor = FindActiveParticipant(state, action.ParticipantId);
            if (actor == null)
                return Failure("participant_inactive", "The participant is not active.", state);

            ConversationOperationResult validation = ValidateAction(state, action);
            if (validation != null) return validation;

            state.TurnCount++;
            ConversationEvent recordedEvent = ApplyAction(state, actor, action);
            if (!string.IsNullOrWhiteSpace(action.ActionId))
                state.EventByActionId[action.ActionId] = recordedEvent;

            if (state.State == ConversationState.Active)
                AdvanceOrComplete(state, action);

            string code = state.State == ConversationState.Completed
                ? "completed"
                : "accepted";
            return Success(code, "The action was recorded.", state, recordedEvent);
        }

        /// <summary>Pauses an active session.</summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <returns>The lifecycle operation result.</returns>
        /// <exception cref="ArgumentException">The session does not exist.</exception>
        public ConversationOperationResult Pause(string sessionId)
        {
            SessionState state = RequireSession(sessionId);
            if (state.State != ConversationState.Active)
                return Failure("session_inactive", "Only active sessions can pause.", state);
            state.State = ConversationState.Paused;
            ConversationEvent recordedEvent = RecordEvent(state, new ConversationEvent
            {
                Kind = ConversationEventKind.SessionPaused,
                Reason = "paused_by_host"
            });
            return Success("paused", "The conversation was paused.", state, recordedEvent);
        }

        /// <summary>Resumes a paused session.</summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <returns>The lifecycle operation result.</returns>
        /// <exception cref="ArgumentException">The session does not exist.</exception>
        public ConversationOperationResult Resume(string sessionId)
        {
            SessionState state = RequireSession(sessionId);
            if (state.State != ConversationState.Paused)
                return Failure("session_not_paused", "Only paused sessions can resume.", state);
            state.State = ConversationState.Active;
            ConversationEvent recordedEvent = RecordEvent(state, new ConversationEvent
            {
                Kind = ConversationEventKind.SessionResumed,
                Reason = "resumed_by_host"
            });
            return Success("resumed", "The conversation resumed.", state, recordedEvent);
        }

        /// <summary>Completes a session with a host-owned reason code.</summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="reason">The terminal reason code.</param>
        /// <returns>The lifecycle operation result.</returns>
        /// <exception cref="ArgumentException">The session does not exist.</exception>
        public ConversationOperationResult Stop(
            string sessionId,
            string reason = "stopped_by_host")
        {
            SessionState state = RequireSession(sessionId);
            if (state.State == ConversationState.Completed)
                return Success("already_completed", "The conversation is already completed.", state, null);
            Complete(state, string.IsNullOrWhiteSpace(reason) ? "stopped_by_host" : reason);
            return Success(
                "completed",
                "The conversation was stopped.",
                state,
                state.EventList[state.EventList.Count - 1]);
        }

        /// <summary>Removes an active participant independently of turn ownership.</summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="participantId">The active participant to remove.</param>
        /// <returns>The operation result and resulting snapshot.</returns>
        /// <exception cref="ArgumentException">The session does not exist.</exception>
        public ConversationOperationResult LeaveParticipant(
            string sessionId,
            string participantId)
        {
            SessionState state = RequireSession(sessionId);
            if (state.State == ConversationState.Completed)
                return Failure("session_inactive", "The conversation is not active.", state);

            ConversationParticipant participant = FindActiveParticipant(state, participantId);
            if (participant == null)
                return Failure("participant_inactive", "The participant is not active.", state);

            state.InactiveParticipantIdSet.Add(participantId);
            state.PassStreak = 0;
            state.ConsecutiveSpeakCount = 0;
            ConversationEvent recordedEvent = RecordEvent(state, new ConversationEvent
            {
                Kind = ConversationEventKind.ParticipantLeft,
                ActorId = participant.ParticipantId,
                ActorName = participant.DisplayName,
                Reason = "participant_disconnected"
            });

            List<string> activeParticipantIdList = GetActiveParticipantIds(state);
            if (activeParticipantIdList.Count < 2)
                Complete(state, "insufficient_participants");
            else if (string.Equals(state.NextParticipantId, participantId, StringComparison.Ordinal))
                state.NextParticipantId = activeParticipantIdList[0];

            return Success(
                state.State == ConversationState.Completed ? "completed" : "left",
                "The participant left the conversation.",
                state,
                recordedEvent);
        }

        private ConversationOperationResult ValidateAction(
            SessionState state,
            ConversationAction action)
        {
            if (!Enum.IsDefined(typeof(ConversationActionKind), action.Kind))
                return Failure("invalid_action_kind", "The action kind is not supported.", state);

            if (action.Kind == ConversationActionKind.Speak &&
                string.IsNullOrWhiteSpace(action.Content))
                return Failure("blank_utterance", "Speak requires non-empty content.", state);

            List<string> recipientIdList = action.RecipientIdList ?? new List<string>();
            for (int i = 0; i < recipientIdList.Count; i++)
            {
                string recipientId = recipientIdList[i];
                if (string.Equals(recipientId, action.ParticipantId, StringComparison.Ordinal))
                    return Failure("self_recipient", "A participant cannot address itself.", state);
                if (FindActiveParticipant(state, recipientId) == null)
                    return Failure("invalid_recipient", "A recipient is not active.", state);
            }

            if (action.Kind == ConversationActionKind.RequestParticipant)
            {
                if (string.IsNullOrWhiteSpace(action.RequestedParticipantId) ||
                    string.Equals(
                        action.RequestedParticipantId,
                        action.ParticipantId,
                        StringComparison.Ordinal))
                    return Failure("invalid_participant_request", "A different active participant is required.", state);
                if (FindActiveParticipant(state, action.RequestedParticipantId) == null)
                    return Failure("invalid_participant_request", "The requested participant is not active.", state);
            }

            return null;
        }

        private ConversationEvent ApplyAction(
            SessionState state,
            ConversationParticipant actor,
            ConversationAction action)
        {
            var recordedEvent = new ConversationEvent
            {
                ActorId = actor.ParticipantId,
                ActorName = actor.DisplayName,
                Content = action.Content?.Trim(),
                ActionId = action.ActionId
            };
            switch (action.Kind)
            {
                case ConversationActionKind.Speak:
                    state.PassStreak = 0;
                    UpdateConsecutiveSpeaks(state, actor.ParticipantId);
                    recordedEvent.Kind = ConversationEventKind.Utterance;
                    break;
                case ConversationActionKind.Pass:
                    state.PassStreak++;
                    state.ConsecutiveSpeakCount = 0;
                    recordedEvent.Kind = ConversationEventKind.Passed;
                    break;
                case ConversationActionKind.Leave:
                    state.InactiveParticipantIdSet.Add(actor.ParticipantId);
                    state.PassStreak = 0;
                    state.ConsecutiveSpeakCount = 0;
                    recordedEvent.Kind = ConversationEventKind.ParticipantLeft;
                    break;
                case ConversationActionKind.RequestParticipant:
                    state.PassStreak = 0;
                    state.ConsecutiveSpeakCount = 0;
                    recordedEvent.Kind = ConversationEventKind.ParticipantRequested;
                    recordedEvent.TargetParticipantId = action.RequestedParticipantId;
                    break;
                default:
                    recordedEvent.Kind = ConversationEventKind.StopRequested;
                    recordedEvent.Reason = "participant_requested_stop";
                    break;
            }

            ConversationEvent result = RecordEvent(state, recordedEvent);
            if (action.Kind == ConversationActionKind.RequestStop)
                Complete(state, "participant_requested_stop");
            return result;
        }

        private void AdvanceOrComplete(SessionState state, ConversationAction action)
        {
            List<string> activeParticipantIdList = GetActiveParticipantIds(state);
            if (activeParticipantIdList.Count < 2)
            {
                Complete(state, "insufficient_participants");
                return;
            }

            if (state.PassStreak >= activeParticipantIdList.Count)
            {
                Complete(state, "all_participants_passed");
                return;
            }

            if (state.TurnCount >= state.Policy.MaxTurns)
            {
                Complete(state, "max_turns_reached");
                return;
            }

            if (action.Kind == ConversationActionKind.RequestParticipant)
            {
                state.NextParticipantId = action.RequestedParticipantId;
                return;
            }

            if (action.Kind == ConversationActionKind.Speak &&
                action.RecipientIdList != null &&
                action.RecipientIdList.Count > 0 &&
                state.ConsecutiveSpeakCount <= state.Policy.MaxConsecutiveSpeaks)
            {
                state.NextParticipantId = action.RecipientIdList[0];
                return;
            }

            int currentIndex = activeParticipantIdList.IndexOf(action.ParticipantId);
            state.NextParticipantId = currentIndex < 0
                ? activeParticipantIdList[0]
                : activeParticipantIdList[(currentIndex + 1) % activeParticipantIdList.Count];
        }

        private void Complete(SessionState state, string reason)
        {
            state.State = ConversationState.Completed;
            state.NextParticipantId = null;
            state.StopReason = reason;
            RecordEvent(state, new ConversationEvent
            {
                Kind = ConversationEventKind.SessionCompleted,
                Reason = reason
            });
        }

        private ConversationEvent RecordEvent(
            SessionState state,
            ConversationEvent conversationEvent)
        {
            conversationEvent.EventId = Guid.NewGuid().ToString("N");
            conversationEvent.SessionId = state.SessionId;
            conversationEvent.Sequence = state.EventList.Count + 1;
            conversationEvent.CreatedAtUnixMs = nowUnixMs();
            state.EventList.Add(conversationEvent);
            OnEventRecorded?.Invoke(conversationEvent.Clone());
            return conversationEvent;
        }

        private static void UpdateConsecutiveSpeaks(SessionState state, string actorId)
        {
            if (string.Equals(state.LastSpeakerId, actorId, StringComparison.Ordinal))
                state.ConsecutiveSpeakCount++;
            else
            {
                state.LastSpeakerId = actorId;
                state.ConsecutiveSpeakCount = 1;
            }
        }

        private static void ValidateParticipants(
            IReadOnlyList<ConversationParticipant> participantList)
        {
            if (participantList == null || participantList.Count < 2)
                throw new ArgumentException("A conversation requires at least two participants.");

            var idSet = new HashSet<string>(StringComparer.Ordinal);
            var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < participantList.Count; i++)
            {
                ConversationParticipant participant = participantList[i];
                if (participant == null ||
                    string.IsNullOrWhiteSpace(participant.ParticipantId) ||
                    string.IsNullOrWhiteSpace(participant.DisplayName) ||
                    string.IsNullOrWhiteSpace(participant.Provider))
                    throw new ArgumentException("Participants require IDs, names, and providers.");
                if (!idSet.Add(participant.ParticipantId))
                    throw new ArgumentException("Participant IDs must be unique.");
                if (!nameSet.Add(participant.DisplayName))
                    throw new ArgumentException("Participant display names must be unique.");
            }
        }

        private static ConversationParticipant FindParticipant(
            IReadOnlyList<ConversationParticipant> participantList,
            string participantId)
        {
            for (int i = 0; i < participantList.Count; i++)
            {
                ConversationParticipant participant = participantList[i];
                if (string.Equals(
                        participant.ParticipantId,
                        participantId,
                        StringComparison.Ordinal))
                    return participant;
            }

            return null;
        }

        private static ConversationParticipant FindActiveParticipant(
            SessionState state,
            string participantId)
        {
            return state.InactiveParticipantIdSet.Contains(participantId)
                ? null
                : FindParticipant(state.ParticipantList, participantId);
        }

        private static List<ConversationParticipant> CloneParticipants(
            IReadOnlyList<ConversationParticipant> participantList)
        {
            var cloneList = new List<ConversationParticipant>(participantList.Count);
            for (int i = 0; i < participantList.Count; i++)
                cloneList.Add(participantList[i].Clone());
            return cloneList;
        }

        private static List<string> GetActiveParticipantIds(SessionState state)
        {
            var participantIdList = new List<string>();
            for (int i = 0; i < state.ParticipantList.Count; i++)
            {
                string participantId = state.ParticipantList[i].ParticipantId;
                if (!state.InactiveParticipantIdSet.Contains(participantId))
                    participantIdList.Add(participantId);
            }
            return participantIdList;
        }

        private SessionState RequireSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) ||
                !sessionById.TryGetValue(sessionId, out SessionState state))
                throw new ArgumentException("The conversation session does not exist.", nameof(sessionId));
            return state;
        }

        private static ConversationSnapshot BuildSnapshot(SessionState state)
        {
            var snapshot = new ConversationSnapshot
            {
                SessionId = state.SessionId,
                Mode = state.Mode,
                State = state.State,
                TurnCount = state.TurnCount,
                NextParticipantId = state.NextParticipantId,
                StopReason = state.StopReason,
                Policy = new ConversationPolicy
                {
                    MaxTurns = state.Policy.MaxTurns,
                    ContextWindowEvents = state.Policy.ContextWindowEvents,
                    MaxConsecutiveSpeaks = state.Policy.MaxConsecutiveSpeaks
                },
                Scene = state.Scene == null
                    ? null
                    : new ConversationSceneView
                    {
                        Objective = state.Scene.Objective,
                        PublicContext = state.Scene.PublicContext,
                        StopConditionList = new List<string>(state.Scene.StopConditionList)
                    },
                ParticipantList = CloneParticipants(state.ParticipantList),
                InactiveParticipantIdList = new List<string>(state.InactiveParticipantIdSet)
            };
            for (int i = 0; i < state.EventList.Count; i++)
                snapshot.EventList.Add(state.EventList[i].Clone());
            return snapshot;
        }

        private static ConversationOperationResult Success(
            string code,
            string message,
            SessionState state,
            ConversationEvent conversationEvent)
        {
            return new ConversationOperationResult
            {
                Success = true,
                Code = code,
                Message = message,
                Event = conversationEvent?.Clone(),
                Snapshot = BuildSnapshot(state)
            };
        }

        private static ConversationOperationResult Failure(
            string code,
            string message,
            SessionState state)
        {
            return new ConversationOperationResult
            {
                Success = false,
                Code = code,
                Message = message,
                Snapshot = state == null ? null : BuildSnapshot(state)
            };
        }
    }
}
