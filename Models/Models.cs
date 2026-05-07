namespace smallurl.Models
{
    public class Link
    {
        public int Id { get; set; }
        public string OriginalUrl { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? CustomSlug { get; set; }
        public DateTime CreatedAt { get; set; }
        public ICollection<Click> Clicks { get; set; } = new List<Click>();
    }

    public class Click
    {
        public int Id { get; set; }
        public int LinkId { get; set; }
        public DateTime ClickedAt { get; set; }
        public Link Link { get; set; } = null!;
    }

    public class Concept
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ResolvedUrl { get; set; } = string.Empty;
        public string? SourceBlogUrl { get; set; }
        public double Confidence { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
