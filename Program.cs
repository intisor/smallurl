using HashidsNet;
using Microsoft.EntityFrameworkCore;
using smallurl.Data;
using smallurl.Models;
using smallurl.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── Ensure database directory exists (if specified as a path)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=smallurl.db";
if (connectionString.Contains("/") || connectionString.Contains("\\"))
{
    var dataSource = connectionString.Split('=').Last().Trim();
    var dir = Path.GetDirectoryName(dataSource);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        Directory.CreateDirectory(dir);
}

// ── Services
builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=/home/data/smallurl.db"));

builder.Services.AddSingleton<IHashids>(_ =>
    new Hashids(
        builder.Configuration["Hashids:Salt"] 
            ?? throw new InvalidOperationException("Hashids:Salt not configured"),
        minHashLength: 5
    ));

builder.Services.AddMemoryCache();
builder.Services.AddScoped<LinkProcessorService>();
builder.Services.AddScoped<OgImageService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Portfolio", policy =>
        policy.WithOrigins(
            "https://intitech.dev",
            "https://blogs.intitech.dev",
            "http://localhost:3000"
        )
        .AllowAnyMethod()
        .AllowAnyHeader());
});

var app = builder.Build();

// ── Middleware
app.UseCors("Portfolio");
app.UseStaticFiles();
app.UseHttpsRedirection();

// ── DB init
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    // Create tables if they don't exist
    db.Database.EnsureCreated();

    // Set SQLite PRAGMAs on the now-existing database file
    var conn = db.Database.GetDbConnection();
    try
    {
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode = WAL;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA synchronous = NORMAL;";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
    }
    finally
    {
        try { conn.Close(); } catch { }
    }
}


// ────────────────────────────────────────────
// PUBLIC ENDPOINTS
// ────────────────────────────────────────────

app.MapGet("/", () => Results.Ok(new
{
    service = "i.intitech.dev",
    status = "operational",
    timestamp = DateTime.UtcNow
}));

app.MapGet("/{slug:regex(^[a-zA-Z0-9_-]+$)}", async (string slug, ApplicationDbContext db, IHashids hashids) =>
{
    if (string.IsNullOrWhiteSpace(slug))
        return Results.NotFound();

    var ids = hashids.Decode(slug);
    if (ids.Length == 0)
        return Results.NotFound();

    var link = await db.Links.FirstOrDefaultAsync(l => l.Id == ids[0]);
    if (link is null)
        return Results.NotFound();

    db.Clicks.Add(new Click
    {
        LinkId = link.Id,
        ClickedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    return Results.Redirect(link.OriginalUrl, permanent: true);
});

app.MapGet("/og/{slug}", async (string slug, ApplicationDbContext db, IHashids hashids, OgImageService ogService) =>
{
    if (string.IsNullOrWhiteSpace(slug))
        return Results.NotFound();

    var ids = hashids.Decode(slug);
    if (ids.Length == 0)
        return Results.NotFound();

    var link = await db.Links.FirstOrDefaultAsync(l => l.Id == ids[0]);
    if (link is null)
        return Results.NotFound();

    string title, date;
    
    if (link.OriginalUrl.Contains("blogs.intitech.dev/posts/"))
    {
        var meta = await ogService.GetMetadataFromUrlAsync(link.OriginalUrl);
        title = meta.Title;
        date = meta.Date;
    }
    else
    {
        title = link.Label;
        date = link.CreatedAt.ToString("MMM dd, yyyy");
    }

    var imageBytes = await ogService.GenerateOgImageAsync(title, date);
    return Results.Bytes(imageBytes, "image/png");
});

app.MapGet("/api/stats/{slug}", async (string slug, ApplicationDbContext db, IHashids hashids) =>
{
    var ids = hashids.Decode(slug);
    if (ids.Length == 0)
        return Results.NotFound();

    var link = await db.Links
        .Include(l => l.Clicks)
        .FirstOrDefaultAsync(l => l.Id == ids[0]);

    if (link is null)
        return Results.NotFound();

    return Results.Ok(new
    {
        slug,
        originalUrl = link.OriginalUrl,
        label = link.Label,
        createdAt = link.CreatedAt,
        clicks = link.Clicks.Count,
        lastClickedAt = link.Clicks.OrderByDescending(c => c.ClickedAt).FirstOrDefault()?.ClickedAt
    });
});

app.MapGet("/api/stats", async (ApplicationDbContext db, IHashids hashids) =>
{
    var links = await db.Links
        .Include(l => l.Clicks)
        .OrderByDescending(l => l.CreatedAt)
        .ToListAsync();

    return Results.Ok(links.Select(l => new
    {
        slug = hashids.Encode(l.Id),
        label = l.Label,
        originalUrl = l.OriginalUrl,
        createdAt = l.CreatedAt,
        clicks = l.Clicks.Count
    }));
});

// ────────────────────────────────────────────
// PROTECTED ENDPOINTS (API Key)
// ────────────────────────────────────────────

var apiKey = builder.Configuration["ApiKey"]
    ?? throw new InvalidOperationException("ApiKey not configured");

app.MapPost("/api/shorten", async (ShortenRequest request, ApplicationDbContext db, IHashids hashids, LinkProcessorService processor, HttpContext ctx) =>
{
    if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var key) || key != apiKey)
        return Results.Unauthorized();

    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
        return Results.BadRequest(new { error = "Invalid URL" });

    // Apply MS Attribution if applicable
    var targetUrl = processor.ApplyAttribution(request.Url);

    if (!string.IsNullOrWhiteSpace(request.CustomSlug))
    {
        var exists = await db.Links.AnyAsync(l => l.CustomSlug == request.CustomSlug);
        if (exists)
            return Results.Conflict(new { error = "Custom slug already taken" });
    }

    var link = new Link
    {
        OriginalUrl = targetUrl,
        Label = request.Label ?? request.Url,
        CustomSlug = request.CustomSlug,
        CreatedAt = DateTime.UtcNow
    };

    db.Links.Add(link);
    await db.SaveChangesAsync();

    var slug = string.IsNullOrWhiteSpace(request.CustomSlug)
        ? hashids.Encode(link.Id)
        : request.CustomSlug;

    return Results.Created($"/{slug}", new
    {
        slug,
        shortUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}/{slug}",
        originalUrl = targetUrl,
        label = link.Label,
        createdAt = link.CreatedAt
    });
});

app.MapGet("/api/search", async (string q, SearchService searchService, HttpContext ctx) =>
{
    if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var key) || key != apiKey)
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest(new { error = "Query is required" });

    var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    var results = await searchService.DiscoverAsync(q, baseUrl);

    foreach (var res in results)
    {
        if (!string.IsNullOrEmpty(res.ShortUrl) && !res.ShortUrl.StartsWith("http"))
        {
            res.ShortUrl = $"{baseUrl}/{res.ShortUrl}";
        }
    }

    return Results.Json(results, new JsonSerializerOptions(JsonSerializerDefaults.Web));
});

app.MapPost("/api/process-blog", async (ProcessBlogRequest request, LinkProcessorService processor, HttpContext ctx) =>
{
    if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var key) || key != apiKey)
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(request.Content))
        return Results.BadRequest(new { error = "Content is required" });

    var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    
    // 1. Process all links within the blog content
    var processedContent = await processor.ProcessBlogContentAsync(request.Content, baseUrl);

    // 2. Generate a master short link for the blog post itself if provided
    string? blogShortUrl = null;
    string? blogSlug = null;
    if (!string.IsNullOrWhiteSpace(request.BlogUrl))
    {
        blogSlug = await processor.GetOrCreateShortCodeAsync(request.BlogUrl);
        blogShortUrl = $"{baseUrl}/{blogSlug}";
    }

    return Results.Ok(new
    {
        processedContent,
        blogShortUrl,
        blogSlug,
        timestamp = DateTime.UtcNow
    });
});

app.MapDelete("/api/links/{slug}", async (string slug, ApplicationDbContext db, IHashids hashids, HttpContext ctx) =>
{
    if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var key) || key != apiKey)
        return Results.Unauthorized();

    var ids = hashids.Decode(slug);
    if (ids.Length == 0) return Results.NotFound();

    var link = await db.Links.FindAsync(ids[0]);
    if (link is null) return Results.NotFound();

    db.Links.Remove(link);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.Run();

record ShortenRequest(string Url, string? Label, string? CustomSlug);
record ProcessBlogRequest(string Content, string? BlogUrl);
