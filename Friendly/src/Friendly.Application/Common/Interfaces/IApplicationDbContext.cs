using Friendly.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Friendly.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Person> Persons { get; }
    DbSet<Detail> Details { get; }
    DbSet<Alarm> Alarms { get; }
    DbSet<AlarmAction> AlarmActions { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
