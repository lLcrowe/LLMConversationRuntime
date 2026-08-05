using System;
using System.Collections.Generic;

namespace lLCroweTool.LLMConversation
{
    public enum ConversationQualityDecision
    {
        Accept,
        Retry,
        Reject
    }

    public enum ConversationQualityIssueKind
    {
        RoleDrift,
        SceneConstraintViolation,
        MetaLeak,
        Repetition
    }

    [Serializable]
    public sealed class ConversationParticipantQualityRule
    {
        public string ParticipantId;
        public List<string> ForbiddenPhraseList = new List<string>();
    }

    [Serializable]
    public sealed class ConversationQualityContract
    {
        public List<ConversationParticipantQualityRule> ParticipantRuleList =
            new List<ConversationParticipantQualityRule>();
        public List<string> SceneForbiddenPhraseList = new List<string>();
        public List<string> MetaLeakPhraseList = new List<string>
        {
            "[system]",
            "</system>",
            "시스템 프롬프트",
            "프롬프트 지시"
        };
        public float RepetitionSimilarityThreshold = 0.8f;

        public void Validate()
        {
            if (RepetitionSimilarityThreshold < 0f ||
                RepetitionSimilarityThreshold > 1f)
                throw new ArgumentOutOfRangeException(
                    nameof(RepetitionSimilarityThreshold));
        }
    }

    [Serializable]
    public sealed class ConversationQualityIssue
    {
        public ConversationQualityIssueKind Kind;
        public string Code;
        public string Message;
        public string MatchedPhrase;
    }

    [Serializable]
    public sealed class ConversationQualityResult
    {
        public ConversationQualityDecision Decision;
        public List<ConversationQualityIssue> IssueList =
            new List<ConversationQualityIssue>();
    }

    public sealed class ConversationQualityEvaluator
    {
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
