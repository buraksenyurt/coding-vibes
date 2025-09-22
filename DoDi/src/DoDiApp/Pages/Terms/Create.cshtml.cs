using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DoDiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace DoDiApp.Pages.Terms
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Term Term { get; set; } = new Term();

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("Term.CreatedAt"); // Remove CreatedAt from validation

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Set default values
            Term.CreatedAt = DateTime.UtcNow;
            Term.UpdatedAt = DateTime.UtcNow;
            Term.CreatedBy = "admin"; // For now, hardcoded. In real app, get from user context
            Term.Version = 1;

            _context.Terms.Add(Term);
            await _context.SaveChangesAsync();

            return RedirectToPage("/Terms/Index");
        }
    }
}