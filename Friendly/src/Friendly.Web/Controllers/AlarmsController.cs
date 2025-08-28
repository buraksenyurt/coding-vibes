using Friendly.Application.Common.Interfaces;
using Friendly.Domain.Entities;
using Friendly.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Friendly.Web.Controllers;

public class AlarmsController : Controller
{
    private readonly IApplicationDbContext _context;

    public AlarmsController(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var alarms = await _context.Alarms
            .Include(a => a.Person)
            .Include(a => a.Actions)
            .OrderBy(a => a.TriggerDate)
            .ToListAsync();
        
        return View(alarms);
    }

    public async Task<IActionResult> Create()
    {
        var persons = await _context.Persons
            .Where(p => !p.IsOffended)
            .OrderBy(p => p.FirstName)
            .ThenBy(p => p.LastName)
            .ToListAsync();

        ViewBag.PersonId = new SelectList(persons, "Id", "FullName");
        ViewBag.AlarmCriteria = new SelectList(Enum.GetValues<AlarmCriteria>().Select(x => new { 
            Value = (int)x, 
            Text = GetAlarmCriteriaText(x) 
        }), "Value", "Text");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Alarm alarm)
    {
        if (ModelState.IsValid)
        {
            alarm.Id = Guid.NewGuid();
            
            _context.Alarms.Add(alarm);
            await _context.SaveChangesAsync();
            
            return RedirectToAction(nameof(Index));
        }

        // Reload dropdown data if validation fails
        var persons = await _context.Persons
            .Where(p => !p.IsOffended)
            .OrderBy(p => p.FirstName)
            .ThenBy(p => p.LastName)
            .ToListAsync();

        ViewBag.PersonId = new SelectList(persons, "Id", "FullName", alarm.PersonId);
        ViewBag.AlarmCriteria = new SelectList(Enum.GetValues<AlarmCriteria>().Select(x => new { 
            Value = (int)x, 
            Text = GetAlarmCriteriaText(x) 
        }), "Value", "Text", alarm.Criteria);

        return View(alarm);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var alarm = await _context.Alarms
            .Include(a => a.Person)
            .Include(a => a.Actions)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (alarm == null)
        {
            return NotFound();
        }

        return View(alarm);
    }

    public async Task<IActionResult> Edit(Guid id)
    {
        var alarm = await _context.Alarms
            .FirstOrDefaultAsync(a => a.Id == id);

        if (alarm == null)
        {
            return NotFound();
        }

        var persons = await _context.Persons
            .Where(p => !p.IsOffended)
            .OrderBy(p => p.FirstName)
            .ThenBy(p => p.LastName)
            .ToListAsync();

        ViewBag.PersonId = new SelectList(persons, "Id", "FullName", alarm.PersonId);
        ViewBag.AlarmCriteria = new SelectList(Enum.GetValues<AlarmCriteria>().Select(x => new { 
            Value = (int)x, 
            Text = GetAlarmCriteriaText(x) 
        }), "Value", "Text", alarm.Criteria);

        return View(alarm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Alarm alarm)
    {
        if (id != alarm.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            alarm.UpdatedAt = DateTime.Now;
            
            _context.Alarms.Update(alarm);
            await _context.SaveChangesAsync();
            
            return RedirectToAction(nameof(Index));
        }

        // Reload dropdown data if validation fails
        var persons = await _context.Persons
            .Where(p => !p.IsOffended)
            .OrderBy(p => p.FirstName)
            .ThenBy(p => p.LastName)
            .ToListAsync();

        ViewBag.PersonId = new SelectList(persons, "Id", "FullName", alarm.PersonId);
        ViewBag.AlarmCriteria = new SelectList(Enum.GetValues<AlarmCriteria>().Select(x => new { 
            Value = (int)x, 
            Text = GetAlarmCriteriaText(x) 
        }), "Value", "Text", alarm.Criteria);

        return View(alarm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var alarm = await _context.Alarms
            .FirstOrDefaultAsync(a => a.Id == id);

        if (alarm != null)
        {
            _context.Alarms.Remove(alarm);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private static string GetAlarmCriteriaText(AlarmCriteria criteria)
    {
        return criteria switch
        {
            AlarmCriteria.Birthday => "Doğum Günü",
            AlarmCriteria.Appointment => "Randevu",
            AlarmCriteria.Memo => "Hatırlatma",
            _ => criteria.ToString()
        };
    }
}
