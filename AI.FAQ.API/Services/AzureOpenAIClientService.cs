using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace AI.FAQ.API.Services
{
    public class AzureOpenAIClientService
    {
        private readonly AzureOpenAIClient client;

        public AzureOpenAIClientService(string uri, string key)
        {
            client = new AzureOpenAIClient(new Uri(uri), new AzureKeyCredential(key));
        }

        public ChatClient GetChatClient(string deploymentName)
        {
            return client.GetChatClient(deploymentName);
        }

        public Task<System.ClientModel.ClientResult<ChatCompletion>> GetChatCompletionAsync(
            ChatClient chatClient, List<ChatMessage> chatMessages, CancellationToken cancellationToken = default)
        {
            ChatCompletionOptions options = new ChatCompletionOptions()
            {
                Temperature = 0.1f
            };

            return chatClient.CompleteChatAsync(chatMessages, options, cancellationToken);
        }
    }
}
