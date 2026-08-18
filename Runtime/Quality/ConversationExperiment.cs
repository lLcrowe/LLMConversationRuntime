using System;
using System.Collections.Generic;
using System.Linq;

namespace lLCroweTool.LLMConversation
{
    /// <summary>Aggregates observed conversation metrics for one model or persona identifier.</summary>
    [Serializable]
    public sealed class ConversationExperimentModelMetric
    {
        /// <summary>Gets or sets the provider-neutral model grouping identifier.</summary>
        public string ModelId;
        /// <summary>Gets or sets the number of accepted utterance events.</summary>
        public int TurnCount;
        /// <summary>Gets or sets the number of rejected quality evaluations.</summary>
        public int RejectedTurnCount;
        /// <summary>Gets or sets the number of repetition issues.</summary>
        public int RepetitionCount;
        /// <summary>Gets or sets the number of role-drift issues.</summary>
        public int RoleDriftCount;
        /// <summary>Gets or sets the number of metadata-leak issues.</summary>
        public int MetaLeakCount;
    }

    /// <summary>Captures a provider-neutral summary of one conversation experiment.</summary>
    [Serializable]
    public sealed class ConversationExperimentRecord
    {
        /// <summary>Gets or sets the stable experiment scenario identifier.</summary>
        public string ScenarioId;
        /// <summary>Gets or sets the human-readable scenario name.</summary>
        public string ScenarioName;
        /// <summary>Gets or sets the evaluated session identifier.</summary>
        public string SessionId;
        /// <summary>Gets or sets the configured session turn limit.</summary>
        public int MaxTurns;
        /// <summary>Gets or sets the session's terminal reason code.</summary>
        public string StopReason;
        /// <summary>Gets the metrics grouped by model or persona identifier.</summary>
        public List<ConversationExperimentModelMetric> ModelMetricList =
            new List<ConversationExperimentModelMetric>();
    }

    /// <summary>Builds comparable model metrics from a conversation snapshot.</summary>
    public static class ConversationExperimentEvaluator
    {
        /// <summary>Creates an experiment record from accepted events and rejected quality results.</summary>
        /// <param name="snapshot">The session snapshot to aggregate.</param>
        /// <param name="scenarioId">The stable scenario identifier.</param>
        /// <param name="scenarioName">The human-readable scenario name.</param>
        /// <param name="rejectedQualityListByParticipantId">Rejected evaluations grouped by participant.</param>
        /// <returns>A provider-neutral experiment record.</returns>
        /// <exception cref="ArgumentNullException">The snapshot is null.</exception>
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
