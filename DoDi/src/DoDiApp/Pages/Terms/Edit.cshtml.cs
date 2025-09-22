using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DoDiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace DoDiApp.Pages.Terms
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Term Term { get; set; } = new Term();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var term = await _context.Terms.FindAsync(id);
            if (term == null)
            {
                return NotFound();
            }

            Term = term;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine("OnPostAsync called");
            Console.WriteLine($"Term ID: {Term.Id}, Name: {Term.Name}");
            ModelState.Remove("Term.CreatedBy"); // Remove CreatedBy from validation
            
            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState is invalid");
                return Page();
            }

            var term = await _context.Terms.FindAsync(Term.Id);
            if (term == null)
            {
                return NotFound();
            }

            // Only allow editing unapproved terms
            if (term.IsApproved)
            {
                ModelState.AddModelError("", "Approved terms cannot be edited.");
                return Page();
            }

            // Update fields
            term.Name = Term.Name;
            term.Definition = Term.Definition;
            term.MainDomain = Term.MainDomain;
            term.SubDomain = Term.SubDomain;
            term.UpdatedAt = DateTime.UtcNow;
            term.Version++;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TermExists(Term.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("/Terms/Index");
        }

        private bool TermExists(int id)
        {
            return _context.Terms.Any(e => e.Id == id);
        }
    }
}