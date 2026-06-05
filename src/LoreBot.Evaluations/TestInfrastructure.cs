using LoreBot.Core.Abstractions;
using LoreBot.Core.Models;

namespace LoreBot.Evaluations;

public static class TestInfrastructure
{
    public static IChatService BuildDeterministicChatService() => new DeterministicChatService();

    private sealed class DeterministicChatService : IChatService
    {
        public Task<ChatResult> ChatAsync(string universe, string message, string sessionId, CancellationToken ct = default)
        {
            var lower = message.ToLowerInvariant();
            if (universe == "jojo" && lower.Contains("дио"))
            {
                return Task.FromResult(new ChatResult
                {
                    Type = "character",
                    Answer = "Дио Брандо — вампир и ключевой антагонист JoJo [1].",
                    Confidence = 0.9,
                    Sources =
                    [
                        new Source
                        {
                            Title = "Dio Brando",
                            Url = "https://jojo.fandom.com/wiki/Dio_Brando",
                            Category = "character",
                            Similarity = 0.92
                        }
                    ],
                    Cards =
                    [
                        new ChatCard
                        {
                            Type = "character",
                            Title = "Dio Brando",
                            Body = "Vampire antagonist"
                        }
                    ],
                    TokensUsed = 120
                });
            }

            if (universe == "jojo" && lower.Contains("стенд"))
            {
                return Task.FromResult(new ChatResult
                {
                    Type = "answer",
                    Answer = "Стенд — сверхъестественная способность, проявляющая силу пользователя [1].",
                    Confidence = 0.84,
                    Sources =
                    [
                        new Source
                        {
                            Title = "Stand",
                            Url = "https://jojo.fandom.com/wiki/Stand",
                            Category = "ability",
                            Similarity = 0.88
                        }
                    ],
                    TokensUsed = 96
                });
            }

            return Task.FromResult(new ChatResult
            {
                Type = "no_context",
                Answer = "В базе знаний нет информации по этому вопросу.",
                Sources = [],
                TokensUsed = 0
            });
        }
    }
}
