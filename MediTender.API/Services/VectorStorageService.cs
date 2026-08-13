using System;
using System.Collections.Generic;
using System.Linq; 
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Grpc.Core;

namespace MediTender.API.Services
{
    public class VectorStorageService : IVectorStorageService 
    {
        private readonly QdrantClient _qdrantClient;
        private readonly IGeminiService _geminiService; 
        private readonly ILogger<VectorStorageService> _logger;
        
        private readonly string _collectionName = "meditender_collection_v2";

        public VectorStorageService(QdrantClient qdrantClient, IGeminiService geminiService, ILogger<VectorStorageService> logger)
        {
            _qdrantClient = qdrantClient;
            _geminiService = geminiService;
            _logger = logger;
        }

        public async Task EnsureCollectionExistsAsync()
        {
            try
            {
                await _qdrantClient.GetCollectionInfoAsync(_collectionName);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogInformation("Collection {CollectionName} not found. Creating a new one...", _collectionName);
                
                await _qdrantClient.CreateCollectionAsync(
                    collectionName: _collectionName,
                    vectorsConfig: new VectorParams { Size = 768, Distance = Distance.Cosine } 
                );
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "CRITICAL: Failed to connect to Qdrant or verify collection {CollectionName}.", _collectionName);
                throw; 
            }
        }

        public async Task SaveChunksToQdrantAsync(string fileName, string documentType, string vendorName, List<string> chunks, int tenderId)
        {
            var allEmbeddings = await _geminiService.GetEmbeddingsBatchAsync(chunks);
            var points = new List<PointStruct>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                if (i >= allEmbeddings.Count) break; 
                var embedding = allEmbeddings[i]; 
                
                var payload = new Dictionary<string, Value>
                {
                    { "fileName", new Value { StringValue = fileName } },
                    { "text", new Value { StringValue = chunk } },
                    { "documentType", new Value { StringValue = documentType } },
                    { "vendorName", new Value { StringValue = string.IsNullOrWhiteSpace(vendorName) ? "None" : vendorName } },
                    { "tenderId", new Value { StringValue = tenderId.ToString() } } 
                };

                points.Add(new PointStruct
                {
                    Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                    Vectors = embedding,
                    Payload = { payload }
                });
            }

            int upsertBatchSize = 100;
            for (int i = 0; i < points.Count; i += upsertBatchSize)
            {
                var pointBatch = points.Skip(i).Take(upsertBatchSize).ToList();
                await _qdrantClient.UpsertAsync(_collectionName, pointBatch);
            }
        }

        public async Task DeleteExistingDocumentAsync(int tenderId, string documentType, string vendorName)
        {
            var filter = new Filter();
            filter.Must.Add(new Condition { Field = new FieldCondition { Key = "tenderId", Match = new Match { Keyword = tenderId.ToString() } } });
            filter.Must.Add(new Condition { Field = new FieldCondition { Key = "documentType", Match = new Match { Keyword = documentType } } });
            
            if (!string.IsNullOrWhiteSpace(vendorName))
            {
                filter.Must.Add(new Condition { Field = new FieldCondition { Key = "vendorName", Match = new Match { Keyword = vendorName } } });
            }

            try
            {
                await _qdrantClient.DeleteAsync(_collectionName, filter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CRITICAL: Failed to delete existing document chunks for TenderId: {TenderId}, DocumentType: {DocumentType}. Aborting operation to prevent RAG data duplication and context mixing.", tenderId, documentType);
                throw; 
            }
        }
    }
}