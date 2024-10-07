namespace smallurl.Models
{
    public class UrlMapping
    {
        public int Id { get; set; }
        public string OriginalUrl { get; set; }
        public string? ShortCode { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
