using System.Text.RegularExpressions;
using System.Web;

namespace smallurl.Services
{
    public class AttributionService
    {
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
            "community.powerplatform.com",
            "visualstudio.microsoft.com",
            "azure.com",
            "office.com",
            "microsoft365.com",
            "social.technet.microsoft.com",
            "social.msdn.microsoft.com"
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
            "/power-automate",
            "/power-apps",
            "/power-bi",
            "/power-pages",
            "/insidetrack",
            "/training"
        };

        public string ApplyAttribution(string url)
        {
            if (url.Contains("wt.mc_id=studentamb")) return url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;

            var host = uri.Host.ToLower();
            var path = uri.AbsolutePath;
            bool isEligible = false;

            if (_eligibleHosts.Contains(host))
            {
                isEligible = true;
                if (host == "learn.microsoft.com" && path.Contains("/training/plans/"))
                {
                    isEligible = false;
                }
            }
            else if (host == "microsoft.com" || host == "www.microsoft.com")
            {
                if (_eligibleMicrosoftComPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                {
                    isEligible = true;
                }
            }

            if (!isEligible) return url;

            var uriBuilder = new UriBuilder(url);

            var localeRegex = new Regex(@"^/([a-z]{2}-[a-z]{2})(/|$)", RegexOptions.IgnoreCase);
            if (localeRegex.IsMatch(uriBuilder.Path))
            {
                uriBuilder.Path = localeRegex.Replace(uriBuilder.Path, "/");
            }

            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["wt.mc_id"] = _contributorId;
            uriBuilder.Query = query.ToString();

            if ((uriBuilder.Scheme == "https" && uriBuilder.Port == 443) || 
                (uriBuilder.Scheme == "http" && uriBuilder.Port == 80))
            {
                uriBuilder.Port = -1;
            }

            return uriBuilder.ToString();
        }
    }
}
