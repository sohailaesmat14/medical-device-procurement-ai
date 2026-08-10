namespace MediTender.API.Services
{
    public interface IGeminiService
    {
        Task<string> GenerateChatResponseAsync(string prompt,bool jsonMode = false, CancellationToken cancellationToken = default);
        Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
        Task<List<float[]>> GetEmbeddingsBatchAsync(List<string> texts, CancellationToken cancellationToken = default);
    }
}