using System;
using System.Collections.Generic;
using System.Linq;

namespace MediTender.API.Services
{
    public class TextChunkingService : ITextChunkingService
    {
        private readonly int _maxTokensPerChunk;
        private readonly int _overlapTokens;

        public TextChunkingService(int maxTokensPerChunk = 500, int overlapTokens = 50)
        {
            _maxTokensPerChunk = maxTokensPerChunk <= 0 ? 500 : maxTokensPerChunk;
            _overlapTokens = overlapTokens >= _maxTokensPerChunk ? _maxTokensPerChunk - 1 : (overlapTokens < 0 ? 0 : overlapTokens);
        }

        public List<string> ChunkText(string text)
        {
            var chunks = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) 
                return chunks;

            var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (words.Length <= _maxTokensPerChunk)
            {
                chunks.Add(text);
                return chunks;
            }

            for (int i = 0; i < words.Length; i += (_maxTokensPerChunk - _overlapTokens))
            {
                var chunkWords = words.Skip(i).Take(_maxTokensPerChunk);
                var chunkText = string.Join(" ", chunkWords);
                
                if (!string.IsNullOrWhiteSpace(chunkText))
                {
                    chunks.Add(chunkText);
                }
                
                if (i + _maxTokensPerChunk >= words.Length)
                    break;
            }

            return chunks;
        }
    }
}