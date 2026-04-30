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
}
