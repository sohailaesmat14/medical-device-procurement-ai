using Microsoft.EntityFrameworkCore;
using MediTender.API.Data;
using MediTender.API.Services;
using Qdrant.Client;
using Polly;
using Polly.Extensions.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("StrictCorsPolicy",
        policy =>
        {
            policy.WithOrigins(allowedOrigins ?? Array.Empty<string>())
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

builder.Services.AddScoped<IPdfParsingService, PdfParsingService>();
builder.Services.AddScoped<ITextChunkingService, TextChunkingService>();
builder.Services.AddScoped<IVectorStorageService, VectorStorageService>();
builder.Services.AddScoped<IFinancialEvaluationService, FinancialEvaluationService>();  
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IComparisonService, ComparisonService>();
builder.Services.AddScoped<IStandardExtractionService, StandardExtractionService>();
builder.Services.AddHttpClient<IPaymobService, PaymobService>();
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddHttpClient<IGeminiService, GeminiService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
})
.AddPolicyHandler((serviceProvider, request) =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<GeminiService>>();

    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        // FIX: Previous formula (30 + 2^retryAttempt) meant a worst-case wait of
        // ~3.5 minutes across 5 retries before the caller ever got a response,
        // leaving users staring at a spinner. Standard capped exponential backoff instead.
        .WaitAndRetryAsync(5, retryAttempt =>
            TimeSpan.FromSeconds(Math.Min(2 * Math.Pow(2, retryAttempt), 20)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                logger.LogWarning("Rate limit hit or connection issue. Delaying for {DelaySeconds}s, then making retry {RetryAttempt}.", timespan.TotalSeconds, retryAttempt);
            });
});

var geminiApiKey = builder.Configuration["Gemini:ApiKey"] ?? throw new ArgumentNullException("Gemini:ApiKey");
var qdrantEndpoint = builder.Configuration["Qdrant:Endpoint"] ?? throw new ArgumentNullException("Qdrant:Endpoint");
var qdrantApiKey = builder.Configuration["Qdrant:ApiKey"] ?? throw new ArgumentNullException("Qdrant:ApiKey");

var qdrantClient = new QdrantClient(
    host: new Uri(qdrantEndpoint).Host,
    https: true,
    apiKey: qdrantApiKey
);
builder.Services.AddSingleton(qdrantClient);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104857600; // 100 MB
});


var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)) 
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("LoginPolicy", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 2
        });
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("StrictCorsPolicy");

app.UseRateLimiter();

app.UseAuthentication(); 
app.UseAuthorization();  

using (var scope = app.Services.CreateScope())
{
    var qClient = scope.ServiceProvider.GetRequiredService<QdrantClient>();
    try 
    {
        await qClient.GetCollectionInfoAsync("meditender_collection_v2");
    }
    catch 
    {
        await qClient.CreateCollectionAsync("meditender_collection_v2", new Qdrant.Client.Grpc.VectorParams { Size = 3072, Distance = Qdrant.Client.Grpc.Distance.Cosine });
        await qClient.CreatePayloadIndexAsync("meditender_collection_v2", "fileName", Qdrant.Client.Grpc.PayloadSchemaType.Keyword);
        await qClient.CreatePayloadIndexAsync("meditender_collection_v2", "documentType", Qdrant.Client.Grpc.PayloadSchemaType.Keyword);
        await qClient.CreatePayloadIndexAsync("meditender_collection_v2", "vendorName", Qdrant.Client.Grpc.PayloadSchemaType.Keyword);
        await qClient.CreatePayloadIndexAsync("meditender_collection_v2", "tenderId", Qdrant.Client.Grpc.PayloadSchemaType.Keyword);
    }
}

app.MapControllers();
app.Run();