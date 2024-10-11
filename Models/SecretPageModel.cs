using Microsoft.AspNetCore.Mvc.RazorPages;

namespace smallurl.Models
{
    public class SecretPageModel 
    {
        public string MsLink { get; set; }
        public string AppendedLink { get; set; }
        public string? ErrorMessage {  get; set; }
    }
}
