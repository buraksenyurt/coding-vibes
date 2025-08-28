using Friendly.Application.Common.Interfaces;
using Friendly.Domain.Entities;
using Friendly.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Friendly.Web.Controllers;

public class DetailsController : Controller
{
    private readonly IApplicationDbContext _context;

    public DetailsController(IApplicationDbContext context)
    {
        _context = context;
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
        if (ModelState.IsValid)
        {
            detail.Id = Guid.NewGuid();
            
            _context.Details.Add(detail);
            await _context.SaveChangesAsync();
            
            return RedirectToAction("Details", "Persons", new { id = detail.PersonId });
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
            detail.UpdatedAt = DateTime.Now;
            
            _context.Details.Update(detail);
            await _context.SaveChangesAsync();
            
            return RedirectToAction("Details", "Persons", new { id = detail.PersonId });
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
