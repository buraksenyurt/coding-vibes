using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DoDiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace DoDiApp.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public int TotalTerms { get; set; }
    public int TotalDomains { get; set; }
    public IList<Term> RecentTerms { get; set; } = new List<Term>();

    public async Task OnGetAsync()
    {
        TotalTerms = await _context.Terms.CountAsync();
        TotalDomains = await _context.Terms.Select(t => t.MainDomain).Distinct().CountAsync();
        RecentTerms = await _context.Terms
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .ToListAsync();
    }
}
