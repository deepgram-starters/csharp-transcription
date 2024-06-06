using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Deepgram;
using DotNetEnv;
using Deepgram.Transcription;
using WebApp.Models;
using WebApp.Handlers;

namespace WebApp.Handlers
{
  public class ApiHandler
  {
      public async Task HandleApiRequest(HttpContext context)
      {
      if (context.Request.Method != "POST")
      {
          context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
          await context.Response.WriteAsync("Method not allowed. Only POST requests are accepted.");
          return;
      }

      if (context.Request.HasFormContentType)
      {
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

          // Set the features if they were provided by traversing the dictionary
          if (featuresData != null)
          {
              foreach (var feature in featuresData)
              {
                  var key = feature.Key.ToString();
                  var value = feature.Value.ToString();
                  TranscriptionOptions.SetFeatures(transcriptionOptions, key, value);
              }
          }

          var deepgramResponseJson = null as PrerecordedTranscription;

          if (file != null && file.Length > 0)
          {
              string fileName = file.FileName;
              string mimeType = file.ContentType;
              
              try
              {   
                  using (FileStream stream = File.Create(fileName))
                  {
                      await file.CopyToAsync(stream);
                  }
                  using (FileStream stream = File.OpenRead(fileName))
                  {
                      var deepgramResponse = await deepgram.Transcription.Prerecorded.GetTranscriptionAsync(
                          new StreamSource(stream, mimeType), transcriptionOptions);
                      deepgramResponseJson =  deepgramResponse;
                  }
              }
              catch (Exception ex)
              {
                  Console.WriteLine($"An error occurred while processing the file: {ex.Message}");
              }
              // Delete the file after it has been processed
              if (File.Exists(fileName)){
              File.Delete(fileName);
              }
          } else {
              try{
                  var deepgramResponse = await deepgram.Transcription.Prerecorded.GetTranscriptionAsync(
                      new Deepgram.Transcription.UrlSource(url),
                  transcriptionOptions);
                  deepgramResponseJson =  deepgramResponse;
              } catch (Exception ex)
              {
                Console.WriteLine($"An error occurred while processing the audio: {ex.Message}");
              }
          }

          var options = new JsonSerializerOptions
          {
              PropertyNamingPolicy = JsonNamingPolicy.CamelCase // Use CamelCase naming convention
          };

          var responseObject = new
          {
              model = model,
              tier = tier,
              version = "1.0",
              dgFeatures = featuresData,
              transcription = deepgramResponseJson,
          };

          context.Response.ContentType = "application/json";
          // allow CORS
          context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
          context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
          context.Response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
          Console.WriteLine($"Sending response: {JsonSerializer.Serialize(responseObject)}");
          

          string responseJson = JsonSerializer.Serialize(responseObject, options);
          byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
          await context.Response.Body.WriteAsync(responseBytes, 0, responseBytes.Length);
          }
      else
      {
          context.Response.StatusCode = StatusCodes.Status400BadRequest;
          await context.Response.WriteAsync("Invalid request format. Only form data is accepted.");
      }
    }
  }
}