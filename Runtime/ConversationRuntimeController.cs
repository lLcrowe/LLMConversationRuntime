using UnityEngine;

namespace lLCroweTool.LLMConversation
{
    public sealed class ConversationRuntimeController : MonoBehaviour
    {
        public ConversationRuntime Runtime { get; private set; }

        private void Awake()
        {
            EnsureRuntime();
        }

        public ConversationRuntime EnsureRuntime()
        {
            if (Runtime == null)
                Runtime = new ConversationRuntime();
            return Runtime;
        }
    }
}

