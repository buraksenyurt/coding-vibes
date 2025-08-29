using Friendly.Application.Common.Interfaces;
using Friendly.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Friendly.Web.Controllers;

public class PersonsController : Controller
{
    private readonly IApplicationDbContext _context;

    public PersonsController(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var persons = await _context.Persons
            .OrderBy(p => p.FirstName)
            .ThenBy(p => p.LastName)
            .ToListAsync();
        
        return View(persons);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Person person)
    {
        if (ModelState.IsValid)
        {
            person.Id = Guid.NewGuid();
            // CreatedAt is automatically set in BaseEntity
            
            _context.Persons.Add(person);
            await _context.SaveChangesAsync();
            
            return RedirectToAction(nameof(Index));
        }
        
        return View(person);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var person = await _context.Persons
            .AsSplitQuery()
            .Include(p => p.Details)
            .Include(p => p.Alarms)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person == null)
        {
            return NotFound();
        }

        return View(person);
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var person = await _context.Persons
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person == null)
        {
            return NotFound();
        }

        return View(person);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Person person)
    {
        if (id != person.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            person.UpdatedAt = DateTime.Now;
            
            _context.Persons.Update(person);
            await _context.SaveChangesAsync();
            
            return RedirectToAction(nameof(Index));
        }
        
        return View(person);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var person = await _context.Persons
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person != null)
        {
            _context.Persons.Remove(person);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Offended()
    {
        var offendedPersons = await _context.Persons
            .Where(p => p.IsOffended)
            .OrderBy(p => p.FirstName)
            .ThenBy(p => p.LastName)
            .ToListAsync();
        
        return View(offendedPersons);
    }
}
