using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using smallurl.Data;
using smallurl.Models;
using HashidsNet;
using System.Text.RegularExpressions;
using System.Net;
using System.Collections.Concurrent;

namespace smallurl.Services
{
    public class LinkProcessorService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHashids _hashids;
        private readonly AttributionService _attributionService;
        private readonly SearchService _searchService;
        
        // Regex to find capitalized phrases (Potential technical concepts)
        private static readonly Regex _conceptRegex = new Regex(@"\b[A-Z.#][a-zA-Z0-9+#.]*(?:\s+[A-Z][a-zA-Z0-9+#.]*)*\b", RegexOptions.Compiled);

        // Strict registry for very short acronyms to avoid collisions
        private static readonly HashSet<string> _strictShortAcronyms = new(StringComparer.OrdinalIgnoreCase)
        {
            "C#", "F#", ".NET", "AI", "ML", "SQL", "WPF", "UWP", "MAUI", "CLI", "API", "SDK"
        };

        // Common words to ignore when they appear capitalized
        private static readonly HashSet<string> _blacklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "The", "This", "That", "When", "How", "Why", "In", "On", "At", "From", "To", "With", "By", "For", "And", "Or", "But", "Wait", "Start", "End",
            "Here", "There", "Then", "If", "Else", "Are", "Is", "Was", "Were", "Will", "Would", "Should", "Could", "Can", "May", "Might", "Must", "Do",
            "Does", "Did", "Have", "Has", "Had", "Get", "Got", "Getting", "Make", "Making", "Take", "Taking", "See", "Seen", "Use", "Using"
        };

        public LinkProcessorService(
            ApplicationDbContext db, 
            IHashids hashids, 
            AttributionService attributionService,
            SearchService searchService)
        {
            _db = db;
            _hashids = hashids;
            _attributionService = attributionService;
            _searchService = searchService;
        }

        public async Task<string> ProcessBlogContentAsync(string content, string baseUrl)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            // 1. Process existing links (Apply attribution)
            var existingLinks = doc.DocumentNode.SelectNodes("//a[@href]");
            if (existingLinks != null)
            {
                foreach (var link in existingLinks)
                {
                    var originalUrl = link.GetAttributeValue("href", "");
                    if (string.IsNullOrWhiteSpace(originalUrl) || originalUrl.StartsWith("#") || originalUrl.StartsWith("/"))
                        continue;

                    var attributedUrl = _attributionService.ApplyAttribution(originalUrl);
                    link.SetAttributeValue("href", attributedUrl);
                }
            }

            // 2. Intelligent Auto-Linking for concepts
            await AutoLinkConceptsAsync(doc, baseUrl);

            return doc.DocumentNode.OuterHtml;
        }

        private async Task AutoLinkConceptsAsync(HtmlDocument doc, string baseUrl)
        {
            var textNodes = doc.DocumentNode.SelectNodes("//text()[not(ancestor::a) and not(ancestor::code) and not(ancestor::pre) and not(ancestor::h1) and not(ancestor::h2) and not(ancestor::h3)]");
            if (textNodes == null) return;

            var linkedConcepts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Step 1: Identify all potential candidates
            var allText = string.Join(" ", textNodes.Select(n => n.InnerText));
            var rawCandidates = _conceptRegex.Matches(allText)
                .Select(m => m.Value.Trim())
                .Distinct()
                .ToList();

            var candidatesToResolve = new List<string>();

            // Step 2: Smart Filtering (Acronyms, Word Boundaries)
            foreach (var candidate in rawCandidates)
            {
                // Smart stripping of possessives
                var cleanCandidate = candidate.EndsWith("'s") || candidate.EndsWith("’s") 
                    ? candidate.Substring(0, candidate.Length - 2) 
                    : candidate;

                if (_blacklist.Contains(cleanCandidate)) continue;
                
                // Short term strict check
                if (cleanCandidate.Length < 4 && !_strictShortAcronyms.Contains(cleanCandidate)) continue;

                candidatesToResolve.Add(cleanCandidate);
            }

            // Step 3: Parallel Discovery (Solves Cold Start)
            var resolvedMap = new ConcurrentDictionary<string, string>();
            var semaphore = new SemaphoreSlim(5); // Max 5 concurrent requests to avoid API rate limits
            
            var discoveryTasks = candidatesToResolve.Distinct().Select(async candidate => 
            {
                await semaphore.WaitAsync();
                try 
                {
                    var link = await ResolveConceptLinkAsync(candidate, baseUrl);
                    if (!string.IsNullOrEmpty(link))
                    {
                        resolvedMap.TryAdd(candidate, link);
                    }
                }
                finally 
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(discoveryTasks);

            // Step 4: Inject Links with Density Control (Solves Link Soup)
            const int MIN_GAP_CHARS = 100;
            int lastLinkEndIndex = -MIN_GAP_CHARS;

            foreach (var node in textNodes)
            {
                var text = node.InnerHtml;
                var nodeLength = text.Length;

                // Sort resolved candidates by length descending to match longest phrases first (e.g., "Azure Cosmos DB" before "Azure")
                var applicableCandidates = resolvedMap.Keys
                    .Where(k => text.Contains(k, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(k => k.Length)
                    .ToList();

                foreach (var candidate in applicableCandidates)
                {
                    if (linkedConcepts.Contains(candidate)) continue;

                    var index = text.IndexOf(candidate, StringComparison.OrdinalIgnoreCase);
                    
                    if (index >= 0)
                    {
                        // Density check: is this too close to the last link?
                        if (index - lastLinkEndIndex < MIN_GAP_CHARS && lastLinkEndIndex > 0)
                        {
                            continue; // Skip for now, maybe it will link in the next paragraph
                        }

                        // Word boundary check
                        bool startBoundary = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                        bool endBoundary = index + candidate.Length == text.Length || !char.IsLetterOrDigit(text[index + candidate.Length]);

                        if (startBoundary && endBoundary)
                        {
                            var link = resolvedMap[candidate];
                            var before = text.Substring(0, index);
                            var match = text.Substring(index, candidate.Length);
                            var after = text.Substring(index + candidate.Length);

                            var newNodeHtml = $"{before}<a href=\"{link}\" class=\"concept-link\" title=\"Microsoft Learn: {candidate}\">{match}</a>{after}";
                            var newNode = HtmlNode.CreateNode(newNodeHtml);
                            
                            node.ParentNode.ReplaceChild(newNode, node);
                            
                            linkedConcepts.Add(candidate);
                            lastLinkEndIndex = index + candidate.Length;
                            
                            // We modified the node, so we need to break out and process the next node
                            // (A more complex implementation would split the text node and continue processing, 
                            // but this guarantees max 1 link per node which also helps prevent density issues)
                            break; 
                        }
                    }
                }
            }
        }

        private async Task<string?> ResolveConceptLinkAsync(string term, string baseUrl)
        {
            // 1. Check local cache
            var concept = await _db.Concepts.FirstOrDefaultAsync(c => c.Name.ToLower() == term.ToLower());
            if (concept != null && concept.Confidence > 0.7)
            {
                // Unverified concepts (Confidence 0.5) won't be linked until manually approved
                if (concept.Confidence >= 0.9)
                {
                    concept.LastUpdated = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    return concept.ResolvedUrl;
                }
                return null;
            }

            // 2. Discover via SearchService
            var results = await _searchService.DiscoverAsync(term, baseUrl);
            var bestMatch = results.FirstOrDefault(r => r.Source == "Microsoft Learn");

            if (bestMatch != null)
            {
                // Strict Title Similarity Score (Solves Ambiguity)
                double similarity = CalculateSimilarity(term.ToLower(), bestMatch.Title.ToLower());
                
                // If it's a very exact match or highly similar, it's verified (0.95+)
                // If it's somewhat similar (e.g. contains the word), it's pending review (0.5)
                double confidence = similarity > 0.6 ? 0.95 : (bestMatch.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ? 0.5 : 0.0);

                if (confidence >= 0.5)
                {
                    if (concept == null)
                    {
                        concept = new Concept
                        {
                            Name = term,
                            ResolvedUrl = bestMatch.AttributedUrl,
                            Confidence = confidence,
                            LastUpdated = DateTime.UtcNow
                        };
                        _db.Concepts.Add(concept);
                    }
                    else
                    {
                        concept.ResolvedUrl = bestMatch.AttributedUrl;
                        concept.Confidence = confidence;
                        concept.LastUpdated = DateTime.UtcNow;
                    }

                    await _db.SaveChangesAsync();
                    
                    // Only return the link immediately if confidence is high. Otherwise, it requires human approval first.
                    return confidence >= 0.9 ? bestMatch.AttributedUrl : null;
                }
            }

            return null;
        }

        // Levenshtein Distance for strict similarity scoring
        private static double CalculateSimilarity(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0.0;
            if (source == target) return 1.0;

            var sourceLength = source.Length;
            var targetLength = target.Length;
            var matrix = new int[sourceLength + 1, targetLength + 1];

            for (var i = 0; i <= sourceLength; matrix[i, 0] = i++) { }
            for (var j = 0; j <= targetLength; matrix[0, j] = j++) { }

            for (var i = 1; i <= sourceLength; i++)
            {
                for (var j = 1; j <= targetLength; j++)
                {
                    var cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }
            
            int stepsToSame = matrix[sourceLength, targetLength];
            return 1.0 - ((double)stepsToSame / (double)Math.Max(sourceLength, targetLength));
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
    }
}
