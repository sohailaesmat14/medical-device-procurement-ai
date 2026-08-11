using System.Text;
using System.Text.Json;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using MediTender.API.Models;
using MediTender.API.Data;
using Microsoft.EntityFrameworkCore;

namespace MediTender.API.Services
{
    public class StandardExtractionService : IStandardExtractionService
    {
        private readonly QdrantClient _qdrantClient;
        private readonly IGeminiService _geminiService;  
        private readonly ApplicationDbContext _dbContext; 
        private readonly string _collectionName = "meditender_collection_v2";

        public StandardExtractionService(QdrantClient qdrantClient, IGeminiService geminiService, ApplicationDbContext dbContext)
        {
            _qdrantClient = qdrantClient;
            _geminiService = geminiService;
            _dbContext = dbContext;
        }

        public async Task<List<Standard>> ExtractRequirementsAsync(string fileName, int tenderId, CancellationToken cancellationToken = default)
        {
            var searchVector = await _geminiService.GetEmbeddingAsync("mandatory technical specifications, requirements, physical characteristics, performance parameters", cancellationToken);

            var filter = new Filter();
            filter.Must.Add(new Condition { Field = new FieldCondition { Key = "fileName", Match = new Match { Keyword = fileName } } });
            filter.Must.Add(new Condition { Field = new FieldCondition { Key = "tenderId", Match = new Match { Keyword = tenderId.ToString() } } });

            var searchResults = await _qdrantClient.SearchAsync(
                collectionName: _collectionName,
                vector: searchVector,
                filter: filter,
                limit: 100,
                cancellationToken: cancellationToken);

            var contextBuilder = new StringBuilder();
            foreach (var result in searchResults)
            {
                if (result.Payload.TryGetValue("text", out var textValue))
                    contextBuilder.AppendLine(textValue.StringValue);
            }

            var context = contextBuilder.ToString();
            if (string.IsNullOrWhiteSpace(context))
                throw new Exception("No context found in the database for this file.");

           var prompt = $@"
            You are a Biomedical Procurement Expert. Extract the technical specifications from the following text.
            For each requirement, extract a short category or item name, a brief description, and the exact specification.
            
            Determine if it is strictly mandatory (must-have) or optional/preferred based on clinical and technical necessity.
            Set 'IsMandatory' to true if the context implies it is an essential requirement, a core clinical function, or uses strong enforcing language (including synonyms like 'essential', 'crucial', 'compulsory', or Arabic equivalents like 'يجب', 'أساسي', 'إلزامي'). 
            Set 'IsMandatory' to false ONLY if the text explicitly describes it as 'optional', 'preferred', 'added advantage', or 'nice-to-have'.

            Return ONLY a valid JSON array of objects. Each object must exactly match the C# model properties and have the following keys:
            - ""ItemName"": string
            - ""Description"": string
            - ""RequirementText"": string
            - ""IsMandatory"": boolean
            
            Do not include any markdown formatting or json code blocks.

            Context:
            {context}
            ";
            
            var aiResponse = await _geminiService.GenerateChatResponseAsync(prompt, false, cancellationToken);
            var cleanedJson = aiResponse.Replace("```json", "").Replace("```", "").Trim();
            
            try
            {
                var extractedDtos = JsonSerializer.Deserialize<List<StandardExtractionDto>>(cleanedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                var requirements = new List<Standard>();

                if (extractedDtos != null && extractedDtos.Any())
                {
                    var oldStandards = await _dbContext.Standards.Where(s => s.TenderId == tenderId).ToListAsync(cancellationToken);
                    _dbContext.Standards.RemoveRange(oldStandards);

                    foreach (var dto in extractedDtos)
                    {
                        var req = new Standard
                        {
                            TenderId = tenderId,
                            ItemName = dto.ItemName,
                            Description = dto.Description,
                            RequirementText = dto.RequirementText,
                            IsMandatory = dto.IsMandatory
                        };
                        
                        _dbContext.Standards.Add(req);
                        requirements.Add(req); 
                    }
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                return requirements;
            }
            catch (Exception ex) 
            {
                throw new Exception("AI returned invalid JSON format. Please check the inner exception for details.", ex);
            }
        }
    }

    public class StandardExtractionDto
    {
        public string ItemName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RequirementText { get; set; } = string.Empty;
        public bool IsMandatory { get; set; }
    }
}