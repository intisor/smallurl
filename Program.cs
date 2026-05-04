using HashidsNet;
using Microsoft.EntityFrameworkCore;
using smallurl.Data;
using smallurl.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Ensure persistent directory exists BEFORE any DB operations
var dbDir = "/home/data";
if (!Directory.Exists(dbDir))
    Directory.CreateDirectory(dbDir);

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

app.MapGet("/{slug}", async (string slug, ApplicationDbContext db, IHashids hashids) =>
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

app.MapPost("/api/shorten", async (ShortenRequest request, ApplicationDbContext db, IHashids hashids, HttpContext ctx) =>
{
    if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var key) || key != apiKey)
        return Results.Unauthorized();

    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
        return Results.BadRequest(new { error = "Invalid URL" });

    if (!string.IsNullOrWhiteSpace(request.CustomSlug))
    {
        var exists = await db.Links.AnyAsync(l => l.CustomSlug == request.CustomSlug);
        if (exists)
            return Results.Conflict(new { error = "Custom slug already taken" });
    }

    var link = new Link
    {
        OriginalUrl = request.Url,
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
        originalUrl = link.OriginalUrl,
        label = link.Label,
        createdAt = link.CreatedAt
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
