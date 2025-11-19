using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
    private static async Task Main(string[] args)
    {
        // Build configuration from command-line arguments
        var configurationBuilder = new ConfigurationBuilder();

        configurationBuilder.AddCommandLine(args);
        var config = configurationBuilder.Build();

        var portNumber = config.GetValue<int>("port"); // port on which the caching proxy server will run.
        var url = config.GetValue<string>("url"); // URL of the resource to be fetched and cached.     
        bool clearCache = config.GetValue<bool>("clear-cache");

        Console.WriteLine($"Listening on port: {portNumber}");
        Console.WriteLine($"Fetching URL: {url}");
        Console.WriteLine($"Clear cache: {clearCache}");

        var builder = WebApplication.CreateBuilder(args);

        // Configure Kestrel web server to listen on the specified port
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(portNumber == 0 ? 5000 : portNumber);
        });

        builder.Services.AddMemoryCache();
        builder.Services.AddHttpClient();

        var app = builder.Build();

        var memoryCache = app.Services.GetRequiredService<IMemoryCache>();
        var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();

        if (!string.IsNullOrEmpty(url) && clearCache)
        {
            memoryCache.Remove(url);
            Console.WriteLine("Cache cleared");
        }

        app.MapGet("/", async (HttpContext context) =>
        {
            if (memoryCache.TryGetValue(key: url, value: out var cachedResponse))
            {
                // Add a custom header so clients can tell whether the response came from cache
                context.Response.Headers.Append("X-Cache", "HIT");
                return cachedResponse;
            }

            // HttpClient is .NET’s built-in HTTP client for sending HTTP requests and receiving HTTP responses.
            var httpClient = httpClientFactory.CreateClient();

            // Returns only the response body as a string.
            var response = await httpClient.GetStringAsync(url);

            // Add custom header to indicate cache miss
            context.Response.Headers.Append("X-Cache", "MISS");
            memoryCache.Set(url, response);

            return response;
        });

        // starts the web server and begins listening for HTTP requests.
        // Any code after app.Run() won’t execute until the server stops
        app.Run();
    }
}