using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using smallurl.Data;
using smallurl.Models;
using HashidsNet;

using Microsoft.Extensions.Caching.Memory;

namespace smallurl.Services
{
    public class SearchService
    {
        private readonly HttpClient _httpClient;
        private readonly AttributionService _attributionService;
        private readonly ApplicationDbContext _db;
        private readonly IHashids _hashids;
        private readonly IMemoryCache _cache;
        private const string LearnSearchApi = "https://learn.microsoft.com/api/search";

        public SearchService(
            HttpClient httpClient, 
            AttributionService attributionService,
            ApplicationDbContext db,
            IHashids hashids,
            IMemoryCache cache)
        {
            _httpClient = httpClient;
            _attributionService = attributionService;
            _db = db;
            _hashids = hashids;
            _cache = cache;
        }

        public async Task<List<DiscoveryResult>> DiscoverAsync(string query, string baseUrl)
        {
            var cacheKey = $"discovery_{query.ToLower().Trim()}";
            if (_cache.TryGetValue(cacheKey, out List<DiscoveryResult>? cachedResults) && cachedResults != null)
            {
                return cachedResults;
            }

            var localResults = await SearchLocalAsync(query, baseUrl);
            var learnResults = await SearchMicrosoftLearnAsync(query);

            var results = localResults.Concat(learnResults)
                .GroupBy(r => r.AttributedUrl.ToLower().TrimEnd('/'))
                .Select(g => 
                {
                    var best = g.OrderBy(r => r.Source == "Local" ? 0 : 1).First();
                    var shortUrl = g.FirstOrDefault(r => !string.IsNullOrEmpty(r.ShortUrl))?.ShortUrl;
                    
                    var result = best with { ShortUrl = shortUrl };
                    result.LogoUrl = GetLogoForUrl(result.AttributedUrl);
                    return result;
                })
                .OrderBy(r => r.Source == "Local" ? 0 : 1)
                .ToList();

            _cache.Set(cacheKey, results, TimeSpan.FromMinutes(5));

            return results;
        }

        private string GetLogoForUrl(string url)
        {
            if (url.Contains("learn.microsoft.com")) return "https://learn.microsoft.com/favicon.ico";
            if (url.Contains("azure.microsoft.com")) return "https://azure.microsoft.com/favicon.ico";
            if (url.Contains("code.visualstudio.com")) return "https://code.visualstudio.com/favicon.ico";
            if (url.Contains("devblogs.microsoft.com")) return "https://devblogs.microsoft.com/favicon.ico";
            return "https://www.microsoft.com/favicon.ico";
        }

        private async Task<List<DiscoveryResult>> SearchLocalAsync(string query, string baseUrl)
        {
            try
            {
                var links = await _db.Links
                    .Where(l => l.Label.Contains(query) || l.OriginalUrl.Contains(query))
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                return links.Select(l => {
                    var slug = !string.IsNullOrEmpty(l.CustomSlug) ? l.CustomSlug : _hashids.Encode(l.Id);
                    return new DiscoveryResult
                    {
                        Title = l.Label,
                        Url = l.OriginalUrl,
                        AttributedUrl = l.OriginalUrl,
                        ShortUrl = slug,
                        Source = "Local",
                        Description = "Previously shortened link from your history."
                    };
                }).ToList();
            }
            catch
            {
                return new List<DiscoveryResult>();
            }
        }

        private async Task<List<DiscoveryResult>> SearchMicrosoftLearnAsync(string query, int count = 10)
        {
            var searchUrl = $"{LearnSearchApi}?search={Uri.EscapeDataString(query)}&locale=en-us&$top={count}";
            
            try
            {
                var response = await _httpClient.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode) return new List<DiscoveryResult>();

                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<LearnSearchResponse>(content);

                if (data?.Results == null) return new List<DiscoveryResult>();

                var results = new List<DiscoveryResult>();
                foreach (var r in data.Results)
                {
                    var attributedUrl = _attributionService.ApplyAttribution(r.Url);
                    
                    var existing = await _db.Links.FirstOrDefaultAsync(l => l.OriginalUrl == attributedUrl);
                    string? existingShortUrl = null;
                    if (existing != null)
                    {
                        existingShortUrl = !string.IsNullOrEmpty(existing.CustomSlug) ? existing.CustomSlug : _hashids.Encode(existing.Id);
                    }

                    results.Add(new DiscoveryResult
                    {
                        Title = r.Title,
                        Description = r.Description,
                        Url = r.Url,
                        AttributedUrl = attributedUrl,
                        ShortUrl = existingShortUrl,
                        Source = "Microsoft Learn",
                        Tags = new List<string> { "Documentation" }
                    });
                }
                return results;
            }
            catch
            {
                return new List<DiscoveryResult>();
            }
        }
    }

    public record DiscoveryResult
    {
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string AttributedUrl { get; init; } = string.Empty;
        public string? ShortUrl { get; set; }
        public string Source { get; init; } = "Unknown";
        public string? LogoUrl { get; set; }
        public List<string> Tags { get; init; } = new();
    }

    internal class LearnSearchResponse
    {
        [JsonPropertyName("results")]
        public List<LearnResult>? Results { get; set; }
    }

    internal class LearnResult
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }
}
