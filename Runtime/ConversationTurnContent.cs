using System;

namespace lLCroweTool.LLMConversation
{
    /// <summary>Defines how much structured turn content a consumer may reveal.</summary>
    public enum ConversationContentVisibility
    {
        /// <summary>Exposes spoken text and presentation hints only.</summary>
        Player,
        /// <summary>Also exposes the participant's inner monologue.</summary>
        MindReading,
        /// <summary>Exposes all structured content and evaluation details.</summary>
        Debug
    }

    /// <summary>Provides optional consumer-defined presentation keys for a turn.</summary>
    [Serializable]
    public sealed class ConversationPresentationHint
    {
        /// <summary>Gets or sets the text-style lookup key.</summary>
        public string TextStyleKey;
        /// <summary>Gets or sets the sound-effect cue lookup key.</summary>
        public string SfxCueKey;
        /// <summary>Gets or sets the character-expression lookup key.</summary>
        public string ExpressionKey;
    }

    /// <summary>Contains provider output before visibility projection.</summary>
    [Serializable]
    public sealed class ConversationTurnContent
    {
        /// <summary>Gets or sets text that may be shown to the player.</summary>
        public string SpokenText;
        /// <summary>Gets or sets private reasoning or character-thought text.</summary>
        public string InnerMonologueText;
        /// <summary>Gets or sets a non-authoritative proposal for a game-owned adapter.</summary>
        public string ActionProposal;
        /// <summary>Gets presentation keys associated with the turn.</summary>
        public ConversationPresentationHint Presentation = new ConversationPresentationHint();
        /// <summary>Gets or sets the optional quality evaluation.</summary>
        public ConversationQualityResult QualityResult;
        /// <summary>Gets or sets the number of provider retries that produced this content.</summary>
        public int RetryCount;
    }

    /// <summary>Contains the subset of structured turn content allowed for one consumer.</summary>
    [Serializable]
    public sealed class ConversationTurnContentView
    {
        /// <summary>Gets or sets the projected spoken text.</summary>
        public string SpokenText;
        /// <summary>Gets or sets the projected inner monologue.</summary>
        public string InnerMonologueText;
        /// <summary>Gets or sets the projected non-authoritative action proposal.</summary>
        public string ActionProposal;
        /// <summary>Gets or sets the projected presentation hints.</summary>
        public ConversationPresentationHint Presentation;
        /// <summary>Gets or sets the projected quality result.</summary>
        public ConversationQualityResult QualityResult;
        /// <summary>Gets or sets the projected retry count.</summary>
        public int RetryCount;
    }

    /// <summary>Projects structured turn content according to an explicit visibility level.</summary>
    public static class ConversationTurnContentProjection
    {
        /// <summary>Creates a detached content view with only the fields allowed by visibility.</summary>
        /// <param name="content">The unprojected provider output.</param>
        /// <param name="visibility">The maximum visibility granted to the consumer.</param>
        /// <returns>A projected view, or <see langword="null"/> when content is null.</returns>
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
