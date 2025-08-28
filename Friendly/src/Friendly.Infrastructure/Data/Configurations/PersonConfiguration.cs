using Friendly.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friendly.Infrastructure.Data.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("Persons");
        
        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.FirstName)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(p => p.LastName)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(p => p.About)
            .HasMaxLength(500);
            
        builder.Property(p => p.Notes)
            .HasMaxLength(1000);
            
        builder.Property(p => p.OffendedReason)
            .HasMaxLength(500);
            
        builder.HasMany(p => p.Details)
            .WithOne(d => d.Person)
            .HasForeignKey(d => d.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasMany(p => p.Alarms)
            .WithOne(a => a.Person)
            .HasForeignKey(a => a.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
