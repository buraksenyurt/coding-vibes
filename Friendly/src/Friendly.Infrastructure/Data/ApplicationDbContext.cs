using Friendly.Application.Common.Interfaces;
using Friendly.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Friendly.Infrastructure.Data;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Detail> Details => Set<Detail>();
    public DbSet<Alarm> Alarms => Set<Alarm>();
    public DbSet<AlarmAction> AlarmActions => Set<AlarmAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
