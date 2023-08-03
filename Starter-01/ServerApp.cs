using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using YourProject.Handlers;
using DotNetEnv;

namespace YourProject
{
    public class ServerApp
    {
        private readonly int port;

        public ServerApp(int port)
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
                    app.Map("/api", apiApp => apiApp.Run(new ApiHandler().HandleApiRequest));
                    app.Run(new RequestHandler().HandleRequest);
                })
                .UseUrls($"http://localhost:{port}/")
                .Build();

            host.Run();
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            DotNetEnv.Env.Load();
            string portString = Environment.GetEnvironmentVariable("port");
            int.TryParse(portString, out int port);
            ServerApp server = new ServerApp(port);
            server.Start();
            Console.WriteLine($"Server started on port {port}");
        }
    }
}
