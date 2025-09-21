using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using DoDiApp.Models;
using System.Linq;

namespace DoDiApp.Pages.Terms
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Term> Terms { get; set; } = new List<Term>();

        [BindProperty(SupportsGet = true)]
        public string? SearchString { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortOrder { get; set; }

        public async Task OnGetAsync()
        {
            var terms = from t in _context.Terms
                        select t;

            // Filter
            if (!string.IsNullOrEmpty(SearchString))
            {
                terms = terms.Where(t => t.Name.Contains(SearchString) ||
                                       t.Definition.Contains(SearchString) ||
                                       t.MainDomain.Contains(SearchString) ||
                                       (t.SubDomain != null && t.SubDomain.Contains(SearchString)));
            }

            // Sort
            terms = SortOrder switch
            {
                "name_desc" => terms.OrderByDescending(t => t.Name),
                "domain" => terms.OrderBy(t => t.MainDomain).ThenBy(t => t.SubDomain),
                "domain_desc" => terms.OrderByDescending(t => t.MainDomain).ThenByDescending(t => t.SubDomain),
                "approved" => terms.OrderBy(t => t.IsApproved),
                "approved_desc" => terms.OrderByDescending(t => t.IsApproved),
                "created" => terms.OrderBy(t => t.CreatedAt),
                "created_desc" => terms.OrderByDescending(t => t.CreatedAt),
                _ => terms.OrderBy(t => t.Name),
            };

            Terms = await terms.ToListAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var term = await _context.Terms.FindAsync(id);
            if (term != null && !term.IsApproved) // Only allow deletion of unapproved terms
            {
                _context.Terms.Remove(term);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}