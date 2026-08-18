using UnityEngine;

namespace lLCroweTool.LLMConversation
{
    /// <summary>Provides a scene-owned lazy instance of <see cref="ConversationRuntime"/>.</summary>
    public sealed class ConversationRuntimeController : MonoBehaviour
    {
        /// <summary>Gets the runtime owned by this component after initialization.</summary>
        public ConversationRuntime Runtime { get; private set; }

        private void Awake()
        {
            EnsureRuntime();
        }

        /// <summary>Returns the existing runtime or creates it when first requested.</summary>
        /// <returns>The runtime owned by this component.</returns>
        public ConversationRuntime EnsureRuntime()
        {
            if (Runtime == null)
                Runtime = new ConversationRuntime();
            return Runtime;
        }
    }
}

