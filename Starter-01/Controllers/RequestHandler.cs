// RequestHandler.cs
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WebApp.Handlers
{
  public class RequestHandler
  {
      public async Task HandleRequest(HttpContext context)
    {
      var uri = context.Request.Path;
      if (uri == "/")
      {
          uri = "/index.html";
      }

      // Construct the file path within the "Static" folder
      var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Static", uri.Value.TrimStart('/'));

      if (File.Exists(filePath))
      {
          var mimeType = GetMimeTypeForFile(filePath);
          context.Response.ContentType = mimeType;
          await context.Response.SendFileAsync(filePath);
      }
      else
      {
          context.Response.StatusCode = StatusCodes.Status404NotFound;
          await context.Response.WriteAsync("File not found");
      }
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
  }
}