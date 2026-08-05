using System;
using System.Collections.Generic;
using System.Linq;

namespace lLCroweTool.LLMConversation
{
    [Serializable]
    public sealed class ConversationExperimentModelMetric
    {
        public string ModelId;
        public int TurnCount;
        public int RejectedTurnCount;
        public int RepetitionCount;
        public int RoleDriftCount;
        public int MetaLeakCount;
    }

    [Serializable]
    public sealed class ConversationExperimentRecord
    {
        public string ScenarioId;
        public string ScenarioName;
        public string SessionId;
        public int MaxTurns;
        public string StopReason;
        public List<ConversationExperimentModelMetric> ModelMetricList =
            new List<ConversationExperimentModelMetric>();
    }

    public static class ConversationExperimentEvaluator
    {
        public static ConversationExperimentRecord Create(
            ConversationSnapshot snapshot,
            string scenarioId,
            string scenarioName,
            IReadOnlyDictionary<string, List<ConversationQualityResult>> rejectedQualityListByParticipantId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var record = new ConversationExperimentRecord
            {
                ScenarioId = scenarioId,
                ScenarioName = scenarioName,
                SessionId = snapshot.SessionId,
                MaxTurns = snapshot.Policy.MaxTurns,
                StopReason = snapshot.StopReason
            };
            foreach (IGrouping<string, ConversationParticipant> group in snapshot.ParticipantList
                         .GroupBy(item => string.IsNullOrWhiteSpace(item.PersonaReference)
                             ? item.Provider
                             : item.PersonaReference))
            {
                var metric = new ConversationExperimentModelMetric { ModelId = group.Key };
                foreach (ConversationParticipant participant in group)
                {
                    metric.TurnCount += snapshot.EventList.Count(item =>
                        item.Kind == ConversationEventKind.Utterance &&
                        item.ActorId == participant.ParticipantId);
                    if (rejectedQualityListByParticipantId == null ||
                        !rejectedQualityListByParticipantId.TryGetValue(participant.ParticipantId,
                            out List<ConversationQualityResult> qualityList))
                        continue;

                    metric.RejectedTurnCount += qualityList.Count;
                    foreach (ConversationQualityResult quality in qualityList)
                    {
                        metric.RepetitionCount += quality.IssueList.Count(item =>
                            item.Kind == ConversationQualityIssueKind.Repetition);
                        metric.RoleDriftCount += quality.IssueList.Count(item =>
                            item.Kind == ConversationQualityIssueKind.RoleDrift);
                        metric.MetaLeakCount += quality.IssueList.Count(item =>
                            item.Kind == ConversationQualityIssueKind.MetaLeak);
                    }
                }
                record.ModelMetricList.Add(metric);
            }
            return record;
        }
    }
}
