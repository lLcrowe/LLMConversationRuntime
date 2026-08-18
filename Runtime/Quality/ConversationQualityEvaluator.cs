using System;
using System.Collections.Generic;

namespace lLCroweTool.LLMConversation
{
    /// <summary>Defines the consumer action recommended by a quality evaluation.</summary>
    public enum ConversationQualityDecision
    {
        /// <summary>The content passed every configured check.</summary>
        Accept,
        /// <summary>The content has recoverable issues and may be regenerated.</summary>
        Retry,
        /// <summary>The content cannot be accepted as submitted.</summary>
        Reject
    }

    /// <summary>Identifies a category of detected conversation-quality issue.</summary>
    public enum ConversationQualityIssueKind
    {
        /// <summary>The participant violated its configured role constraints.</summary>
        RoleDrift,
        /// <summary>The content violated a scene-wide constraint.</summary>
        SceneConstraintViolation,
        /// <summary>The content exposed prompt or system metadata.</summary>
        MetaLeak,
        /// <summary>The participant repeated a recent utterance above the threshold.</summary>
        Repetition
    }

    /// <summary>Defines forbidden phrases for one participant.</summary>
    [Serializable]
    public sealed class ConversationParticipantQualityRule
    {
        /// <summary>Gets or sets the participant identifier governed by this rule.</summary>
        public string ParticipantId;
        /// <summary>Gets the phrases that indicate role drift for this participant.</summary>
        public List<string> ForbiddenPhraseList = new List<string>();
    }

    /// <summary>Defines deterministic checks applied to generated conversation content.</summary>
    [Serializable]
    public sealed class ConversationQualityContract
    {
        /// <summary>Gets the participant-specific quality rules.</summary>
        public List<ConversationParticipantQualityRule> ParticipantRuleList =
            new List<ConversationParticipantQualityRule>();
        /// <summary>Gets the phrases forbidden by the current scene.</summary>
        public List<string> SceneForbiddenPhraseList = new List<string>();
        /// <summary>Gets the phrases treated as provider or prompt metadata leakage.</summary>
        public List<string> MetaLeakPhraseList = new List<string>
        {
            "[system]",
            "</system>",
            "시스템 프롬프트",
            "프롬프트 지시"
        };
        /// <summary>Gets or sets the Jaccard token-similarity threshold from zero to one.</summary>
        public float RepetitionSimilarityThreshold = 0.8f;

        /// <summary>Validates the configurable similarity threshold.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The threshold is outside zero through one.</exception>
        public void Validate()
        {
            if (RepetitionSimilarityThreshold < 0f ||
                RepetitionSimilarityThreshold > 1f)
                throw new ArgumentOutOfRangeException(
                    nameof(RepetitionSimilarityThreshold));
        }
    }

    /// <summary>Describes one issue found in generated conversation content.</summary>
    [Serializable]
    public sealed class ConversationQualityIssue
    {
        /// <summary>Gets or sets the issue category.</summary>
        public ConversationQualityIssueKind Kind;
        /// <summary>Gets or sets the stable machine-readable issue code.</summary>
        public string Code;
        /// <summary>Gets or sets the human-readable issue message.</summary>
        public string Message;
        /// <summary>Gets or sets the phrase that caused the issue, when applicable.</summary>
        public string MatchedPhrase;
    }

    /// <summary>Reports the recommended decision and all detected quality issues.</summary>
    [Serializable]
    public sealed class ConversationQualityResult
    {
        /// <summary>Gets or sets the recommended consumer decision.</summary>
        public ConversationQualityDecision Decision;
        /// <summary>Gets the detected issues.</summary>
        public List<ConversationQualityIssue> IssueList =
            new List<ConversationQualityIssue>();
    }

    /// <summary>Evaluates generated content with deterministic provider-neutral checks.</summary>
    public sealed class ConversationQualityEvaluator
    {
        /// <summary>Evaluates one proposed utterance in the context of its turn.</summary>
        /// <param name="opportunity">The turn context for the current speaker.</param>
        /// <param name="content">The proposed spoken content.</param>
        /// <param name="contract">The quality rules to apply.</param>
        /// <returns>A quality result that does not mutate the conversation runtime.</returns>
        /// <exception cref="ArgumentNullException">The opportunity or contract is null.</exception>
        public ConversationQualityResult Evaluate(
            ConversationTurnOpportunity opportunity,
            string content,
            ConversationQualityContract contract)
        {
            if (opportunity == null)
                throw new ArgumentNullException(nameof(opportunity));
            if (contract == null)
                throw new ArgumentNullException(nameof(contract));

            contract.Validate();
            var result = new ConversationQualityResult
            {
                Decision = ConversationQualityDecision.Accept
            };
            string trimmedContent = content?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedContent))
            {
                AddIssue(result, ConversationQualityIssueKind.SceneConstraintViolation,
                    "blank_content", "A quality check requires non-empty content.", null);
                result.Decision = ConversationQualityDecision.Reject;
                return result;
            }

            CheckForbiddenPhrases(
                result,
                FindParticipantRule(contract, opportunity.ParticipantId)?.ForbiddenPhraseList,
                trimmedContent,
                ConversationQualityIssueKind.RoleDrift,
                "role_drift");
            CheckForbiddenPhrases(
                result,
                contract.SceneForbiddenPhraseList,
                trimmedContent,
                ConversationQualityIssueKind.SceneConstraintViolation,
                "scene_constraint_violation");
            CheckForbiddenPhrases(
                result,
                contract.MetaLeakPhraseList,
                trimmedContent,
                ConversationQualityIssueKind.MetaLeak,
                "meta_leak");
            CheckSameSpeakerRepetition(result, opportunity, trimmedContent, contract);

            if (result.IssueList.Count > 0)
                result.Decision = ConversationQualityDecision.Retry;
            return result;
        }

        private static void CheckForbiddenPhrases(
            ConversationQualityResult result,
            List<string> phraseList,
            string content,
            ConversationQualityIssueKind kind,
            string code)
        {
            if (phraseList == null) return;
            for (int i = 0; i < phraseList.Count; i++)
            {
                string phrase = phraseList[i];
                if (string.IsNullOrWhiteSpace(phrase) ||
                    content.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                AddIssue(result, kind, code, $"Forbidden phrase: {phrase}", phrase);
            }
        }

        private static void CheckSameSpeakerRepetition(
            ConversationQualityResult result,
            ConversationTurnOpportunity opportunity,
            string content,
            ConversationQualityContract contract)
        {
            for (int i = opportunity.RecentEventList.Count - 1; i >= 0; i--)
            {
                ConversationEvent prior = opportunity.RecentEventList[i];
                if (prior == null || prior.Kind != ConversationEventKind.Utterance ||
                    !string.Equals(prior.ActorId, opportunity.ParticipantId,
                        StringComparison.Ordinal))
                    continue;

                float similarity = CalculateTokenSimilarity(content, prior.Content);
                if (similarity >= contract.RepetitionSimilarityThreshold)
                {
                    AddIssue(result, ConversationQualityIssueKind.Repetition,
                        "repetition", "The current speaker repeated a recent utterance.", null);
                }
                return;
            }
        }

        private static float CalculateTokenSimilarity(string first, string second)
        {
            var firstTokenSet = new HashSet<string>(Tokenize(first), StringComparer.OrdinalIgnoreCase);
            var secondTokenSet = new HashSet<string>(Tokenize(second), StringComparer.OrdinalIgnoreCase);
            if (firstTokenSet.Count == 0 || secondTokenSet.Count == 0) return 0f;

            int intersectionCount = 0;
            foreach (string token in firstTokenSet)
            {
                if (secondTokenSet.Contains(token)) intersectionCount++;
            }

            int unionCount = firstTokenSet.Count + secondTokenSet.Count - intersectionCount;
            return unionCount == 0 ? 0f : (float)intersectionCount / unionCount;
        }

        private static string[] Tokenize(string content)
        {
            return (content ?? string.Empty).Split(
                new[] { ' ', '\t', '\r', '\n', '.', ',', '!', '?', ':', ';', '"', '\'' },
                StringSplitOptions.RemoveEmptyEntries);
        }

        private static ConversationParticipantQualityRule FindParticipantRule(
            ConversationQualityContract contract,
            string participantId)
        {
            for (int i = 0; i < contract.ParticipantRuleList.Count; i++)
            {
                ConversationParticipantQualityRule rule = contract.ParticipantRuleList[i];
                if (rule != null && string.Equals(rule.ParticipantId, participantId,
                    StringComparison.Ordinal))
                    return rule;
            }

            return null;
        }

        private static void AddIssue(
            ConversationQualityResult result,
            ConversationQualityIssueKind kind,
            string code,
            string message,
            string matchedPhrase)
        {
            result.IssueList.Add(new ConversationQualityIssue
            {
                Kind = kind,
                Code = code,
                Message = message,
                MatchedPhrase = matchedPhrase
            });
        }
    }
}
