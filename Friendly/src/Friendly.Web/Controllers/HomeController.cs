using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Friendly.Web.Models;
using Friendly.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Friendly.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, IApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        // İstatistikleri hesapla
        var totalFriends = await _context.Persons.CountAsync();
        var totalAlarms = await _context.Alarms.CountAsync();
        var offendedFriends = await _context.Persons.CountAsync(p => p.IsOffended);
        var upcomingBirthdays = await _context.Persons
            .Where(p => p.BirthDate.HasValue && 
                       p.BirthDate.Value.Month == DateTime.Now.Month &&
                       p.BirthDate.Value.Day >= DateTime.Now.Day &&
                       p.BirthDate.Value.Day <= DateTime.Now.AddDays(30).Day)
            .CountAsync();

        ViewBag.TotalFriends = totalFriends;
        ViewBag.TotalAlarms = totalAlarms;
        ViewBag.OffendedFriends = offendedFriends;
        ViewBag.UpcomingBirthdays = upcomingBirthdays;

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
