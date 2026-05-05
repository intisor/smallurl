using Microsoft.AspNetCore.Mvc.RazorPages;

namespace smallurl.Models
{
    public class SecretPageModel 
    {
        public string MsLink { get; set; } = string.Empty;
        public string AppendedLink { get; set; } = string.Empty;
        public string? ErrorMessage {  get; set; }
    }
}
