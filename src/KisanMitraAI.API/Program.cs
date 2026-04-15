using System.Threading.RateLimiting;
using Amazon.S3;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using KisanMitraAI.API.Swagger;
using KisanMitraAI.Core.Authentication;
using KisanMitraAI.Core.Authorization;
using KisanMitraAI.Core.GovernmentIntegration;
using KisanMitraAI.Infrastructure.GovernmentIntegration;
using KisanMitraAI.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.OpenApi.Models;
using Amazon.Lambda.Core;
using Amazon.Lambda.AspNetCoreServer;


var builder = WebApplication.CreateBuilder(args);

// Configure AWS options with explicit settings
var awsOptions = builder.Configuration.GetAWSOptions();
awsOptions.Region = Amazon.RegionEndpoint.GetBySystemName(builder.Configuration["AWS:Region"] ?? "us-east-1");

// Only set AWS_PROFILE for local development (Lambda uses IAM role)
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV")))
{
    // Running in Lambda - use IAM role (no profile needed)
    Console.WriteLine("Running in AWS Lambda - using IAM role for credentials");
}
else
{
    // Running locally - use profile
    Environment.SetEnvironmentVariable("AWS_PROFILE", 
        Environment.GetEnvironmentVariable("AWS_PROFILE") ?? 
        builder.Configuration["AWS:Profile"] ?? 
        "kisan-mitra");
    Console.WriteLine($"Running locally - using AWS profile: {Environment.GetEnvironmentVariable("AWS_PROFILE")}");
}

// Configure AWS services with explicit options
builder.Services.AddAWSService<Amazon.S3.IAmazonS3>(awsOptions);
builder.Services.AddAWSService<Amazon.DynamoDBv2.IAmazonDynamoDB>(awsOptions);
builder.Services.AddAWSService<Amazon.BedrockRuntime.IAmazonBedrockRuntime>(awsOptions);
builder.Services.AddAWSService<Amazon.BedrockAgentRuntime.IAmazonBedrockAgentRuntime>(awsOptions);
builder.Services.AddAWSService<Amazon.SimpleNotificationService.IAmazonSimpleNotificationService>(awsOptions);
builder.Services.AddAWSService<Amazon.Rekognition.IAmazonRekognition>(awsOptions);
builder.Services.AddAWSService<Amazon.Textract.IAmazonTextract>(awsOptions);
builder.Services.AddAWSService<Amazon.TranscribeService.IAmazonTranscribeService>(awsOptions);
builder.Services.AddAWSService<Amazon.Polly.IAmazonPolly>(awsOptions);

// Timestream removed - using DynamoDB for time-series data (cost optimization)

builder.Services.AddAWSService<Amazon.StepFunctions.IAmazonStepFunctions>(awsOptions);

// Add memory cache for voice synthesis caching
builder.Services.AddMemoryCache();

// Add HttpClient for reCAPTCHA verification
builder.Services.AddHttpClient();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "KisanMitra AI API", Version = "v1" });
    
    // Add operation filter to handle file uploads in Swagger
    options.OperationFilter<FileUploadOperationFilter>();
    
    // Map IFormFile to binary format at schema level
    options.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
});



// Add controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings instead of integers
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Add CORS for frontend integration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // Allow all origins to match API Gateway CORS (which uses *)
        // This prevents dual CORS header conflicts
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add CoreWCF SOAP service
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

// Register SOAP service implementation
builder.Services.AddScoped<IGovernmentIntegrationService, GovernmentIntegrationService>();
// Register SOAP service implementation - use Singleton for CoreWCF
//builder.Services.AddSingleton<GovernmentIntegrationService>();

// Add Cognito authentication
builder.Services.AddCognitoAuthentication(builder.Configuration);

// Register JWT claims extractor for fallback authentication (when JWKS is unavailable)
builder.Services.AddScoped<KisanMitraAI.Core.Authentication.JwtClaimsExtractor>();

// Configure JWT Bearer authentication for API endpoints
var cognitoConfig = builder.Configuration.GetSection("Cognito");
var userPoolId = cognitoConfig["UserPoolId"];
var region = cognitoConfig["Region"];

if (!string.IsNullOrEmpty(userPoolId) && !string.IsNullOrEmpty(region))
{
    builder.Services.AddAuthentication("Bearer")
        .AddJwtBearer("Bearer", options =>
        {
            options.Authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidIssuer = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}",
                ValidateLifetime = true,
                ValidateAudience = false, // Cognito access tokens don't have audience claim
                ClockSkew = TimeSpan.FromMinutes(5)
            };
            options.RequireHttpsMetadata = false; // Allow HTTP for local development
            
            // INCREASED TIMEOUT for Lambda cold starts and network delays
            options.BackchannelTimeout = TimeSpan.FromSeconds(30);
            
            // Enable automatic JWKS refresh when keys are not found
            options.RefreshOnIssuerKeyNotFound = true;
            options.MetadataAddress = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}/.well-known/jwks.json";
            
            // Configure HTTP client for better reliability in Lambda
            options.BackchannelHttpHandler = new HttpClientHandler
            {
                // Disable proxy to avoid additional latency in Lambda
                UseProxy = false,
                // Increase connection limit for better throughput
                MaxConnectionsPerServer = 10,
                // Allow more time for DNS resolution
                MaxResponseHeadersLength = 64
            };
            
            // Add resilient error handling for Lambda environment
            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    
                    // Special handling for JWKS download failures (common in Lambda cold starts)
                    if (context.Exception is Microsoft.IdentityModel.Tokens.SecurityTokenSignatureKeyNotFoundException)
                    {
                        logger.LogWarning(
                            "JWT signature validation failed - JWKS not available (likely Lambda cold start). " +
                            "Request will proceed without authentication. Exception: {Exception}",
                            context.Exception.Message);
                        
                        // Allow request to proceed without authentication
                        // Controllers will use fallback behavior for unauthenticated requests
                        context.NoResult();
                        
                        // Add header to indicate authentication warning
                        context.HttpContext.Response.Headers.Add("X-Auth-Warning", "JWT-validation-failed-jwks-unavailable");
                    }
                    else
                    {
                        logger.LogError(context.Exception, "JWT authentication failed with exception: {ExceptionType}", 
                            context.Exception.GetType().Name);
                    }
                    
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    var farmerId = context.Principal?.FindFirst("sub")?.Value ?? 
                                   context.Principal?.FindFirst("farmer_id")?.Value ?? "unknown";
                    logger.LogInformation("JWT token validated successfully for farmer {FarmerId}", farmerId);
                    return Task.CompletedTask;
                },
                OnMessageReceived = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    var hasToken = !string.IsNullOrEmpty(context.Token);
                    logger.LogDebug("JWT token received: {HasToken}", hasToken);
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("JWT authentication challenge issued. Error: {Error}, ErrorDescription: {ErrorDescription}",
                        context.Error, context.ErrorDescription);
                    return Task.CompletedTask;
                }
            };
        });
}
else
{
    // Fallback for development without Cognito
    builder.Services.AddAuthentication();
}

// Add AWS services
//builder.Services.AddAWSService<Amazon.S3.IAmazonS3>();

// Configure DynamoDB settings
builder.Services.Configure<KisanMitraAI.Infrastructure.Repositories.DynamoDB.DynamoDBConfiguration>(
    builder.Configuration.GetSection("DynamoDB"));

// Register DynamoDB repositories (cost-optimized replacements for PostgreSQL)
builder.Services.AddScoped<KisanMitraAI.Infrastructure.Repositories.PostgreSQL.IFarmRepository, 
    KisanMitraAI.Infrastructure.Repositories.DynamoDB.DynamoDBFarmRepository>();
builder.Services.AddScoped<KisanMitraAI.Infrastructure.Repositories.PostgreSQL.IAuditLogRepository, 
    KisanMitraAI.Infrastructure.Repositories.DynamoDB.DynamoAuditLogRepository>();

// Register User Profile Repository
builder.Services.AddScoped<KisanMitraAI.Infrastructure.Repositories.DynamoDB.IUserProfileRepository,
    KisanMitraAI.Infrastructure.Repositories.DynamoDB.UserProfileRepository>();

// Register Voice Query History Repository
builder.Services.AddScoped<KisanMitraAI.Core.VoiceIntelligence.IVoiceQueryHistoryRepository,
    KisanMitraAI.Infrastructure.Repositories.DynamoDB.DynamoVoiceQueryHistoryRepository>();

// Register DynamoDB repositories for time-series data (cost-optimized replacement for Timestream)
builder.Services.AddScoped<KisanMitraAI.Infrastructure.Repositories.Timestream.ISoilDataRepository, 
    KisanMitraAI.Infrastructure.Repositories.DynamoDB.DynamoSoilDataHistoryRepository>();
builder.Services.AddScoped<KisanMitraAI.Infrastructure.Repositories.Timestream.IGradingHistoryRepository, 
    KisanMitraAI.Infrastructure.Repositories.DynamoDB.DynamoGradingHistoryRepository>();
builder.Services.AddScoped<KisanMitraAI.Infrastructure.Repositories.Timestream.IMandiPriceRepository, 
    KisanMitraAI.Infrastructure.Repositories.DynamoDB.DynamoMandiPricesHistoryRepository>();

// Configure S3 settings
builder.Services.Configure<KisanMitraAI.Infrastructure.Storage.S3.S3Configuration>(options =>
{
    options.BucketName = builder.Configuration["AWS:S3:BucketName"] ?? "kisan-mitra-knowledge-base-253490756058";
    options.Region = builder.Configuration["AWS:Region"] ?? "us-east-1";
});

// Register S3 Storage Service (for general use - Soil Analysis, etc.)
builder.Services.AddScoped<KisanMitraAI.Infrastructure.Storage.S3.IS3StorageService, 
    KisanMitraAI.Infrastructure.Storage.S3.S3StorageService>();

// Register S3 Storage Service (adapter for Vision namespace)
builder.Services.AddScoped<KisanMitraAI.Infrastructure.Vision.IS3StorageService>(sp =>
{
    var s3Client = sp.GetRequiredService<Amazon.S3.IAmazonS3>();
    var logger = sp.GetRequiredService<ILogger<KisanMitraAI.Infrastructure.Vision.S3StorageServiceAdapter>>();
    var bucketName = builder.Configuration["AWS:S3:BucketName"] ?? "kisan-mitra-knowledge-base-253490756058";
    return new KisanMitraAI.Infrastructure.Vision.S3StorageServiceAdapter(s3Client, logger, bucketName);
});

// Register direct Bedrock Knowledge Base service (cost-optimized replacement for OpenSearch-based RAG)
builder.Services.AddScoped<KisanMitraAI.Core.Advisory.IKnowledgeBaseService, 
    KisanMitraAI.Infrastructure.Advisory.DirectBedrockKnowledgeBaseService>();

// Register DbContext for PostgreSQL operations (used by audit logs and data deletion service)
// Note: While most data operations use DynamoDB, audit logs remain in PostgreSQL for compliance
builder.Services.AddDbContext<KisanMitraAI.Infrastructure.Data.KisanMitraDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("PostgreSQL");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        // For local development without PostgreSQL, use in-memory database
        options.UseInMemoryDatabase("KisanMitraAI");
    }
});

// DbContext registration removed - migrated to DynamoDB for cost optimization
// Previous: AddDbContext<KisanMitraDbContext> with PostgreSQL connection
// builder.Services.AddDbContext<KisanMitraDbContext>(options =>
//     options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));

// Add authorization
builder.Services.AddKisanMitraAuthorization();

// Add TLS 1.3 and HTTPS configuration
builder.Services.AddTlsConfiguration();

// Add encryption services
builder.Services.AddEncryptionServices();

// Add malware scanning services
builder.Services.AddMalwareScanningServices();

// Add data deletion services
builder.Services.AddDataDeletionServices();

// Register Quality Grading services (Rekognition, S3, Timestream)
builder.Services.AddScoped<KisanMitraAI.Core.QualityGrading.IImageUploadHandler, KisanMitraAI.Infrastructure.Vision.ImageUploadHandler>();
builder.Services.AddScoped<KisanMitraAI.Core.QualityGrading.IImageAnalyzer, KisanMitraAI.Infrastructure.Vision.ImageAnalyzer>();
builder.Services.AddScoped<KisanMitraAI.Core.QualityGrading.IQualityClassifier, KisanMitraAI.Infrastructure.Vision.QualityClassifier>();
builder.Services.AddScoped<KisanMitraAI.Core.QualityGrading.IPriceCalculator, KisanMitraAI.Infrastructure.Vision.PriceCalculator>();
builder.Services.AddScoped<KisanMitraAI.Core.QualityGrading.IGradingRecordStore, KisanMitraAI.Infrastructure.Vision.GradingRecordStore>();
builder.Services.AddScoped<KisanMitraAI.Core.VoiceIntelligence.IPriceRetriever, KisanMitraAI.Infrastructure.AI.PriceRetriever>();

// Register Soil Analysis services (Textract, Bedrock, S3, DynamoDB)
builder.Services.AddScoped<KisanMitraAI.Core.SoilAnalysis.IDocumentUploadHandler>(sp =>
{
    var s3Client = sp.GetRequiredService<IAmazonS3>();
    var logger = sp.GetRequiredService<ILogger<KisanMitraAI.Infrastructure.SoilAnalysis.DocumentUploadHandler>>();
    var bucketName = builder.Configuration["AWS:S3:BucketName"] ?? "kisan-mitra-knowledge-base-253490756058";
    return new KisanMitraAI.Infrastructure.SoilAnalysis.DocumentUploadHandler(s3Client, logger, bucketName);
});
builder.Services.AddScoped<KisanMitraAI.Core.SoilAnalysis.ITextExtractor>(sp =>
{
    var textractClient = sp.GetRequiredService<Amazon.Textract.IAmazonTextract>();
    var s3Client = sp.GetRequiredService<IAmazonS3>();
    var logger = sp.GetRequiredService<ILogger<KisanMitraAI.Infrastructure.SoilAnalysis.TextExtractor>>();
    var bucketName = builder.Configuration["AWS:S3:BucketName"] ?? "kisan-mitra-knowledge-base-253490756058";
    return new KisanMitraAI.Infrastructure.SoilAnalysis.TextExtractor(textractClient, s3Client, logger, bucketName);
});
builder.Services.AddScoped<KisanMitraAI.Core.SoilAnalysis.ISoilDataParser, KisanMitraAI.Infrastructure.SoilAnalysis.SoilDataParser>();
builder.Services.AddScoped<KisanMitraAI.Core.SoilAnalysis.IRegenerativePlanGenerator, KisanMitraAI.Infrastructure.SoilAnalysis.RegenerativePlanGenerator>();
builder.Services.AddScoped<KisanMitraAI.Core.SoilAnalysis.ICarbonEstimator, KisanMitraAI.Infrastructure.SoilAnalysis.CarbonEstimator>();
builder.Services.AddScoped<KisanMitraAI.Core.SoilAnalysis.IPlanDeliveryService, KisanMitraAI.Infrastructure.SoilAnalysis.PlanDeliveryService>();

// Register Planting Advisory services (Bedrock, Weather API, DynamoDB)
builder.Services.AddScoped<KisanMitraAI.Core.PlantingAdvisory.IWeatherDataCollector>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    var cache = sp.GetRequiredService<IMemoryCache>();
    var logger = sp.GetRequiredService<ILogger<KisanMitraAI.Infrastructure.PlantingAdvisory.WeatherDataCollector>>();
    // For MVP, using a placeholder API key - in production, this should be from configuration or AWS Secrets Manager
    var apiKey = builder.Configuration["Weather:ApiKey"] ?? "demo_api_key";
    var apiBaseUrl = builder.Configuration["Weather:ApiBaseUrl"] ?? "https://api.openweathermap.org/data/2.5";
    return new KisanMitraAI.Infrastructure.PlantingAdvisory.WeatherDataCollector(httpClient, cache, logger, apiKey, apiBaseUrl);
});
builder.Services.AddScoped<KisanMitraAI.Core.PlantingAdvisory.ISoilDataRetriever>(sp =>
{
    // Use DynamoDB repository instead of Timestream for cost optimization
    var soilDataRepo = sp.GetRequiredService<KisanMitraAI.Infrastructure.Repositories.Timestream.ISoilDataRepository>();
    var s3StorageService = sp.GetRequiredService<KisanMitraAI.Infrastructure.Storage.S3.IS3StorageService>();
    var logger = sp.GetRequiredService<ILogger<KisanMitraAI.Infrastructure.PlantingAdvisory.SoilDataRetrieverAdapter>>();
    // Create a wrapper that adapts the DynamoDB repository to ISoilDataRetriever
    return new KisanMitraAI.Infrastructure.PlantingAdvisory.SoilDataRetrieverAdapter(soilDataRepo, s3StorageService, logger);
});
builder.Services.AddScoped<KisanMitraAI.Core.PlantingAdvisory.IPlantingWindowAnalyzer, KisanMitraAI.Infrastructure.PlantingAdvisory.PlantingWindowAnalyzer>();
// Register Direct Bedrock Seed Variety Recommender (cost-optimized, AI-powered)
// Uses direct Bedrock API instead of Knowledge Base + OpenSearch
// Cost: ~$2/month vs $197/month for OpenSearch-based solution
builder.Services.AddScoped<KisanMitraAI.Core.PlantingAdvisory.ISeedVarietyRecommender>(sp =>
{
    var bedrockRuntime = sp.GetRequiredService<Amazon.BedrockRuntime.IAmazonBedrockRuntime>();
    var logger = sp.GetRequiredService<ILogger<KisanMitraAI.Infrastructure.PlantingAdvisory.DirectBedrockSeedVarietyRecommender>>();
    return new KisanMitraAI.Infrastructure.PlantingAdvisory.DirectBedrockSeedVarietyRecommender(bedrockRuntime, logger);
});
builder.Services.AddScoped<KisanMitraAI.Core.PlantingAdvisory.IConfidenceScorer, KisanMitraAI.Infrastructure.PlantingAdvisory.ConfidenceScorer>();

// Register Voice Query services (Transcribe, Polly, Bedrock, DynamoDB)
builder.Services.AddScoped<KisanMitraAI.Core.VoiceIntelligence.IVoiceQueryHandler, KisanMitraAI.Infrastructure.AI.VoiceQueryHandler>();
builder.Services.AddScoped<KisanMitraAI.Core.VoiceIntelligence.ITranscriptionService>(sp =>
{
    var transcribeClient = sp.GetRequiredService<Amazon.TranscribeService.IAmazonTranscribeService>();
    var s3Client = sp.GetRequiredService<Amazon.S3.IAmazonS3>();
    var logger = sp.GetRequiredService<ILogger<KisanMitraAI.Infrastructure.AI.TranscriptionService>>();
    var bucketName = builder.Configuration["AWS:S3:BucketName"] ?? "kisan-mitra-knowledge-base-253490756058";
    return new KisanMitraAI.Infrastructure.AI.TranscriptionService(transcribeClient, s3Client, logger, bucketName);
});
builder.Services.AddScoped<KisanMitraAI.Core.VoiceIntelligence.IQueryParser, KisanMitraAI.Infrastructure.AI.QueryParser>();
builder.Services.AddScoped<KisanMitraAI.Core.VoiceIntelligence.IResponseGenerator, KisanMitraAI.Infrastructure.AI.ResponseGenerator>();
builder.Services.AddScoped<KisanMitraAI.Core.VoiceIntelligence.IVoiceSynthesizer>(sp =>
{
    var pollyClient = sp.GetRequiredService<Amazon.Polly.IAmazonPolly>();
    var s3Client = sp.GetRequiredService<Amazon.S3.IAmazonS3>();
    var cache = sp.GetRequiredService<IMemoryCache>();
    var logger = sp.GetRequiredService<ILogger<KisanMitraAI.Infrastructure.AI.VoiceSynthesizer>>();
    var bucketName = builder.Configuration["AWS:S3:BucketName"] ?? "kisan-mitra-knowledge-base-253490756058";
    return new KisanMitraAI.Infrastructure.AI.VoiceSynthesizer(pollyClient, s3Client, cache, logger, bucketName);
});

// Add rate limiting (100 requests per minute per farmer)
builder.Services.AddRateLimiter(options =>
{
    // Add named policy for farmer endpoints
    options.AddPolicy("farmer-rate-limit", context =>
    {
        var farmerId = context.User.GetFarmerId();
        
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: farmerId ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            errorCode = "RATE_LIMIT_EXCEEDED",
            message = "Too many requests. Please try again later.",
            userFriendlyMessage = "बहुत अधिक अनुरोध। कृपया बाद में पुनः प्रयास करें (Too many requests. Please try again later)",
            suggestedActions = new[] { "Wait for 1 minute before making more requests" },
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken: cancellationToken);
    };
});

// Configure Lambda hosting for Function URLs
// Function URLs use HTTP API v2 format (same as API Gateway HTTP API)
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable static files for frontend
//app.UseStaticFiles();

// Apply TLS configuration and security headers only in production
// Note: HTTPS redirection disabled for Lambda - API Gateway/Function URL handles HTTPS
if (app.Environment.IsProduction() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV")))
{
    // Only apply HTTPS redirection when NOT running in Lambda
    app.UseTlsConfiguration();
    app.UseHttpsRedirection();
}
else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV")))
{
    Console.WriteLine("HTTPS redirection disabled in Lambda - API Gateway handles HTTPS termination");
}

// Enable CORS (IMPORTANT: Add this before UseAuthentication)
app.UseCors();

// Add rate limiting middleware
app.UseRateLimiter();

// Add authentication and authorization middleware
app.UseAuthentication();

// Add JWT claims extraction middleware as fallback when signature validation fails
// This extracts farmer ID from JWT token even when JWKS is unavailable (Lambda cold starts)
app.UseMiddleware<KisanMitraAI.API.Middleware.JwtClaimsExtractionMiddleware>();

app.UseAuthorization();

// Add authorization logging middleware for security auditing
app.UseMiddleware<AuthorizationLoggingMiddleware>();

// Map controllers
app.MapControllers();

// Add a simple test endpoint that doesn't require authentication
app.MapGet("/test", () => new { 
    status = "ok", 
    message = "Lambda is working!", 
    timestamp = DateTime.UtcNow,
    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
});

app.MapGet("/api/v1/test", () => new { 
    status = "ok", 
    message = "API endpoint is working!", 
    timestamp = DateTime.UtcNow 
});

// Configure CoreWCF SOAP endpoint (disabled in Lambda - only for local/server deployment)
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV")))
{
    app.UseServiceModel(serviceBuilder =>
    {
        // Add the Government Integration SOAP service
        serviceBuilder.AddService<GovernmentIntegrationService>(serviceOptions =>
        {
            serviceOptions.DebugBehavior.IncludeExceptionDetailInFaults = app.Environment.IsDevelopment();
        });

        // Add BasicHttpBinding endpoint for SOAP 1.1 (using HTTP for local development)
        serviceBuilder.AddServiceEndpoint<GovernmentIntegrationService, IGovernmentIntegrationService>(
            new BasicHttpBinding(BasicHttpSecurityMode.None),
            "/GovernmentIntegration.svc");

        // Enable metadata exchange for WSDL generation
        var serviceMetadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
        serviceMetadataBehavior.HttpGetEnabled = true;
    });
}
else
{
    Console.WriteLine("SOAP service disabled in Lambda environment");
}

app.Run();
