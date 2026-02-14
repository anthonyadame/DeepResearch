using DeepResearchAgent.Services;
using DeepResearchAgent.Models;

namespace DeepResearchAgent.Examples;

/// <summary>
/// Example usage of SearCrawl4AI service
/// </summary>
public class SearCrawl4AIExample
{
    public static async Task RunExamples()
    {
        Console.WriteLine("=== SearCrawl4AI Service Examples ===\n");

        // Initialize the service
        var httpClient = new HttpClient();
        var service = new SearCrawl4AIService(
            httpClient,
            searxngBaseUrl: Environment.GetEnvironmentVariable("SEARXNG_BASE_URL") ?? "http://localhost:8080",
            crawl4aiBaseUrl: Environment.GetEnvironmentVariable("CRAWL4AI_BASE_URL") ?? "http://localhost:8000",
            logger: new ConsoleLogger()
        );

        await Example1_SimpleSearch(service);
        await Example2_ScrapeUrls(service);
        await Example3_SearchAndScrape(service);
        await Example4_AdvancedScraping(service);
    }

    /// <summary>
    /// Example 1: Simple web search using SearXNG
    /// </summary>
    static async Task Example1_SimpleSearch(ISearCrawl4AIService service)
    {
        Console.WriteLine("\n--- Example 1: Simple Web Search ---");
        
        try
        {
            var results = await service.SearchAsync("artificial intelligence trends 2024", maxResults: 5);
            
            Console.WriteLine($"Found {results.NumberOfResults} results for '{results.Query}':\n");
            
            foreach (var result in results.Results)
            {
                Console.WriteLine($"Title: {result.Title}");
                Console.WriteLine($"URL: {result.Url}");
                Console.WriteLine($"Snippet: {result.Content}");
                Console.WriteLine($"Source: {result.Engine}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Example 2: Scrape specific URLs
    /// </summary>
    static async Task Example2_ScrapeUrls(ISearCrawl4AIService service)
    {
        Console.WriteLine("\n--- Example 2: Scrape Specific URLs ---");
        
        var urls = new List<string>
        {
            "https://en.wikipedia.org/wiki/Artificial_intelligence",
            "https://en.wikipedia.org/wiki/Machine_learning"
        };

        try
        {
            var scrapeResponse = await service.ScrapeAsync(urls);
            
            if (scrapeResponse.Success)
            {
                Console.WriteLine($"Successfully scraped {scrapeResponse.Results.Count} URLs:\n");
                
                foreach (var content in scrapeResponse.Results)
                {
                    if (content.Success)
                    {
                        Console.WriteLine($"URL: {content.Url}");
                        Console.WriteLine($"Content length: {content.Markdown.Length} characters");
                        Console.WriteLine($"Preview: {content.Markdown.Substring(0, Math.Min(200, content.Markdown.Length))}...");
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.WriteLine($"Failed to scrape {content.Url}: {content.ErrorMessage}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Scraping failed: {scrapeResponse.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Example 3: Combined search and scrape (most powerful)
    /// </summary>
    static async Task Example3_SearchAndScrape(ISearCrawl4AIService service)
    {
        Console.WriteLine("\n--- Example 3: Search and Scrape ---");
        
        try
        {
            var results = await service.SearchAndScrapeAsync(
                query: "transformer architecture deep learning",
                maxResults: 3
            );
            
            Console.WriteLine($"Deep search completed. Scraped {results.Count} pages:\n");
            
            foreach (var content in results)
            {
                Console.WriteLine($"Source: {content.Url}");
                Console.WriteLine($"Content length: {content.Markdown.Length} characters");
                
                // Extract first heading or first 100 chars
                var preview = content.Markdown.Length > 100 
                    ? content.Markdown.Substring(0, 100) + "..." 
                    : content.Markdown;
                    
                Console.WriteLine($"Preview:\n{preview}\n");
                Console.WriteLine("---\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Example 4: Advanced scraping with custom options
    /// </summary>
    static async Task Example4_AdvancedScraping(ISearCrawl4AIService service)
    {
        Console.WriteLine("\n--- Example 4: Advanced Scraping Options ---");
        
        var urls = new List<string>
        {
            "https://arxiv.org/abs/1706.03762"  // Attention Is All You Need paper
        };

        var options = new Crawl4AIRequest
        {
            Urls = urls,
            ExtractionStrategy = "CosineStrategy",
            ChunkingStrategy = "RegexChunking",
            WordCountThreshold = "50",
            Bypass_cache = true,
            Verbose = true
        };

        try
        {
            var scrapeResponse = await service.ScrapeAsync(urls, options);
            
            if (scrapeResponse.Success && scrapeResponse.Results.Any())
            {
                var content = scrapeResponse.Results.First();
                
                Console.WriteLine($"Scraped: {content.Url}");
                Console.WriteLine($"Success: {content.Success}");
                Console.WriteLine($"Markdown length: {content.Markdown.Length}");
                Console.WriteLine($"Cleaned HTML length: {content.CleanedHtml.Length}");
                Console.WriteLine($"Metadata entries: {content.Metadata.Count}");
                
                if (content.Metadata.Any())
                {
                    Console.WriteLine("\nMetadata:");
                    foreach (var kvp in content.Metadata.Take(5))
                    {
                        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Simple console logger implementation
    /// </summary>
    class ConsoleLogger : ILogger
    {
        public void LogInformation(string message, params object[] args)
        {
            Console.WriteLine($"[INFO] {string.Format(message, args)}");
        }

        public void LogWarning(string message, params object[] args)
        {
            Console.WriteLine($"[WARN] {string.Format(message, args)}");
        }

        public void LogError(Exception? exception, string message, params object[] args)
        {
            Console.WriteLine($"[ERROR] {string.Format(message, args)}");
            if (exception != null)
            {
                Console.WriteLine($"  Exception: {exception.Message}");
            }
        }
    }
}
