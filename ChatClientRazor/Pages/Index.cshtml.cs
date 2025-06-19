using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

public class IndexModel : PageModel
{
    [BindProperty]
    public string ServerUrl { get; set; } = string.Empty;
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    public void OnGet()
    {
    }
}
