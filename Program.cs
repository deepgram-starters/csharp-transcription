/**
 * C# Transcription Starter - Backend Server
 *
 * This is a minimal API server that provides a transcription API endpoint
 * powered by Deepgram's Speech-to-Text service. It's designed to be easily
 * modified and extended for your own projects.
 *
 * Key Features:
 * - Single API endpoint: POST /api/transcription
 * - Accepts both file uploads and URLs
 * - JWT session auth with page nonce (production only)
 * - CORS enabled for frontend communication
 * - Pure API server (frontend served separately)
 */

using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Deepgram;
using Deepgram.Models.Listen.v1.REST;
using Microsoft.IdentityModel.Tokens;
using Tomlyn;
using Tomlyn.Model;
using HttpResults = Microsoft.AspNetCore.Http.Results;

// ============================================================================
// ENVIRONMENT LOADING
// ============================================================================

DotNetEnv.Env.Load();

// ============================================================================
// CONFIGURATION - Customize these values for your needs
// ============================================================================

/// Default transcription model to use when none is specified
/// Options: "nova-3", "nova-2", "nova", "enhanced", "base"
/// See: https://developers.deepgram.com/docs/models-languages-overview
const string DefaultModel = "nova-3";

var port = int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var p) ? p : 8081;
var host = Environment.GetEnvironmentVariable("HOST") ?? "0.0.0.0";

// ============================================================================
// SESSION AUTH - JWT tokens with page nonce for production security
// ============================================================================

var sessionSecretEnv = Environment.GetEnvironmentVariable("SESSION_SECRET");
var sessionSecret = sessionSecretEnv ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
var requireNonce = !string.IsNullOrEmpty(sessionSecretEnv);
var sessionSecretKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(sessionSecret));

var sessionNonces = new ConcurrentDictionary<string, long>();
const int NonceTtlSeconds = 5 * 60; // 5 minutes
const int JwtExpirySeconds = 3600; // 1 hour

string GenerateNonce()
{
    var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    sessionNonces[nonce] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + NonceTtlSeconds;
    return nonce;
}

bool ConsumeNonce(string nonce)
{
    if (!sessionNonces.TryRemove(nonce, out var expiry))
        return false;
    return DateTimeOffset.UtcNow.ToUnixTimeSeconds() < expiry;
}

// Cleanup expired nonces every 60 seconds
var nonceCleanupTimer = new Timer(_ =>
{
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    foreach (var kvp in sessionNonces)
    {
        if (now >= kvp.Value)
            sessionNonces.TryRemove(kvp.Key, out _);
    }
}, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

// Read frontend/dist/index.html template for nonce injection
string? indexHtmlTemplate = null;
try
{
    indexHtmlTemplate = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "frontend", "dist", "index.html"));
}
catch (FileNotFoundException) { }

string CreateSessionToken()
{
    var handler = new JwtSecurityTokenHandler();
    var descriptor = new SecurityTokenDescriptor
    {
        Expires = DateTime.UtcNow.AddSeconds(JwtExpirySeconds),
        SigningCredentials = new SigningCredentials(sessionSecretKey, SecurityAlgorithms.HmacSha256Signature),
    };
    var token = handler.CreateToken(descriptor);
    return handler.WriteToken(token);
}

bool ValidateSessionToken(string token)
{
    try
    {
        var handler = new JwtSecurityTokenHandler();
        handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = sessionSecretKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero,
        }, out _);
        return true;
    }
    catch
    {
        return false;
    }
}

// ============================================================================
// API KEY LOADING - Load Deepgram API key from .env
// ============================================================================

/// Loads the Deepgram API key from environment variables
/// Priority: DEEPGRAM_API_KEY env var > error
static string LoadApiKey()
{
    var apiKey = Environment.GetEnvironmentVariable("DEEPGRAM_API_KEY");

    if (string.IsNullOrEmpty(apiKey))
    {
        Console.Error.WriteLine("\n❌ ERROR: Deepgram API key not found!\n");
        Console.Error.WriteLine("Please set your API key using one of these methods:\n");
        Console.Error.WriteLine("1. Create a .env file (recommended):");
        Console.Error.WriteLine("   DEEPGRAM_API_KEY=your_api_key_here\n");
        Console.Error.WriteLine("2. Environment variable:");
        Console.Error.WriteLine("   export DEEPGRAM_API_KEY=your_api_key_here\n");
        Console.Error.WriteLine("Get your API key at: https://console.deepgram.com\n");
        Environment.Exit(1);
    }

    return apiKey;
}

var apiKey = LoadApiKey();

// ============================================================================
// SETUP - Initialize ASP.NET Minimal API, Deepgram, and middleware
// ============================================================================

// Initialize Deepgram library and client
Library.Initialize();
var deepgramClient = ClientFactory.CreateListenRESTClient(apiKey);

// Initialize ASP.NET Minimal API
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://{host}:{port}");

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors();

// ============================================================================
// SESSION ROUTES - Auth endpoints (unprotected)
// ============================================================================

/// GET / — Serve index.html with injected session nonce (production only)
app.MapGet("/", () =>
{
    if (indexHtmlTemplate == null)
        return HttpResults.Text("Frontend not built. Run make build first.", statusCode: 404);

    // Cleanup expired nonces
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    foreach (var kvp in sessionNonces)
    {
        if (now >= kvp.Value)
            sessionNonces.TryRemove(kvp.Key, out _);
    }

    var nonce = GenerateNonce();
    var html = indexHtmlTemplate.Replace("</head>", $"<meta name=\"session-nonce\" content=\"{nonce}\">\n</head>");
    return HttpResults.Content(html, "text/html");
});

/// GET /api/session — Issues a JWT. In production, requires valid nonce.
app.MapGet("/api/session", (HttpRequest request) =>
{
    if (requireNonce)
    {
        var nonce = request.Headers["X-Session-Nonce"].FirstOrDefault();
        if (string.IsNullOrEmpty(nonce) || !ConsumeNonce(nonce))
        {
            return HttpResults.Json(new Dictionary<string, object>
            {
                ["error"] = new Dictionary<string, string>
                {
                    ["type"] = "AuthenticationError",
                    ["code"] = "INVALID_NONCE",
                    ["message"] = "Valid session nonce required. Please refresh the page.",
                }
            }, statusCode: 403);
        }
    }

    var token = CreateSessionToken();
    return HttpResults.Json(new Dictionary<string, string> { ["token"] = token });
});

// ============================================================================
// HELPER FUNCTIONS - Modular logic for easier understanding and testing
// ============================================================================

/// Validates that either a file or URL was provided in the request
static async Task<(string? url, byte[]? fileBytes, string? mimeType)> ValidateTranscriptionInput(
    IFormFile? file, string? url)
{
    if (!string.IsNullOrEmpty(url))
        return (url, null, null);

    if (file is { Length: > 0 })
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return (null, ms.ToArray(), file.ContentType);
    }

    return (null, null, null);
}

/// Formats Deepgram's response into a simplified, consistent structure
static Dictionary<string, object?> FormatTranscriptionResponse(
    SyncResponse transcription, string modelName)
{
    var result = transcription.Results?.Channels?[0]?.Alternatives?[0];

    if (result == null)
        throw new InvalidOperationException("No transcription results returned from Deepgram");

    // Build word list as dictionaries for snake_case JSON output
    var words = new List<Dictionary<string, object?>>();
    if (result.Words != null)
    {
        foreach (var w in result.Words)
        {
            words.Add(new Dictionary<string, object?>
            {
                ["word"] = w.HeardWord,
                ["start"] = w.Start,
                ["end"] = w.End,
                ["confidence"] = w.Confidence,
                ["punctuated_word"] = w.PunctuatedWord,
            });
        }
    }

    // Extract model_uuid from ModelInfo dictionary (key is the UUID)
    string? modelUuid = null;
    if (transcription.Metadata?.ModelInfo?.Count > 0)
        modelUuid = transcription.Metadata.ModelInfo.Keys.First();

    var response = new Dictionary<string, object?>
    {
        ["transcript"] = result.Transcript ?? "",
        ["words"] = words,
        ["metadata"] = new Dictionary<string, object?>
        {
            ["model_uuid"] = modelUuid,
            ["request_id"] = transcription.Metadata?.RequestId,
            ["model_name"] = modelName,
        },
    };

    if (transcription.Metadata?.Duration != null && transcription.Metadata.Duration > 0)
        response["duration"] = transcription.Metadata.Duration;

    return response;
}

/// Formats error responses in a consistent structure
static (int statusCode, object body) FormatErrorResponse(
    string message, int statusCode = 500, string? originalError = null)
{
    return (statusCode, new Dictionary<string, object?>
    {
        ["error"] = new Dictionary<string, object?>
        {
            ["type"] = statusCode == 400 ? "ValidationError" : "TranscriptionError",
            ["code"] = statusCode == 400 ? "MISSING_INPUT" : "TRANSCRIPTION_FAILED",
            ["message"] = message,
            ["details"] = new Dictionary<string, object?>
            {
                ["originalError"] = originalError ?? message,
            },
        },
    });
}

// ============================================================================
// API ROUTES - Define your API endpoints here
// ============================================================================

/// POST /api/transcription
///
/// Main transcription endpoint. Accepts either:
/// - A file upload (multipart/form-data with 'file' field)
/// - A URL to audio file (form data with 'url' field)
///
/// Optional parameters:
/// - model: Deepgram model to use (default: "nova-3")
app.MapPost("/api/transcription", async (HttpRequest request) =>
{
    // Validate JWT session token
    var authHeader = request.Headers.Authorization.FirstOrDefault() ?? "";
    if (!authHeader.StartsWith("Bearer "))
    {
        return HttpResults.Json(new Dictionary<string, object>
        {
            ["error"] = new Dictionary<string, string>
            {
                ["type"] = "AuthenticationError",
                ["code"] = "MISSING_TOKEN",
                ["message"] = "Authorization header with Bearer token is required",
            }
        }, statusCode: 401);
    }
    if (!ValidateSessionToken(authHeader[7..]))
    {
        return HttpResults.Json(new Dictionary<string, object>
        {
            ["error"] = new Dictionary<string, string>
            {
                ["type"] = "AuthenticationError",
                ["code"] = "INVALID_TOKEN",
                ["message"] = "Invalid or expired session token",
            }
        }, statusCode: 401);
    }

    try
    {
        var form = await request.ReadFormAsync();
        var file = form.Files.GetFile("file");
        var url = form["url"].FirstOrDefault();
        var model = form["model"].FirstOrDefault() ?? DefaultModel;

        // Validate input
        var (inputUrl, fileBytes, mimeType) = await ValidateTranscriptionInput(file, url);
        if (inputUrl == null && fileBytes == null)
        {
            var (errCode, errBody) = FormatErrorResponse(
                "Either file or url must be provided", 400);
            return HttpResults.Json(errBody, statusCode: errCode);
        }

        // Build schema options
        var schema = new PreRecordedSchema { Model = model };

        // Send transcription request to Deepgram
        SyncResponse? transcription;
        if (inputUrl != null)
        {
            transcription = await deepgramClient.TranscribeUrl(
                new UrlSource(inputUrl), schema);
        }
        else
        {
            transcription = await deepgramClient.TranscribeFile(
                fileBytes!, schema);
        }

        if (transcription == null)
            throw new InvalidOperationException("Deepgram returned null response");

        // Format and return response
        var response = FormatTranscriptionResponse(transcription, model);
        return HttpResults.Json(response);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Transcription error: {ex}");
        var (errCode, errBody) = FormatErrorResponse(
            ex.Message, 500, ex.ToString());
        return HttpResults.Json(errBody, statusCode: errCode);
    }
});

/// GET /api/metadata
///
/// Returns metadata about this starter application from deepgram.toml
app.MapGet("/api/metadata", () =>
{
    try
    {
        var tomlPath = Path.Combine(Directory.GetCurrentDirectory(), "deepgram.toml");
        var tomlContent = File.ReadAllText(tomlPath);
        var tomlModel = Toml.ToModel(tomlContent);

        if (!tomlModel.ContainsKey("meta") || tomlModel["meta"] is not TomlTable metaTable)
        {
            return HttpResults.Json(new Dictionary<string, string>
            {
                ["error"] = "INTERNAL_SERVER_ERROR",
                ["message"] = "Missing [meta] section in deepgram.toml",
            }, statusCode: 500);
        }

        // Convert TomlTable to dictionary for JSON serialization
        var meta = new Dictionary<string, object?>();
        foreach (var kvp in metaTable)
        {
            meta[kvp.Key] = kvp.Value;
        }

        return HttpResults.Json(meta);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error reading metadata: {ex}");
        return HttpResults.Json(new Dictionary<string, string>
        {
            ["error"] = "INTERNAL_SERVER_ERROR",
            ["message"] = "Failed to read metadata from deepgram.toml",
        }, statusCode: 500);
    }
});

// ============================================================================
// SERVER START
// ============================================================================

var nonceStatus = requireNonce ? " (nonce required)" : "";
Console.WriteLine();
Console.WriteLine(new string('=', 70));
Console.WriteLine($"🚀 Backend API Server running at http://localhost:{port}");
Console.WriteLine($"📡 CORS enabled for all origins");
Console.WriteLine($"📡 GET  /api/session{nonceStatus}");
Console.WriteLine($"📡 POST /api/transcription (auth required)");
Console.WriteLine($"📡 GET  /api/metadata");
Console.WriteLine(new string('=', 70));
Console.WriteLine();

app.Run();
