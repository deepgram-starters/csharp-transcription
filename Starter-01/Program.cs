using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class FrontendServer
{
    private readonly int port;

    public FrontendServer(int port)
    {
        this.port = port;
    }

    public void Start()
    {
        var host = new WebHostBuilder()
            .UseKestrel()
            .ConfigureServices(services => services.AddSingleton(this))
            .Configure(app => app.Run(HandleRequest))
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

    public static void Main(string[] args)
    {
        int port = 3000;
        FrontendServer server = new FrontendServer(port);
        server.Start();
        Console.WriteLine($"Server started on port {port}");
    }
}
