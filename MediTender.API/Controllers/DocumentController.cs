using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediTender.API.Services;
using MediTender.API.Data;
using MediTender.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace MediTender.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly IPdfParsingService _pdfParsingService;
        private readonly ITextChunkingService _textChunkingService;
        private readonly IVectorStorageService _vectorStorageService;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<DocumentController> _logger;

        public DocumentController(
            IPdfParsingService pdfParsingService, 
            ITextChunkingService textChunkingService, 
            IVectorStorageService vectorStorageService,
            ApplicationDbContext dbContext,
            ILogger<DocumentController> logger)
        {
            _pdfParsingService = pdfParsingService;
            _textChunkingService = textChunkingService;
            _vectorStorageService = vectorStorageService;
            _dbContext = dbContext;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdStr = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                return userId;
            }
            return 0;
        }

        [HttpGet("evaluations/{tenderId}")]
        public async Task<IActionResult> GetEvaluations(int tenderId)
        {
            int userId = GetCurrentUserId();
            var tender = await _dbContext.Tenders.FirstOrDefaultAsync(t => t.Id == tenderId && t.UserId == userId);
            if (tender == null)
                return Unauthorized(new { Message = "Access denied to this tender." });

            var evaluations = await _dbContext.OfferEvaluations
                .Include(e => e.Details)
                .Where(e => e.TenderId == tenderId)
                .ToListAsync();

            return Ok(evaluations);
        }

        [HttpPost("upload-pdf")]
        public async Task<IActionResult> UploadPdfAsync([FromForm] FileUploadRequest request)
        {
            request.VendorName = request.VendorName?.Trim().ToLowerInvariant() ?? string.Empty;
            
            if (request.File == null || request.File.Length == 0)
                return BadRequest("Invalid file.");

            if (string.IsNullOrWhiteSpace(request.DocumentType))
                return BadRequest("Document type is required.");

            if (request.DocumentType.Contains("Offer") && string.IsNullOrWhiteSpace(request.VendorName))
                return BadRequest("Vendor name is required for offers.");

            int userId = GetCurrentUserId();
            var tender = await _dbContext.Tenders.FirstOrDefaultAsync(t => t.Id == request.TenderId && t.UserId == userId);
            if (tender == null)
                return Unauthorized(new { Message = "Access denied to this tender." });

            using var stream = request.File.OpenReadStream();
            byte[] header = new byte[4];
            var bytesRead = await stream.ReadAsync(header, 0, 4);
            
            if (bytesRead < 4 || header[0] != 0x25 || header[1] != 0x50 || header[2] != 0x44 || header[3] != 0x46)
            {
                return BadRequest("Invalid file format. Only genuine PDF files are permitted.");
            }
            stream.Position = 0; 

            try
            {
                await _vectorStorageService.DeleteExistingDocumentAsync(request.TenderId, request.DocumentType, request.VendorName);

                var extractedText = await Task.Run(() => _pdfParsingService.ExtractTextFromPdf(stream));

                if (string.IsNullOrWhiteSpace(extractedText) || extractedText.Trim().Length < 50)
                {
                    return BadRequest("Error: The PDF contains no readable text. It appears to be a scanned image. Please use OCR software to make it text-searchable before uploading.");
                }

                var chunks = _textChunkingService.ChunkText(extractedText);

                if (chunks == null || chunks.Count == 0)
                {
                    return BadRequest("Error: The document text could not be processed into chunks. Please check the file format.");
                }
                
                await _vectorStorageService.SaveChunksToQdrantAsync(request.File.FileName, request.DocumentType, request.VendorName, chunks, request.TenderId);

                return Ok(new { 
                    Message = "Success", 
                    DocumentType = request.DocumentType,
                    Vendor = request.VendorName,
                    ChunksCount = chunks.Count 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading PDF for DocumentType: {DocumentType}, Vendor: {VendorName}", request.DocumentType, request.VendorName);
                return StatusCode(500, new { Message = "An internal server error occurred while processing your request. Please try again later." });
            }
        }

        [HttpPost("ask")]
        public async Task<IActionResult> AskQuestion([FromBody] QuestionRequest request, [FromServices] IRagService ragService)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest("Question is required.");

            int userId = GetCurrentUserId();
            var tender = await _dbContext.Tenders.FirstOrDefaultAsync(t => t.Id == request.TenderId && t.UserId == userId);
            if (tender == null)
                return Unauthorized(new { Message = "Access denied to this tender." });

            try
            {
                var answer = await ragService.AnalyzeOfferAsync(request.Question, request.TenderId, request.VendorName);
                return Ok(new { Answer = answer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Q&A request for Tender: {TenderId}, Vendor: {VendorName}", request.TenderId, request.VendorName);
                return StatusCode(500, new { Message = "An internal server error occurred while answering the question. Please try again." });
            }
        }

        public class QuestionRequest 
        { 
            public string Question { get; set; } = string.Empty; 
            public int TenderId { get; set; }
            public string VendorName { get; set; } = string.Empty;
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var history = await _dbContext.TenderInteractions
                .OrderByDescending(x => x.CreatedAt)
                .Take(10)
                .ToListAsync();
            
            return Ok(history);
        }

        [HttpPost("compare-vendors")]
        public async Task<IActionResult> CompareVendors([FromBody] MultiComparisonRequest request, [FromServices] IComparisonService comparisonService, CancellationToken cancellationToken)
        {
            if (request.VendorNames == null || !request.VendorNames.Any())
                return BadRequest("Vendor names list cannot be empty.");

            int userId = GetCurrentUserId();
            var tender = await _dbContext.Tenders.FirstOrDefaultAsync(t => t.Id == request.TenderId && t.UserId == userId, cancellationToken);
            if (tender == null)
                return Unauthorized(new { Message = "Access denied to this tender." });

            request.VendorNames = request.VendorNames.Select(v => v.Trim().ToLowerInvariant()).ToList();
            int cost = request.VendorNames.Count * 15;
            
            var quotaResult = await TryConsumeQuotaAsync(cost);
            if (!quotaResult.Success)
            {
                return BadRequest(new { Message = $"❌ Your current balance ({quotaResult.Remaining} points) isn't enough. You need ({cost} points)." });
            }

            try
            {
                var dbRequirements = await _dbContext.Standards
                    .Where(s => s.TenderId == request.TenderId)
                    .ToListAsync(cancellationToken);

                if (!dbRequirements.Any())
                    return BadRequest("No standard requirements found for this tender. Please run the extraction phase first.");

                var results = await comparisonService.CompareVendorsAsync(request.TenderId, dbRequirements, request.VendorNames, cancellationToken);
                return Ok(results);
            }
            catch (OperationCanceledException)
            {
                await RefundQuotaAsync(cost); 
                return StatusCode(499, "Client closed the request.");
            }
            catch (Exception ex)
            {
                await RefundQuotaAsync(cost); 
                _logger.LogError(ex, "Error during multi-vendor comparison for Tender: {TenderId}", request.TenderId);
                return StatusCode(500, new { Message = "An internal server error occurred during vendor comparison. Please review the logs." });
            }
        }

        public class MultiComparisonRequest 
        { 
            public int TenderId { get; set; } = 1; 
           
            public List<string> VendorNames { get; set; } = new();
        }

        [HttpPost("extract-standard")]
        public async Task<IActionResult> ExtractStandardRequirements([FromBody] ExtractStandardRequest request, [FromServices] IStandardExtractionService extractionService)
        {
            if (string.IsNullOrEmpty(request.FileName) || request.TenderId <= 0)
                return BadRequest("Invalid file name or tender ID.");

            int userId = GetCurrentUserId();
            var tender = await _dbContext.Tenders.FirstOrDefaultAsync(t => t.Id == request.TenderId && t.UserId == userId);
            if (tender == null)
                return Unauthorized(new { Message = "Access denied to this tender." });

            int cost = 10;
            var quotaResult = await TryConsumeQuotaAsync(cost);
            if (!quotaResult.Success)
            {
                return BadRequest(new { Message = $"❌ Your current balance ({quotaResult.Remaining} points) isn't enough. You need ({cost} points)." });
            }

            try
            {
                var requirements = await extractionService.ExtractRequirementsAsync(request.FileName, request.TenderId);
                return Ok(requirements);
            }
            catch (Exception ex)
            {
                await RefundQuotaAsync(cost);
                _logger.LogError(ex, "Error extracting standard requirements for Tender: {TenderId}, File: {FileName}", request.TenderId, request.FileName);
                return StatusCode(500, new { Message = "An internal server error occurred while extracting requirements." });
            }
        }
        [Authorize(Roles = "Committee")]
        [HttpDelete("reset-system")]
        public async Task<IActionResult> ResetSystem([FromServices] Qdrant.Client.QdrantClient qdrantClient)
        {
            try
            {
                _dbContext.VendorOffers.RemoveRange(_dbContext.VendorOffers);
                _dbContext.EvaluationDetails.RemoveRange(_dbContext.EvaluationDetails);
                _dbContext.OfferEvaluations.RemoveRange(_dbContext.OfferEvaluations);
                _dbContext.Standards.RemoveRange(_dbContext.Standards); 
                _dbContext.TenderInteractions.RemoveRange(_dbContext.TenderInteractions);
                _dbContext.Tenders.RemoveRange(_dbContext.Tenders);

                await _dbContext.SaveChangesAsync();

                if (_dbContext.Database.IsSqlServer())
                {
                    await _dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Tenders', RESEED, 0)");
                    await _dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Standards', RESEED, 0)"); 
                    await _dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('VendorOffers', RESEED, 0)");
                    await _dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('OfferEvaluations', RESEED, 0)");
                    await _dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('EvaluationDetails', RESEED, 0)"); 
                    await _dbContext.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('TenderInteractions', RESEED, 0)"); 
                }
                else if (_dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL") 
                {
                    await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Tenders\", \"Standards\", \"VendorOffers\", \"OfferEvaluations\" , \"EvaluationDetails\", \"TenderInteractions\" RESTART IDENTITY CASCADE");
                }
                else if (_dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite") 
                {
                    await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('Tenders', 'Standards', 'VendorOffers', 'OfferEvaluations', 'EvaluationDetails', 'TenderInteractions')");                
                }

                await qdrantClient.DeleteCollectionAsync("meditender_collection_v2");
                await qdrantClient.CreateCollectionAsync("meditender_collection_v2", 
                    new Qdrant.Client.Grpc.VectorParams { Size = 3072, Distance = Qdrant.Client.Grpc.Distance.Cosine });

                await qdrantClient.CreatePayloadIndexAsync("meditender_collection_v2", "fileName", Qdrant.Client.Grpc.PayloadSchemaType.Keyword);
                await qdrantClient.CreatePayloadIndexAsync("meditender_collection_v2", "documentType", Qdrant.Client.Grpc.PayloadSchemaType.Keyword);
                await qdrantClient.CreatePayloadIndexAsync("meditender_collection_v2", "vendorName", Qdrant.Client.Grpc.PayloadSchemaType.Keyword);
                await qdrantClient.CreatePayloadIndexAsync("meditender_collection_v2", "tenderId", Qdrant.Client.Grpc.PayloadSchemaType.Keyword);

                return Ok(new { Message = "System has been completely reset and is ready for a new demo!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during system reset operation.");
                return StatusCode(500, new { Message = "An internal server error occurred while resetting the system." });
            }
        }
        public class FileUploadRequest
        {
            public IFormFile? File { get; set; }
            public string DocumentType { get; set; } = string.Empty;
            public string VendorName { get; set; } = string.Empty;
            public int TenderId { get; set; } = 1;
        }
        
        private async Task<(bool Success, int Remaining)> TryConsumeQuotaAsync(int cost)
        {
            if (User.IsInRole("Committee"))
                return (true, 9999);

            var userIdStr = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId))
                return (false, 0);

            using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null || user.QuotaPoints < cost)
                {
                    await transaction.RollbackAsync();
                    return (false, user?.QuotaPoints ?? 0);
                }

                user.QuotaPoints -= cost;
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, user.QuotaPoints);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        private async Task RefundQuotaAsync(int amount)
        {
            if (User.IsInRole("Committee")) return;

            var userIdStr = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out int userId)) return;

            using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var user = await _dbContext.Users.FindAsync(userId);
                if (user != null)
                {
                    user.QuotaPoints += amount;
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [HttpPost("check-quota")]
        public async Task<IActionResult> CheckQuota([FromBody] QuotaRequest request)
        {
            int cost = request.VendorCount * 15;
            int userId = GetCurrentUserId();
            
            if (userId == 0)
                return Ok(new { Success = true, RemainingQuota = 9999 });

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return Unauthorized();

            if (user.QuotaPoints >= cost)
                return Ok(new { Success = true, RemainingQuota = user.QuotaPoints });
                
            return BadRequest(new { Success = false, Message = $"❌ Your current balance ({user.QuotaPoints} points) isn't enough. You need ({cost} points)." });
        }

        public class QuotaRequest
        {
            public int VendorCount { get; set; }
        }

        [HttpPost("override-evaluation")]
        public async Task<IActionResult> OverrideEvaluation([FromBody] OverrideRequest request)
        {
            int userId = GetCurrentUserId();
            var tender = await _dbContext.Tenders.FirstOrDefaultAsync(t => t.Id == request.TenderId && t.UserId == userId);
            if (tender == null)
                return Unauthorized(new { Message = "Access denied to this tender." });

            try
            {
                var evaluation = await _dbContext.OfferEvaluations
                    .Include(e => e.Details)
                    .FirstOrDefaultAsync(e => e.TenderId == request.TenderId && e.VendorName == request.VendorName);

                if (evaluation == null) 
                    return NotFound("Evaluation not found in database.");

                var detail = evaluation.Details.FirstOrDefault(d => d.Requirement == request.Requirement);
                if (detail == null) 
                    return NotFound("Requirement not found in this evaluation.");

                detail.Status = "Met";
                detail.Evidence = "✅ Manually verified by committee.";
                detail.Score = detail.IsMandatory ? 20 : 10;

                evaluation.TotalScore = evaluation.Details.Sum(d => d.Score);

                bool hasFailedMandatory = evaluation.Details.Any(d => d.IsMandatory && (d.Status == "Not Met" || d.Status == "Error"));
                bool hasPartialOrMissingMandatory = evaluation.Details.Any(d => d.IsMandatory && (d.Status == "Partially Met" || d.Status == "Not Mentioned"));

                if (hasFailedMandatory)
                    evaluation.FinalDecision = "Recommended for Rejection";
                else if (hasPartialOrMissingMandatory)
                    evaluation.FinalDecision = "Pending Manual Review";
                else
                    evaluation.FinalDecision = "Recommended for Acceptance";

                var vendorOffer = await _dbContext.VendorOffers.FirstOrDefaultAsync(v => v.TenderId == request.TenderId && v.CompanyName == request.VendorName);
                if (vendorOffer != null)
                {
                    vendorOffer.IsAccepted = evaluation.FinalDecision == "Recommended for Acceptance" || evaluation.FinalDecision == "Pending Manual Review";
                    vendorOffer.EvaluationScore = evaluation.TotalScore;
                }

                await _dbContext.SaveChangesAsync();

                return Ok(new { Message = "Override saved to database successfully" });
            }
           catch (Exception ex)
            {
                _logger.LogError(ex, "Error overriding evaluation for Tender: {TenderId}, Vendor: {VendorName}", request.TenderId, request.VendorName);
                return StatusCode(500, new { Message = "An internal server error occurred while processing the evaluation override." });
            }
        }

        [HttpPost("init-tender")]
        public async Task<IActionResult> InitTender()
        {
            try
            {
                int userId = GetCurrentUserId();
                if (userId == 0) return Unauthorized(new { Message = "Invalid user token." });

                var tender = new Tender 
                { 
                    Title = $"Tender - {DateTime.UtcNow:yyyy-MM-dd HH:mm}", 
                    Description = "Auto-generated via AI Workflow",
                    UserId = userId
                };
                
                _dbContext.Tenders.Add(tender);
                await _dbContext.SaveChangesAsync(); 
                
                return Ok(new { TenderId = tender.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing a new tender.");
                return StatusCode(500, new { Message = "An internal server error occurred while creating the tender." });
            }
        }
        
        public class OverrideRequest
        {
            public int TenderId { get; set; }
            public string VendorName { get; set; } = string.Empty;
            public string Requirement { get; set; } = string.Empty;
        }

        [HttpPost("override-vendor-decision")]
        public async Task<IActionResult> OverrideVendorDecision([FromBody] VendorOverrideRequest request)
        {
            int userId = GetCurrentUserId();
            var tender = await _dbContext.Tenders.FirstOrDefaultAsync(t => t.Id == request.TenderId && t.UserId == userId);
            if (tender == null)
                return Unauthorized(new { Message = "Access denied to this tender." });

            try
            {
                var evaluation = await _dbContext.OfferEvaluations
                    .Include(e => e.Details)
                    .FirstOrDefaultAsync(e => e.TenderId == request.TenderId && e.VendorName == request.VendorName);

                if (evaluation == null) 
                    return NotFound("Evaluation not found in database.");

                foreach (var detail in evaluation.Details.Where(d => d.IsMandatory && (d.Status == "Partially Met" || d.Status == "Not Mentioned")))
                {
                    detail.Status = "Met";
                    detail.Evidence = "✅ Vendor manually approved by committee.";
                    detail.Score = 20;
                }

                evaluation.TotalScore = evaluation.Details.Sum(d => d.Score);
                evaluation.FinalDecision = "Recommended for Acceptance";

                var vendorOffer = await _dbContext.VendorOffers.FirstOrDefaultAsync(v => v.TenderId == request.TenderId && v.CompanyName == request.VendorName);
                if (vendorOffer != null)
                {
                    vendorOffer.IsAccepted = true;
                    vendorOffer.EvaluationScore = evaluation.TotalScore;
                }

                await _dbContext.SaveChangesAsync();
                return Ok(new { Message = "Vendor completely approved and saved to database." });
            }
           catch (Exception ex)
            {
                _logger.LogError(ex, "Error overriding vendor decision for Tender: {TenderId}, Vendor: {VendorName}", request.TenderId, request.VendorName);
                return StatusCode(500, new { Message = "An internal server error occurred while processing the vendor decision override." });
            }
        }

        public class VendorOverrideRequest
        {
            public int TenderId { get; set; }
            public string VendorName { get; set; } = string.Empty;
        }
        
        public class ExtractStandardRequest
        {
            public string FileName { get; set; } = string.Empty;
            public int TenderId { get; set; }
        }
    }
}