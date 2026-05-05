using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using smallurl.Data;
using smallurl.Models;
using HashidsNet;

namespace smallurl.Services
{
    public class LinkProcessorService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHashids _hashids;
        
        private readonly string _contributorId = "studentamb_478453";

        private readonly List<string> _eligibleHosts = new()
        {
            "azure.microsoft.com",
            "developer.microsoft.com",
            "dotnet.microsoft.com",
            "learn.microsoft.com",
            "code.visualstudio.com",
            "devblogs.microsoft.com",
            "imaginecup.microsoft.com",
            "copilot.microsoft.com",
            "blog.fabric.microsoft.com",
            "community.fabric.microsoft.com",
            "powerbi.microsoft.com",
            "events.microsoft.com",
            "reactor.microsoft.com",
            "studentambassadors.microsoft.com",
            "techcommunity.microsoft.com",
            "community.powerplatform.com"
        };

        private readonly List<string> _eligibleMicrosoftComPaths = new()
        {
            "/microsoft-cloud/blog",
            "/startups",
            "/microsoft-365/copilot-learning-center",
            "/microsoft-copilot/for-individuals",
            "/microsoft-365-copilot",
            "/microsoft-fabric",
            "/power-platform",
            "/insidetrack"
        };

        public LinkProcessorService(ApplicationDbContext db, IHashids hashids)
        {
            _db = db;
            _hashids = hashids;
        }

        public async Task<string> ProcessBlogContentAsync(string content, string baseUrl)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            var links = doc.DocumentNode.SelectNodes("//a[@href]");
            if (links == null) return content;

            foreach (var link in links)
            {
                var originalUrl = link.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(originalUrl) || originalUrl.StartsWith("#") || originalUrl.StartsWith("/"))
                    continue;

                // 1. Apply MLSA Attribution if applicable (keeps the link long)
                var attributedUrl = ApplyAttribution(originalUrl);

                // 2. Update the link in the content with the attributed URL
                link.SetAttributeValue("href", attributedUrl);
            }

            return doc.DocumentNode.OuterHtml;
        }

        public async Task<string> GetOrCreateShortCodeAsync(string originalUrl)
        {
            var existingLink = await _db.Links.FirstOrDefaultAsync(l => l.OriginalUrl == originalUrl);
            if (existingLink != null)
            {
                return !string.IsNullOrEmpty(existingLink.CustomSlug) 
                    ? existingLink.CustomSlug 
                    : _hashids.Encode(existingLink.Id);
            }

            var newLink = new Link
            {
                OriginalUrl = originalUrl,
                Label = "Auto-generated from Blog",
                CreatedAt = DateTime.UtcNow
            };

            _db.Links.Add(newLink);
            await _db.SaveChangesAsync();

            return _hashids.Encode(newLink.Id);
        }

        public string ApplyAttribution(string url)
        {
            if (url.Contains("wt.mc_id=studentamb")) return url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;

            var host = uri.Host.ToLower();
            var path = uri.AbsolutePath;
            bool isEligible = false;

            // Check if it's an eligible host
            if (_eligibleHosts.Contains(host))
            {
                isEligible = true;
                
                // Special check for Learn Plans (not eligible)
                if (host == "learn.microsoft.com" && path.Contains("/training/plans/"))
                {
                    isEligible = false;
                }
            }
            // Check microsoft.com with specific paths
            else if (host == "microsoft.com" || host == "www.microsoft.com")
            {
                if (_eligibleMicrosoftComPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                {
                    isEligible = true;
                }
            }

            if (!isEligible) return url;

            var uriBuilder = new UriBuilder(url);

            // 1. Remove language-locale if present (e.g., /en-us/ -> /)
            // Regex matches /xx-xx/ or /xx-xx at the start of the path
            var localeRegex = new System.Text.RegularExpressions.Regex(@"^/([a-z]{2}-[a-z]{2})(/|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (localeRegex.IsMatch(uriBuilder.Path))
            {
                uriBuilder.Path = localeRegex.Replace(uriBuilder.Path, "/");
            }

            // 2. Add or overwrite the contributor ID
            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            query["wt.mc_id"] = _contributorId;
            uriBuilder.Query = query.ToString();

            // 3. Clean up default ports
            if ((uriBuilder.Scheme == "https" && uriBuilder.Port == 443) || 
                (uriBuilder.Scheme == "http" && uriBuilder.Port == 80))
            {
                uriBuilder.Port = -1;
            }

            return uriBuilder.ToString();
        }
    }
}
