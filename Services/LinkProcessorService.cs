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
        private readonly string _contributorId = "wt.mc_id=studentamb_425455";
        
        private readonly List<string> _msDomains = new()
        {
            "azure.microsoft.com",
            "imaginecup.microsoft.com",
            "blog.fabric.microsoft.com",
            "learn.microsoft.com",
            "code.visualstudio.com",
            "community.fabric.microsoft.com",
            "microsoft.com/microsoft-cloud/blog",
            "microsoft.com/microsoft-fabric",
            "developer.microsoft.com",
            "microsoft.com/startups",
            "dotnet.microsoft.com",
            "events.microsoft.com",
            "foundershub.startups.microsoft.com",
            "techcommunity.microsoft.com",
            "mvp.microsoft.com",
            "reactor.microsoft.com"
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

                // 1. Apply MLSA Attribution if applicable
                var attributedUrl = ApplyAttribution(originalUrl);

                // 2. Shorten the URL
                var shortCode = await GetOrCreateShortCodeAsync(attributedUrl);
                var shortUrl = $"{baseUrl.TrimEnd('/')}/{shortCode}";

                // 3. Update the link in the content
                link.SetAttributeValue("href", shortUrl);
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

        private string ApplyAttribution(string url)
        {
            if (url.Contains("wt.mc_id=studentamb")) return url;

            bool isMsDomain = _msDomains.Any(d => url.Contains(d));
            if (!isMsDomain) return url;

            var uriBuilder = new UriBuilder(url);
            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            
            // Add or overwrite the contributor ID
            query["wt.mc_id"] = "studentamb_425455";
            uriBuilder.Query = query.ToString();

            return uriBuilder.ToString();
        }
    }
}
