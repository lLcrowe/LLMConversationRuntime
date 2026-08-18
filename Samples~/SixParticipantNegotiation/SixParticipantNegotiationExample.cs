using System.Collections.Generic;
using UnityEngine;

namespace lLCroweTool.LLMConversation.Samples
{
    /// <summary>
    /// Demonstrates a deterministic six-participant negotiation without an LLM provider.
    /// </summary>
    public sealed class SixParticipantNegotiationExample : MonoBehaviour
    {
        private readonly string[] scriptedUtteranceArray =
        {
            "We propose a unit price of 120 with delivery in fourteen days.",
            "We can discuss 120 if the warranty extends to two years.",
            "A two-year warranty is possible with a minimum order of 500 units.",
            "We can accept 500 units if delivery is split into two shipments.",
            "Two shipments are acceptable with the first arriving in ten days.",
            "Those terms work for us. Let us record the agreement."
        };

        private ConversationRuntime runtime;
        private string sessionId;
        private int scriptedUtteranceIndex;

        private void Start()
        {
            runtime = new ConversationRuntime();
            ConversationSnapshot snapshot = runtime.CreateSession(
                CreateParticipantList(),
                ConversationMode.SceneGuided,
                new ConversationSceneContract
                {
                    Objective = "Agree on price, warranty, delivery, and future business.",
                    PublicContext = "Three sellers and three buyers are negotiating a supply contract.",
                    StopConditionList = new List<string> { "All major terms are accepted." }
                },
                new ConversationPolicy { MaxTurns = 12 });
            sessionId = snapshot.SessionId;
            Debug.Log($"[LLMConversationRuntime] Session started: {sessionId}");
        }

        /// <summary>
        /// Submits the next scripted utterance for the participant that currently owns the turn.
        /// </summary>
        [ContextMenu("Advance Negotiation")]
        public void AdvanceNegotiation()
        {
            if (runtime == null || string.IsNullOrWhiteSpace(sessionId)) return;

            ConversationTurnOpportunity opportunity = runtime.GetTurnOpportunity(sessionId);
            if (opportunity == null)
            {
                ConversationSnapshot completedSnapshot = runtime.GetSnapshot(sessionId);
                Debug.Log($"[LLMConversationRuntime] Completed: {completedSnapshot.StopReason}");
                return;
            }

            string utterance = scriptedUtteranceArray[
                scriptedUtteranceIndex % scriptedUtteranceArray.Length];
            scriptedUtteranceIndex++;
            ConversationOperationResult result = runtime.SubmitAction(
                ConversationAction.Speak(sessionId, opportunity.ParticipantId, utterance));
            Debug.Log(
                $"[LLMConversationRuntime] {opportunity.ParticipantId}: {utterance} " +
                $"(result={result.Code}, next={result.Snapshot.NextParticipantId})");
        }

        private static List<ConversationParticipant> CreateParticipantList()
        {
            return new List<ConversationParticipant>
            {
                CreateParticipant("seller_lead", "Seller Lead", "seller"),
                CreateParticipant("buyer_lead", "Buyer Lead", "buyer"),
                CreateParticipant("seller_logistics", "Seller Logistics", "seller"),
                CreateParticipant("buyer_logistics", "Buyer Logistics", "buyer"),
                CreateParticipant("seller_legal", "Seller Legal", "seller"),
                CreateParticipant("buyer_legal", "Buyer Legal", "buyer")
            };
        }

        private static ConversationParticipant CreateParticipant(
            string participantId,
            string displayName,
            string role)
        {
            return new ConversationParticipant
            {
                ParticipantId = participantId,
                DisplayName = displayName,
                Provider = "scripted-sample",
                PersonaReference = role,
                Role = role,
                Kind = ConversationParticipantKind.System
            };
        }
    }
}
