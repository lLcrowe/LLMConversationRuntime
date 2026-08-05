using System;

namespace lLCroweTool.LLMConversation
{
    public enum ConversationContentVisibility
    {
        Player,
        MindReading,
        Debug
    }

    [Serializable]
    public sealed class ConversationPresentationHint
    {
        public string TextStyleKey;
        public string SfxCueKey;
        public string ExpressionKey;
    }

    [Serializable]
    public sealed class ConversationTurnContent
    {
        public string SpokenText;
        public string InnerMonologueText;
        public string ActionProposal;
        public ConversationPresentationHint Presentation = new ConversationPresentationHint();
        public ConversationQualityResult QualityResult;
        public int RetryCount;
    }

    [Serializable]
    public sealed class ConversationTurnContentView
    {
        public string SpokenText;
        public string InnerMonologueText;
        public string ActionProposal;
        public ConversationPresentationHint Presentation;
        public ConversationQualityResult QualityResult;
        public int RetryCount;
    }

    public static class ConversationTurnContentProjection
    {
        public static ConversationTurnContentView Project(
            ConversationTurnContent content,
            ConversationContentVisibility visibility)
        {
            if (content == null) return null;
            var view = new ConversationTurnContentView
            {
                SpokenText = content.SpokenText,
                Presentation = content.Presentation == null ? null : new ConversationPresentationHint
                {
                    TextStyleKey = content.Presentation.TextStyleKey,
                    SfxCueKey = content.Presentation.SfxCueKey,
                    ExpressionKey = content.Presentation.ExpressionKey
                }
            };
            if (visibility == ConversationContentVisibility.Player) return view;

            view.InnerMonologueText = content.InnerMonologueText;
            if (visibility == ConversationContentVisibility.MindReading) return view;

            view.ActionProposal = content.ActionProposal;
            view.QualityResult = content.QualityResult;
            view.RetryCount = content.RetryCount;
            return view;
        }
    }
}
