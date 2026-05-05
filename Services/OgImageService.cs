using SkiaSharp;
using HtmlAgilityPack;
using System.Net.Http;

namespace smallurl.Services
{
    public class OgImageService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public OgImageService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<byte[]> GenerateOgImageAsync(string title, string date)
        {
            const int width = 1200;
            const int height = 630;

            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;

            // Colors from Intitech Brand
            var colorVoid = SKColor.Parse("#080705");
            var colorGold = SKColor.Parse("#a8873a");
            var colorEmber = SKColor.Parse("#c8612a");
            var colorScript = SKColor.Parse("#e8dcc8");
            var colorAsh = SKColor.Parse("#6b6355");
            var colorSediment = SKColor.Parse("#2e2b22");

            // 1. Background
            canvas.Clear(colorVoid);

            // 2. Subtle Gradient
            using var bgPaint = new SKPaint
            {
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    new SKPoint(width, height),
                    new[] { colorVoid, SKColor.Parse("#151310") },
                    null,
                    SKShaderTileMode.Clamp)
            };
            canvas.DrawRect(0, 0, width, height, bgPaint);

            // 3. Decorative Elements
            using var linePaint = new SKPaint
            {
                Color = colorSediment,
                StrokeWidth = 1,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };
            canvas.DrawLine(0, 0, 200, 200, linePaint);
            canvas.DrawLine(width, height, width - 200, height - 200, linePaint);

            // 4. Typefaces (Standard fallback fonts)
            using var tfBold = SKTypeface.FromFamilyName("Georgia", SKFontStyle.Bold);
            using var tfMono = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Normal);

            // 5. Branding (INTITECH)
            using var brandingFont = new SKFont(tfBold, 36);
            using var brandingPaint = new SKPaint { Color = colorGold, IsAntialias = true };
            canvas.DrawText("INTITECH // ENGINEERING ARCHIVES", 80, 100, brandingFont, brandingPaint);

            // 6. Title (Large, wrapped)
            using var titleFont = new SKFont(tfBold, 72);
            using var titlePaint = new SKPaint { Color = colorScript, IsAntialias = true };

            var wrappedTitle = WrapText(title, 1040, titleFont);
            float y = 280;
            foreach (var line in wrappedTitle)
            {
                canvas.DrawText(line, 80, y, titleFont, titlePaint);
                y += 100;
            }

            // 7. Date / Metadata
            using var dateFont = new SKFont(tfMono, 28);
            using var datePaint = new SKPaint { Color = colorAsh, IsAntialias = true };
            canvas.DrawText($"> PUBLISHED: {date.ToUpper()}", 80, height - 80, dateFont, datePaint);

            // 8. Accent line
            using var accentPaint = new SKPaint { Color = colorEmber, StrokeWidth = 6 };
            canvas.DrawLine(80, 130, 280, 130, accentPaint);

            // 9. Build Info
            canvas.DrawText("BUILD: 2.4.0-STABLE", width - 300, height - 80, dateFont, datePaint);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        public async Task<(string Title, string Date)> GetMetadataFromUrlAsync(string url)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "IntitechOgBot/1.0");
                var html = await client.GetStringAsync(url);

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var title = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")?.GetAttributeValue("content", "")
                            ?? doc.DocumentNode.SelectSingleNode("//title")?.InnerText
                            ?? "Intitech Engineering Blog";

                // Try to find a date in the URL or content
                var date = "Recently";
                var parts = url.Split('/');
                var lastPart = parts.LastOrDefault() ?? "";
                if (System.Text.RegularExpressions.Regex.IsMatch(lastPart, @"^\d{4}-\d{2}-\d{2}"))
                {
                    date = lastPart.Substring(0, 10);
                }

                return (title.Trim(), date.Trim());
            }
            catch
            {
                return ("Intitech Engineering", "Today");
            }
        }

        private List<string> WrapText(string text, float maxWidth, SKFont font)
        {
            var words = text.Split(' ');
            var lines = new List<string>();
            var currentLine = "";

            foreach (var word in words)
            {
                var testLine = string.IsNullOrWhiteSpace(currentLine) ? word : $"{currentLine} {word}";
                var width = font.MeasureText(testLine);
                if (width > maxWidth)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
            lines.Add(currentLine);
            return lines;
        }
    }
}
