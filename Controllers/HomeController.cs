using HashidsNet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using smallurl.Data;
using smallurl.Models;
using System.Diagnostics;

namespace smallurl.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _applicationDbContext;

        public HomeController(ILogger<HomeController> logger,ApplicationDbContext applicationDbContext)
        {
            _logger = logger;
            _applicationDbContext = applicationDbContext;   
        }

        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Shorten(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                return BadRequest("Invalid URL");
            }

            var urlMapping = new UrlMapping { 
                OriginalUrl = url,
                CreatedDate = DateTime.Now  
            };

            _applicationDbContext.UrlMappings.Add(urlMapping);
            await _applicationDbContext.SaveChangesAsync();

            var hashids = new Hashids("my salt",5);
            var shortCode = hashids.Encode(urlMapping.Id);
            urlMapping.ShortCode = shortCode;
            await _applicationDbContext.SaveChangesAsync();


            ViewBag.ShortUrl = $"{Request.Scheme}://{Request.Host}/{shortCode}";
            return View("Index");
        }

        [HttpGet("{shortCode}")]
        public async Task<IActionResult> RedirectToUrl(string shortCode)
        {
            if (string.IsNullOrEmpty(shortCode))
            {
                return NotFound();
            }
            var hashids = new Hashids("my salt",5);
            var ids = hashids.Decode(shortCode);

            if (ids.Length == 0)
            {
                return NotFound();
            }

            var urlMapping = await _applicationDbContext.UrlMappings.FirstOrDefaultAsync(u => u.ShortCode == shortCode);

            if (urlMapping == null)
            {
                return NotFound();
            }
            //if (urlMapping.OriginalUrl.Contains(shortCode))
            //{
            //    return await Redirect(urlMapping.OriginalUrl);
            //}

            //return await Redirect($"{Request.Scheme}://{Request.Host}/{shortCode}");
            return new RedirectResult(urlMapping.OriginalUrl, true);
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Secret()
        {
            return View(new SecretPageModel());
        }

        [HttpPost]
        public IActionResult Secret(SecretPageModel model)
        {
            const string ContributorId = "?wt.mc_id=studentamb_425455" ;
           
            if (!string.IsNullOrEmpty(model.MsLink))
            {
                if (CheckUrl(model.MsLink))
                {
                    if (model.MsLink.Contains("wt.mc_id=studentamb"))
                    {
                        model.AppendedLink = $"{model.MsLink}";
                    }
                    else
                    {
                        model.AppendedLink = $"{model.MsLink}{ContributorId}";
                    }
                }
                else
                {
                   model.ErrorMessage = "The provided URL is not valid.";
                    return View(model);
                }
            }
            return View(model);
        }

        private bool CheckUrl(string url)
        {
            List<string> domains = new()
            {
                "azure.microsoft.com",
                "imaginecup.microsoft.com",
                "blog.fabric.microsoft.com",
                "learn.microsoft.com",
                "code.visualstudio.com",
                "learn.microsoft.com/copilot",
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
            foreach (var domain in domains)
            {
                if (url.Contains(domain))
                {
                    return true;
                }
            }
            return false;
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
