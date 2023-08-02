using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Deepgram;
using DotNetEnv;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json.Serialization;
using Deepgram.Transcription;


public class FrontendServer
{
    private readonly int port;

    private async Task HandleApiRequest(HttpContext context)
{

    if (context.Request.Method != "POST")
    {
        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        await context.Response.WriteAsync("Method not allowed. Only POST requests are accepted.");
        return;
    }

    if (context.Request.HasFormContentType)
    {
         // Load .env file
        DotNetEnv.Env.Load();

        // Access the values from environment variables
        string apiKey = Environment.GetEnvironmentVariable("deepgram_api_key");
        var credentials = new Credentials(apiKey);
        var deepgram = new DeepgramClient(credentials);
        
        var form = await context.Request.ReadFormAsync();

        // Accessing form data
        string url = form["url"];
        string model = form["model"];
        string tier = form["tier"];
        string features = form["features"];
        var featuresData = null as Dictionary<string, object>;

        if (!string.IsNullOrEmpty(features))
        {
            try
            {
                // Parse the JSON data into a dictionary
                featuresData = JsonSerializer.Deserialize<Dictionary<string, object>>(features);
            }
            catch (JsonException)
            {
                // JSON parsing failed, handle the error
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Invalid JSON data in the form.");
                return;
            }
        }

        Console.WriteLine($"Received request for {url} with model {model} and tier {tier}");
        Console.WriteLine($"Features: {features}");

        // Handle file uploads
        var file = form.Files.GetFile("file");

        // Create the PrerecordedTranscriptionOptions object
        var transcriptionOptions = new PrerecordedTranscriptionOptions
        {
            Model = model,
        };

        // Set the tier if it was provided
        if (!string.IsNullOrEmpty(tier) && tier != "undefined")
        {
            transcriptionOptions.Tier = tier;
        }

        Console.WriteLine($"Transcription options: {JsonSerializer.Serialize(transcriptionOptions)}");

        // Set the features if they were provided by traversing the dictionary
        if (featuresData != null)
        {
            foreach (var feature in featuresData)
            {
                var key = feature.Key.ToString();
                var value = feature.Value.ToString();
                SetFeatures(transcriptionOptions, key, value);
            }
        }
        
        Console.WriteLine($"Transcription options: {JsonSerializer.Serialize(transcriptionOptions)}");

        if (file != null && file.Length > 0)
        {
            // Process the uploaded file (save, manipulate, etc.)
            // Example:
            string fileName = file.FileName;
            using (var stream = new FileStream(fileName, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
        } else {
            var deepgramResponse = await deepgram.Transcription.Prerecorded.GetTranscriptionAsync(
                new Deepgram.Transcription.UrlSource("https://static.deepgram.com/examples/Bueller-Life-moves-pretty-fast.wav"),
               transcriptionOptions);
            Console.WriteLine(JsonSerializer.Serialize(deepgramResponse));
        }

        // Your API logic goes here
        // Example:
        string response = $"File uploaded successfully!";
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync(response);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Invalid request format. Only form data is accepted.");
    }
}

    public FrontendServer(int port)
    {
        this.port = port;
    }

    public void Start()
    {
        var host = new WebHostBuilder()
        .UseKestrel()
        .ConfigureServices(services => services.AddSingleton(this))
        .Configure(app =>
        {
            app.Map("/api", apiApp => apiApp.Run(HandleApiRequest));
            app.Run(HandleRequest);
        })
        .UseUrls($"http://localhost:{port}/")
        .Build();

        host.Run();
    }

    private Task HandleRequest(HttpContext context)
    {
        var uri = context.Request.Path;
        if (uri == "/")
        {
            uri = "/index.html"; // Default to index.html if root is requested
        }
        var filePath = "./static" + uri;

        if (File.Exists(filePath))
        {
            var mimeType = GetMimeTypeForFile(uri);
            context.Response.ContentType = mimeType;
            return context.Response.SendFileAsync(filePath);
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return context.Response.WriteAsync("File not found");
    }

    private string GetMimeTypeForFile(string uri)
    {
        if (uri.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            return "text/html";
        }
        else if (uri.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            return "text/css";
        }
        else if (uri.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return "image/svg+xml";
        }
        else if (uri.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            return "application/javascript";
        }
        else
        {
            return "text/plain";
        }
    }

    public static void SetFeatures(PrerecordedTranscriptionOptions transcriptionOptions, string key, string value)
    {
        switch (key)
        {
            case "smart_format":
                transcriptionOptions.SmartFormat = bool.Parse(value);
                break;
            case "punctuate":
                transcriptionOptions.Punctuate = bool.Parse(value);
                break;
            case "paragraphs":
                transcriptionOptions.Paragraphs = bool.Parse(value);
                break;
            case "utterances":
                transcriptionOptions.Utterances = bool.Parse(value);
                break;
            case "numerals":
                transcriptionOptions.Numerals = bool.Parse(value);
                break;
            case "profanity_filter":
                transcriptionOptions.ProfanityFilter = bool.Parse(value);
                break;
            case "diarize":
                transcriptionOptions.Diarize = bool.Parse(value);
                break;
            case "summarize":
                transcriptionOptions.Summarize = "v2";
                break;
            case "detect_topics":
                transcriptionOptions.DetectTopics = bool.Parse(value);
                break;
            default:
                Console.WriteLine($"Feature {key} not recognized.");
                break;
        }
    }


    public static void Main(string[] args)
    {
        int port = 8080;
        FrontendServer server = new FrontendServer(port);
        server.Start();
        Console.WriteLine($"Server started on port {port}");
    }
}
