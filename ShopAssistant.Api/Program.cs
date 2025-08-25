using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShopAssistant.Api.Middleware;
using ShopAssistant.Contracts.Config;
using ShopAssistant.Contracts.Interfaces.Analytics;
using ShopAssistant.Contracts.Interfaces.Chat;
using ShopAssistant.Contracts.Interfaces.Data;
using ShopAssistant.Contracts.Interfaces.Integrations;
using ShopAssistant.Contracts.Interfaces.Intent;
using ShopAssistant.Contracts.Interfaces.KnowledgeBase;
using ShopAssistant.Contracts.Interfaces.Localization;
using ShopAssistant.Contracts.Interfaces.TextProcessing;
using ShopAssistant.Contracts.Interfaces.User;
using ShopAssistant.Contracts.Models.Analytics;
using ShopAssistant.Infrastructure.Analytics;
using ShopAssistant.Infrastructure.Chat;
using ShopAssistant.Infrastructure.Data;
using ShopAssistant.Infrastructure.ExternalServices;
using ShopAssistant.Infrastructure.Identity;
using ShopAssistant.Infrastructure.KnowledgeBase;
using ShopAssistant.Infrastructure.Localization;
using ShopAssistant.Infrastructure.TextProcessing.Intent;
using ShopAssistant.Infrastructure.TextProcessing.Lexical;
using ShopAssistant.Infrastructure.TextProcessing.SemanticSearch;
using ShopAssistant.Infrastructure.TextProcessing.SemanticSearch.Embeddings;
using ShopAssistant.Infrastructure.User;
using ShopAssistant.Infrastructure.Validations;
using ShopAssistant.IntentProcessing.IntentDetectors;
using ShopAssistant.IntentProcessing.IntentHandlers;
using System.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ---------------------- CORS ----------------------
builder.Services.AddCors(options =>
{
    // Development
    options.AddPolicy("AllowAll", p =>
        p.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());

    // Production (only allow your frontend app)
    options.AddPolicy("AllowFrontend", p =>
        p.WithOrigins("https://yourfrontend.example.com")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// ---------------------- HttpContext, User context ----------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, UserContext>();

builder.Services.AddControllers();

// ---------------------- Swagger + JWT Auth ----------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "ShopAssistant API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});

builder.Services.Configure<AnalyticsOptions>(builder.Configuration.GetSection("AnalyticsOptions"));
builder.Services.Configure<IntentLoggingOptions>(builder.Configuration.GetSection("IntentLogging"));
builder.Services.Configure<EmbeddingsOptions>(builder.Configuration.GetSection("LaBSE"));

// ---------------------- Ollama embedding (typed HttpClient) ----------------------
//builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
//builder.Services.AddHttpClient<ITextEmbedder, OllamaEmbedder>();

builder.Services.AddSingleton<ITextEmbedder, LabseEmbedder>();

builder.Services.AddMemoryCache();

// ---------------------- Register all IIntentHandler implementations ----------------------
builder.Services.AddScoped<IIntentHandler, OrderStatusIntentHandler>();
builder.Services.AddScoped<IIntentHandler, ProductSearchIntentHandler>();
builder.Services.AddScoped<IIntentHandler, ChangeDeliveryAddressIntentHandler>();
builder.Services.AddScoped<IIntentHandler, ContactSupportIntentHandler>();
builder.Services.AddScoped<IIntentHandler, RecommendationIntentHandler>();
builder.Services.AddScoped<IIntentProcessingService, IntentProcessingService>();
builder.Services.AddSingleton<IIntentPatternCacheService, MemoryIntentPatternCacheService>();
builder.Services.AddSingleton<IIntentPatternMatcher, IntentPatternMatcher>();
builder.Services.AddScoped<IntentKnowledgeValidationService>();

// ---------------------- Intent Detector Embeddings Cache (for HybridIntentDetector) ----------------------
builder.Services.AddSingleton<IIntentDetector, HybridIntentDetector>();
builder.Services.AddSingleton<IIntentDetectorEmbeddingsCacheService, IntentDetectorEmbeddingsCacheService>();

// ---------------------- ANN cache for per-language embeddings/index ----------------------
builder.Services.AddSingleton<EmbeddingIndexCacheService>();

// ---------------------- DI registrations for core services -------------------------------
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddSingleton<ILocalizationService, LocalizationService>();
builder.Services.AddSingleton<ITopicRolePermissionProvider, TopicRolePermissionProvider>();

builder.Services.AddSingleton<IUserChatContextService, InMemoryUserChatContextService>();
builder.Services.AddSingleton<IKnowledgeItemCacheService, KnowledgeItemCacheService>();
builder.Services.AddSingleton<IKnowledgeLoader, KnowledgeLoader>();

// --------------------- Initializers for embedding and knowledge caches ----------------------
builder.Services.AddSingleton<KnowledgeCacheInitializer>();
builder.Services.AddSingleton<IntentEmbeddingCacheInitializer>();
builder.Services.AddSingleton<KnowledgeLexicalIndexInitializer>();

// ---------------------- ANN-aware services ----------------------
builder.Services.AddScoped<ISemanticSearchService, SemanticSearchService>();
builder.Services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
builder.Services.AddSingleton<IBm25QuestionIndex, Bm25QuestionIndex>();
builder.Services.AddScoped<IKnowledgeBaseQueryService, HybridKnowledgeBaseQueryService>();

// ---------------------- External services  ----------------------
builder.Services.AddScoped<IOrderService, ExternalOrderService>();
builder.Services.AddScoped<IProductSearchService, ExternalProductSearchService>();
builder.Services.AddScoped<IRecommendationService, ExternalRecommendationService>();

// ---------------------- Analytics database connection ----------------------
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var factory = sp.GetRequiredService<IDbConnectionFactory>();
    var conn = factory.CreateConnection("Analytics");
    conn.Open();
    return conn;
});

builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();

builder.Services.AddScoped<KnowledgeExporter>();
builder.Services.AddScoped<IntentKnowledgeValidationService>();


// ---------- ATTENTION !!!! ---------------------------------------------------------------
// ---------- ONLY FOR DEVELOPMENT - SHOULD BE REMOVED IN RELEASE VERSION ------------------
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<ITokenService, TokenService>();
}

// ---------------------- JWT Authentication -----------------------------------------------
var key = builder.Configuration.GetValue<string>("JWT:Key") ?? throw new InvalidOperationException("JWT signing key is missing.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
    };
});

var app = builder.Build();

// ---------------------- Embedding & knowledge preload at startup ----------------------
using (var scope = app.Services.CreateScope())
{
    try
    {
        // ------------------- Initialize cache for Knowledge Base Embeddings and Vector Store -------------------
        var embeddingCacheService = scope.ServiceProvider.GetRequiredService<EmbeddingIndexCacheService>();
        await embeddingCacheService.InitializeCacheAsync();

        // ------------------- Initialize cache for Knowledge Base Items -------------------
        var knowledgeCacheInitializer = scope.ServiceProvider.GetRequiredService<KnowledgeCacheInitializer>();
        await knowledgeCacheInitializer.InitializeCacheAsync();

        // ------------------- Initialize cache of Intent Patterns for HybridIntentDetector -------------------
        var patternCacheService = scope.ServiceProvider.GetRequiredService<IIntentPatternCacheService>();
        await patternCacheService.InitializeCacheAsync();

        // ------------------- Initialize cache for Intent Embeddings for HybridIntentDetector -------------------
        var intentEmbeddingsInitializer = scope.ServiceProvider.GetRequiredService<IntentEmbeddingCacheInitializer>();
        await intentEmbeddingsInitializer.InitializeCacheAsync();

        // ------------------- Initialize cache for Role Permissions  -------------------
        var topicRolePermissionProvider = scope.ServiceProvider.GetRequiredService<ITopicRolePermissionProvider>();
        await topicRolePermissionProvider.InitializeCacheAsync();

        // ------------------- Initialize cache for Localization messages -------------------
        var localizationService = scope.ServiceProvider.GetRequiredService<ILocalizationService>();
        await localizationService.InitializeCacheAsync();

        // ------------------- Initialize BM25 lexical index   -------------------
        var kbLexicalInitializer = scope.ServiceProvider.GetRequiredService<KnowledgeLexicalIndexInitializer>();
        await kbLexicalInitializer.InitializeAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "Fatal error during application startup initialization.");
        throw; // Fail fast, app won't run in bad state
    }
}

// ---------------------- Swagger UI (for development only) ----------------------
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ShopAssistant API V1");
    });
}
else
{
    app.UseCors("AllowFrontend");
}

var kbPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "ShopAssistant.Data", "KnowledgeBase"));

if (!Directory.Exists(kbPath))
    throw new DirectoryNotFoundException(kbPath);

var contentTypes = new FileExtensionContentTypeProvider();

app.UseHttpsRedirection();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(kbPath),
    RequestPath = "/KnowledgeBase",
    ContentTypeProvider = contentTypes
});

app.UseCors(app.Environment.IsDevelopment() ? "AllowAll" : "AllowFrontend");

app.UseAuthentication();
app.UseMiddleware<UserContextMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();
