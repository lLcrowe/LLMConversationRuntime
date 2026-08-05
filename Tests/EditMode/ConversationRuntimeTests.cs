using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace lLCroweTool.LLMConversation.Tests
{
    [Category("Core")]
    public sealed class ConversationRuntimeTests
    {
        [Test]
        public void CreateSession_SceneGuided_ProjectsOnlyCurrentPrivateContext()
        {
            ConversationRuntime runtime = CreateRuntime();
            List<ConversationParticipant> participantList = CreateParticipants(2);
            ConversationSceneContract scene = CreateScene(participantList);

            ConversationSnapshot snapshot = runtime.CreateSession(
                participantList,
                ConversationMode.SceneGuided,
                scene);
            ConversationTurnOpportunity opportunity =
                runtime.GetTurnOpportunity(snapshot.SessionId);

            Assert.AreEqual(participantList[0].ParticipantId, opportunity.ParticipantId);
            Assert.AreEqual("private-0", opportunity.Scene.PrivateContext);
            Assert.AreNotEqual("private-1", opportunity.Scene.PrivateContext);
            Assert.AreEqual(2, opportunity.ParticipantList.Count);
        }

        [Test]
        public void SubmitAction_OutOfTurn_IsRejectedWithoutAdvancing()
        {
            ConversationRuntime runtime = CreateRuntime();
            List<ConversationParticipant> participantList = CreateParticipants(2);
            ConversationSnapshot snapshot = runtime.CreateSession(participantList);

            ConversationOperationResult result = runtime.SubmitAction(
                ConversationAction.Speak(
                    snapshot.SessionId,
                    participantList[1].ParticipantId,
                    "too early"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual("out_of_turn", result.Code);
            Assert.AreEqual(0, result.Snapshot.TurnCount);
        }

        [Test]
        public void SubmitAction_DuplicateActionId_ReturnsExistingEventOnce()
        {
            ConversationRuntime runtime = CreateRuntime();
            List<ConversationParticipant> participantList = CreateParticipants(2);
            ConversationSnapshot snapshot = runtime.CreateSession(participantList);
            ConversationAction action = ConversationAction.Speak(
                snapshot.SessionId,
                participantList[0].ParticipantId,
                "hello");

            ConversationOperationResult first = runtime.SubmitAction(action);
            ConversationOperationResult duplicate = runtime.SubmitAction(action);

            Assert.IsTrue(first.Success);
            Assert.IsTrue(duplicate.Success);
            Assert.AreEqual("duplicate", duplicate.Code);
            Assert.AreEqual(first.Event.EventId, duplicate.Event.EventId);
            Assert.AreEqual(first.Snapshot.EventList.Count, duplicate.Snapshot.EventList.Count);
        }

        [Test]
        public void SubmitAction_RequestParticipant_TransfersNextTurn()
        {
            ConversationRuntime runtime = CreateRuntime();
            List<ConversationParticipant> participantList = CreateParticipants(3);
            ConversationSnapshot snapshot = runtime.CreateSession(participantList);
            var action = new ConversationAction
            {
                ActionId = "request-third",
                SessionId = snapshot.SessionId,
                ParticipantId = participantList[0].ParticipantId,
                Kind = ConversationActionKind.RequestParticipant,
                RequestedParticipantId = participantList[2].ParticipantId
            };

            ConversationOperationResult result = runtime.SubmitAction(action);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(participantList[2].ParticipantId, result.Snapshot.NextParticipantId);
        }

        [Test]
        public void SubmitAction_RequestStop_CompletesAtCurrentTurn()
        {
            ConversationRuntime runtime = CreateRuntime();
            List<ConversationParticipant> participantList = CreateParticipants(2);
            ConversationSnapshot snapshot = runtime.CreateSession(participantList);
            var action = new ConversationAction
            {
                ActionId = "deal-accepted",
                SessionId = snapshot.SessionId,
                ParticipantId = participantList[0].ParticipantId,
                Kind = ConversationActionKind.RequestStop,
                Content = "295골드에 수락합니다."
            };

            ConversationOperationResult result = runtime.SubmitAction(action);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ConversationState.Completed, result.Snapshot.State);
            Assert.AreEqual(1, result.Snapshot.TurnCount);
            Assert.AreEqual("participant_requested_stop", result.Snapshot.StopReason);
            Assert.IsNull(result.Snapshot.NextParticipantId);
        }

        [Test]
        public void SubmitAction_AllParticipantsPass_CompletesConversation()
        {
            ConversationRuntime runtime = CreateRuntime();
            List<ConversationParticipant> participantList = CreateParticipants(2);
            ConversationSnapshot snapshot = runtime.CreateSession(participantList);

            ConversationOperationResult first = runtime.SubmitAction(Pass(
                snapshot.SessionId,
                participantList[0].ParticipantId,
                "pass-0"));
            ConversationOperationResult second = runtime.SubmitAction(Pass(
                snapshot.SessionId,
                participantList[1].ParticipantId,
                "pass-1"));

            Assert.IsTrue(first.Success);
            Assert.AreEqual(ConversationState.Completed, second.Snapshot.State);
            Assert.AreEqual("all_participants_passed", second.Snapshot.StopReason);
        }

        [Test]
        public void SubmitAction_LeaveUntilOneParticipant_CompletesConversation()
        {
            ConversationRuntime runtime = CreateRuntime();
            List<ConversationParticipant> participantList = CreateParticipants(2);
            ConversationSnapshot snapshot = runtime.CreateSession(participantList);
            var action = new ConversationAction
            {
                ActionId = "leave-0",
                SessionId = snapshot.SessionId,
                ParticipantId = participantList[0].ParticipantId,
                Kind = ConversationActionKind.Leave
            };

            ConversationOperationResult result = runtime.SubmitAction(action);

            Assert.AreEqual(ConversationState.Completed, result.Snapshot.State);
            Assert.AreEqual("insufficient_participants", result.Snapshot.StopReason);
        }

        [Test]
        public void LeaveParticipant_OutOfTurn_RemovesParticipantAndKeepsTurnOwner()
        {
            ConversationRuntime runtime = CreateRuntime();
            List<ConversationParticipant> participantList = CreateParticipants(3);
            ConversationSnapshot snapshot = runtime.CreateSession(participantList);

            ConversationOperationResult result = runtime.LeaveParticipant(
                snapshot.SessionId,
                participantList[2].ParticipantId);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("left", result.Code);
            Assert.AreEqual(participantList[0].ParticipantId, result.Snapshot.NextParticipantId);
            Assert.AreEqual(ConversationState.Active, result.Snapshot.State);
        }

        [Test]
        public void SubmitAction_MaxTurns_CompletesConversation()
        {
            ConversationRuntime runtime = CreateRuntime();
            List<ConversationParticipant> participantList = CreateParticipants(2);
            ConversationSnapshot snapshot = runtime.CreateSession(
                participantList,
                policy: new ConversationPolicy { MaxTurns = 2 });

            runtime.SubmitAction(ConversationAction.Speak(
                snapshot.SessionId,
                participantList[0].ParticipantId,
                "first"));
            ConversationOperationResult result = runtime.SubmitAction(
                ConversationAction.Speak(
                    snapshot.SessionId,
                    participantList[1].ParticipantId,
                    "second"));

            Assert.AreEqual(ConversationState.Completed, result.Snapshot.State);
            Assert.AreEqual("max_turns_reached", result.Snapshot.StopReason);
        }

        [TestCase(2)]
        [TestCase(6)]
        public void CreateSession_VariableParticipantCounts_UsesDataCount(int count)
        {
            ConversationRuntime runtime = CreateRuntime();
            List<ConversationParticipant> participantList = CreateParticipants(count);

            ConversationSnapshot snapshot = runtime.CreateSession(participantList);

            Assert.AreEqual(count, snapshot.ParticipantList.Count);
        }

        [Test]
        public void QualityEvaluator_CustomerUsesMerchantPhrase_ReturnsRoleDriftRetry()
        {
            ConversationQualityEvaluator evaluator = new ConversationQualityEvaluator();
            ConversationQualityContract contract = CreateQualityContract();

            ConversationQualityResult result = evaluator.Evaluate(
                CreateQualityOpportunity("customer"),
                "내가 시키는 심부름을 해라.",
                contract);

            Assert.AreEqual(ConversationQualityDecision.Retry, result.Decision);
            Assert.AreEqual(ConversationQualityIssueKind.RoleDrift, result.IssueList[0].Kind);
        }

        [Test]
        public void QualityEvaluator_MetaLeak_ReturnsRetry()
        {
            ConversationQualityEvaluator evaluator = new ConversationQualityEvaluator();

            ConversationQualityResult result = evaluator.Evaluate(
                CreateQualityOpportunity("customer"),
                "시스템 프롬프트를 따르겠습니다.",
                CreateQualityContract());

            Assert.AreEqual(ConversationQualityDecision.Retry, result.Decision);
            Assert.AreEqual(ConversationQualityIssueKind.MetaLeak, result.IssueList[0].Kind);
        }

        [Test]
        public void QualityEvaluator_SceneForbiddenPhrase_ReturnsRetry()
        {
            ConversationQualityContract contract = CreateQualityContract();
            contract.SceneForbiddenPhraseList.Add("15은화에 드리겠습니다");

            ConversationQualityResult result = new ConversationQualityEvaluator().Evaluate(
                CreateQualityOpportunity("merchant"),
                "좋습니다. 15은화에 드리겠습니다.",
                contract);

            Assert.AreEqual(ConversationQualityDecision.Retry, result.Decision);
            Assert.AreEqual(
                ConversationQualityIssueKind.SceneConstraintViolation,
                result.IssueList[0].Kind);
        }

        [Test]
        public void QualityEvaluator_RepeatedSameSpeakerUtterance_ReturnsRetry()
        {
            var opportunity = CreateQualityOpportunity("customer");
            opportunity.RecentEventList.Add(new ConversationEvent
            {
                Kind = ConversationEventKind.Utterance,
                ActorId = "customer",
                Content = "18은화가 전부입니다. 조금 깎아 주세요."
            });

            ConversationQualityResult result = new ConversationQualityEvaluator().Evaluate(
                opportunity,
                "18은화가 전부입니다. 조금 깎아 주세요.",
                CreateQualityContract());

            Assert.AreEqual(ConversationQualityDecision.Retry, result.Decision);
            Assert.AreEqual(ConversationQualityIssueKind.Repetition, result.IssueList[0].Kind);
        }

        [Test]
        public void QualityEvaluator_ValidCustomerUtterance_ReturnsAccept()
        {
            ConversationQualityResult result = new ConversationQualityEvaluator().Evaluate(
                CreateQualityOpportunity("customer"),
                "18은화가 전부입니다. 부두 상자를 길드까지 옮기겠습니다.",
                CreateQualityContract());

            Assert.AreEqual(ConversationQualityDecision.Accept, result.Decision);
            Assert.IsEmpty(result.IssueList);
        }

        private static ConversationRuntime CreateRuntime()
        {
            long now = 1000;
            return new ConversationRuntime(() => now++);
        }

        private static List<ConversationParticipant> CreateParticipants(int count)
        {
            var participantList = new List<ConversationParticipant>();
            for (int i = 0; i < count; i++)
            {
                participantList.Add(new ConversationParticipant
                {
                    ParticipantId = $"participant-{i}",
                    DisplayName = $"Participant {i}",
                    Provider = i == 0 ? "human" : "test-provider",
                    Role = $"role-{i}",
                    Kind = i == 0
                        ? ConversationParticipantKind.Human
                        : ConversationParticipantKind.Llm
                });
            }
            return participantList;
        }

        private static ConversationSceneContract CreateScene(
            IReadOnlyList<ConversationParticipant> participantList)
        {
            var scene = new ConversationSceneContract
            {
                Objective = "Reach a bounded agreement.",
                PublicContext = "public"
            };
            for (int i = 0; i < participantList.Count; i++)
            {
                scene.PrivateContextList.Add(new ParticipantPrivateContext
                {
                    ParticipantId = participantList[i].ParticipantId,
                    Context = $"private-{i}"
                });
            }
            scene.StopConditionList.Add("agreement");
            return scene;
        }

        private static ConversationAction Pass(
            string sessionId,
            string participantId,
            string actionId)
        {
            return new ConversationAction
            {
                ActionId = actionId,
                SessionId = sessionId,
                ParticipantId = participantId,
                Kind = ConversationActionKind.Pass
            };
        }

        private static ConversationQualityContract CreateQualityContract()
        {
            var contract = new ConversationQualityContract();
            contract.ParticipantRuleList.Add(new ConversationParticipantQualityRule
            {
                ParticipantId = "customer",
                ForbiddenPhraseList = new List<string> { "내가 시키는" }
            });
            return contract;
        }

        private static ConversationTurnOpportunity CreateQualityOpportunity(string participantId)
        {
            return new ConversationTurnOpportunity
            {
                ParticipantId = participantId
            };
        }
    }
}
