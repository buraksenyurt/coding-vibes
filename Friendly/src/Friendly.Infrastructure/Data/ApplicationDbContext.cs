using Friendly.Application.Common.Interfaces;
using Friendly.Domain.Entities;
using Friendly.Infrastructure.Data.Interceptors;
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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new DateTimeUtcInterceptor());
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
