using Friendly.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friendly.Infrastructure.Data.Configurations;

public class AlarmConfiguration : IEntityTypeConfiguration<Alarm>
{
    public void Configure(EntityTypeBuilder<Alarm> builder)
    {
        builder.ToTable("Alarms");
        
        builder.HasKey(a => a.Id);
        
        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(a => a.Description)
            .HasMaxLength(1000);
            
        builder.Property(a => a.Criteria)
            .IsRequired()
            .HasConversion<int>();
            
        builder.HasOne(a => a.Person)
            .WithMany(p => p.Alarms)
            .HasForeignKey(a => a.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasMany(a => a.Actions)
            .WithOne(ac => ac.Alarm)
            .HasForeignKey(ac => ac.AlarmId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
