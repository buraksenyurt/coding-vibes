using Friendly.Application.Common.Interfaces;
using Friendly.Domain.Entities;
using Friendly.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Friendly.Web.Controllers;

public class DetailsController : Controller
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<DetailsController> _logger;

    public DetailsController(IApplicationDbContext context, ILogger<DetailsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Create(Guid personId)
    {
        var person = await _context.Persons
            .FirstOrDefaultAsync(p => p.Id == personId);

        if (person == null)
        {
            return NotFound();
        }

        ViewBag.Person = person;
        ViewBag.ContactType = new SelectList(Enum.GetValues<ContactType>().Select(x => new { 
            Value = (int)x, 
            Text = GetContactTypeText(x) 
        }), "Value", "Text");

        return View(new Detail { PersonId = personId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Detail detail)
    {
        // Navigation property'i model binding'den temizle
        ModelState.Remove("Person");
        
        _logger.LogInformation("Creating detail for PersonId: {PersonId}, Type: {Type}, Value: {Value}", 
            detail.PersonId, detail.Type, detail.Value);

        if (ModelState.IsValid)
        {
            // Verify that the Person exists
            var personExists = await _context.Persons
                .AnyAsync(p => p.Id == detail.PersonId);
            
            if (!personExists)
            {
                _logger.LogError("Person with ID {PersonId} not found", detail.PersonId);
                ModelState.AddModelError("", "Belirtilen kişi bulunamadı.");
                var personForError = await _context.Persons
                    .FirstOrDefaultAsync(p => p.Id == detail.PersonId);
                ViewBag.Person = personForError;
                ViewBag.ContactType = new SelectList(Enum.GetValues<ContactType>().Select(x => new { 
                    Value = (int)x, 
                    Text = GetContactTypeText(x) 
                }), "Value", "Text", detail.Type);
                return View(detail);
            }
            
            detail.Id = Guid.NewGuid();
            detail.CreatedAt = DateTime.UtcNow;
            
            _logger.LogInformation("Adding detail to context: {DetailId}", detail.Id);
            _context.Details.Add(detail);
            
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Detail saved successfully: {DetailId}", detail.Id);
                
                return RedirectToAction("Details", "Persons", new { id = detail.PersonId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving detail for PersonId: {PersonId}", detail.PersonId);
                ModelState.AddModelError("", "İletişim bilgisi kaydedilirken bir hata oluştu.");
            }
        }
        else
        {
            _logger.LogWarning("ModelState is not valid for PersonId: {PersonId}. Errors: {Errors}", 
                detail.PersonId, 
                string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
        }

        var person = await _context.Persons
            .FirstOrDefaultAsync(p => p.Id == detail.PersonId);

        ViewBag.Person = person;
        ViewBag.ContactType = new SelectList(Enum.GetValues<ContactType>().Select(x => new { 
            Value = (int)x, 
            Text = GetContactTypeText(x) 
        }), "Value", "Text", detail.Type);

        return View(detail);
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var detail = await _context.Details
            .Include(d => d.Person)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (detail == null)
        {
            return NotFound();
        }

        ViewBag.Person = detail.Person;
        ViewBag.ContactType = new SelectList(Enum.GetValues<ContactType>().Select(x => new { 
            Value = (int)x, 
            Text = GetContactTypeText(x) 
        }), "Value", "Text", detail.Type);

        return View(detail);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Detail detail)
    {
        if (id != detail.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                detail.UpdatedAt = DateTime.UtcNow;
                
                _context.Details.Update(detail);
                await _context.SaveChangesAsync();
                
                return RedirectToAction("Details", "Persons", new { id = detail.PersonId });
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "İletişim bilgisi güncellenirken bir hata oluştu.");
            }
        }

        var person = await _context.Persons
            .FirstOrDefaultAsync(p => p.Id == detail.PersonId);

        ViewBag.Person = person;
        ViewBag.ContactType = new SelectList(Enum.GetValues<ContactType>().Select(x => new { 
            Value = (int)x, 
            Text = GetContactTypeText(x) 
        }), "Value", "Text", detail.Type);

        return View(detail);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var detail = await _context.Details
            .FirstOrDefaultAsync(d => d.Id == id);

        if (detail != null)
        {
            var personId = detail.PersonId;
            _context.Details.Remove(detail);
            await _context.SaveChangesAsync();
            
            return RedirectToAction("Details", "Persons", new { id = personId });
        }

        return NotFound();
    }

    private static string GetContactTypeText(ContactType type)
    {
        return type switch
        {
            ContactType.Phone => "Telefon",
            ContactType.Email => "E-posta",
            ContactType.Address => "Adres",
            ContactType.SocialMedia => "Sosyal Medya",
            _ => type.ToString()
        };
    }
}
