using System.Text;
using System.Text.Json;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using MediTender.API.Models;
using MediTender.API.Data;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;

namespace MediTender.API.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiService> _logger;

        private readonly string _googleApiKey;

        private readonly string _itiBaseUrl;
        private readonly string _itiModelId;
        private readonly string _itiApiKey;

        public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _googleApiKey = configuration["Gemini:ApiKey"]
                ?? throw new ArgumentNullException("Gemini API Key is missing in appsettings.json!");

            _itiBaseUrl = configuration["ClaudeIti:BaseUrl"]
                ?? throw new ArgumentNullException("ClaudeIti:BaseUrl is missing in appsettings.json!");
            _itiModelId = configuration["ClaudeIti:ModelId"]
                ?? throw new ArgumentNullException("ClaudeIti:ModelId is missing in appsettings.json!");

            _itiApiKey = configuration["ClaudeIti:ApiKey"]
                ?? throw new ArgumentNullException("SBG_API_KEY environment variable is not set!");

        }

        public async Task<string> GenerateChatResponseAsync(string prompt, bool jsonMode = false, CancellationToken cancellationToken = default)
        {
            var url = $"{_itiBaseUrl}/student/chat";

            string baseSystemPrompt = @"You are an expert Biomedical Procurement Engineer evaluating technical offers.
            CRITICAL EVALUATION RULES:
            1. Unit Conversion: Always perform implicit mathematical unit conversions before comparing values (e.g., convert minutes to hours, mm to cm, Hz to kHz). Evaluate the actual physical or mathematical value, not just the raw text.
            2. Strict Inequalities: Strictly evaluate minimum and maximum boundaries. If a standard requires 'at least X', an offer stating '> Y' (where Y is less than X) does NOT fulfill the requirement explicitly and must be evaluated critically (e.g., as Partially Met).
            3. Contextual Reasoning: Base your decisions strictly on the provided text combined with basic engineering logic. Do not hallucinate capabilities.";

            var payload = new
            {
                model_id = _itiModelId,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                system_prompt = jsonMode
                    ? baseSystemPrompt + "\n\nYou must respond with ONLY valid JSON. Do not include markdown code fences or any explanatory text before or after the JSON."
                    : baseSystemPrompt
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _itiApiKey);

            cancellationToken.ThrowIfCancellationRequested();

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseString);

            if (doc.RootElement.TryGetProperty("output_text", out var outputText) && outputText.GetString() is string text && !string.IsNullOrEmpty(text))
            {

                if (doc.RootElement.TryGetProperty("usage", out var usage))
                {
                    _logger.LogInformation(
                        "Claude chat call: {InputTokens} input / {OutputTokens} output tokens, actual cost ${ActualCost}",
                        usage.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0,
                        usage.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0,
                        doc.RootElement.TryGetProperty("actual_cost_usd", out var cost) ? cost.GetString() : "n/a");
                }

                return text;
            }

            _logger.LogWarning("ITI Claude proxy returned no output_text. Full response: {Response}", doc.RootElement.GetRawText());
            throw new Exception("ITI Claude proxy request returned an empty or unexpected response.");
        }

        public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:embedContent";
            var payload = new { model = "models/gemini-embedding-001", content = new { parts = new[] { new { text = text } } } };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            cancellationToken.ThrowIfCancellationRequested();

            var response = await PostGeminiAsync(url, content, cancellationToken);

            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseString);
            return doc.RootElement.GetProperty("embedding").GetProperty("values").EnumerateArray().Select(v => v.GetSingle()).ToArray();
        }

        public async Task<List<float[]>> GetEmbeddingsBatchAsync(List<string> texts, CancellationToken cancellationToken = default)
        {
            var allEmbeddings = new List<float[]>();
            int batchSize = 15;

            for (int i = 0; i < texts.Count; i += batchSize)
            {
                var batch = texts.Skip(i).Take(batchSize).ToList();

                var requests = batch.Select(text => new { text = text }).ToList();
                var payload = new
                {
                    requests = requests.Select(r => new { model = "models/gemini-embedding-001", content = new { parts = new[] { new { text = r.text } } } })
                };

                var url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:batchEmbedContents";
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                cancellationToken.ThrowIfCancellationRequested();

                var response = await PostGeminiAsync(url, content, cancellationToken);

                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(responseString);

                var returnedEmbeddings = doc.RootElement.GetProperty("embeddings").EnumerateArray().ToList();

                if (returnedEmbeddings.Count != batch.Count)
                {
                    _logger.LogError("Silent truncation detected: Gemini API returned {ReturnedCount} embeddings for a batch of {RequestedCount} chunks.", returnedEmbeddings.Count, batch.Count);
                    throw new InvalidOperationException($"Silent truncation detected: Expected {batch.Count} embeddings, but received {returnedEmbeddings.Count}.");
                }

                foreach (var embedding in returnedEmbeddings)
                {
                    allEmbeddings.Add(embedding.GetProperty("values").EnumerateArray().Select(v => v.GetSingle()).ToArray());
                }

                if (i + batchSize < texts.Count)
                {
                    await Task.Delay(2000, cancellationToken);
                }
            }

            return allEmbeddings;
        }
        private async Task<HttpResponseMessage> PostGeminiAsync(string url, HttpContent content, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.Add("x-goog-api-key", _googleApiKey);
            return await _httpClient.SendAsync(request, cancellationToken);
        }
    }
}