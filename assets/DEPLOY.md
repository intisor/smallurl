# i.intitech.dev — Deployment Guide

## Railway Setup (free tier)

### 1. Create project on Railway
- New Project → Deploy from GitHub repo
- Select this repo
- Railway auto-detects ASP.NET Core

### 2. Set environment variables in Railway dashboard

| Variable | Value |
|---|---|
| `Hashids__Salt` | A long random string — generate once, never change |
| `ApiKey` | Another long random string — your personal API key |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

Generate secure values:
```bash
# In terminal — run twice, use one for salt, one for API key
openssl rand -base64 32
```

### 3. Custom domain
- Railway dashboard → Settings → Custom Domain
- Add: `i.intitech.dev`
- Add CNAME record in Namecheap:
  - Host: `i`
  - Value: Railway-provided domain
  - TTL: Automatic

---

## Using the API

### Shorten a URL (protected)
```bash
curl -X POST https://i.intitech.dev/api/shorten \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -d '{
    "url": "https://blogs.intitech.dev/posts/2025-04-11-amsa-reporting",
    "label": "AMSA Reporting Post",
    "customSlug": "amsa"
  }'
```

Response:
```json
{
  "slug": "amsa",
  "shortUrl": "https://i.intitech.dev/amsa",
  "originalUrl": "https://blogs.intitech.dev/posts/2025-04-11-amsa-reporting",
  "label": "AMSA Reporting Post",
  "createdAt": "2025-04-11T..."
}
```

### Check stats (public)
```bash
curl https://i.intitech.dev/api/stats/amsa
```

Response:
```json
{
  "slug": "amsa",
  "originalUrl": "https://blogs.intitech.dev/posts/2025-04-11-amsa-reporting",
  "label": "AMSA Reporting Post",
  "createdAt": "2025-04-11T...",
  "clicks": 47,
  "lastClickedAt": "2025-04-12T..."
}
```

### All links (for portfolio dashboard)
```bash
curl https://i.intitech.dev/api/stats
```

---

## Planned short links

| Slug | Destination |
|---|---|
| `amsa` | AMSA reporting blog post |
| `fin` | FinSight blog post |
| `kasala` | Inti.Kasala project page |
| `blog` | blogs.intitech.dev |
| `gh` | GitHub profile |

---

## What happened to the Secret page?

The MLSA Microsoft link appender is preserved — just move it to a 
simple standalone HTML page at `intitech.dev/mlsa` with vanilla JS. 
No need for a server route for that feature.
