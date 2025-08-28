using Friendly.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friendly.Infrastructure.Data.Configurations;

public class AlarmActionConfiguration : IEntityTypeConfiguration<AlarmAction>
{
    public void Configure(EntityTypeBuilder<AlarmAction> builder)
    {
        builder.ToTable("AlarmActions");
        
        builder.HasKey(aa => aa.Id);
        
        builder.Property(aa => aa.Type)
            .IsRequired()
            .HasConversion<int>();
            
        builder.Property(aa => aa.Message)
            .IsRequired()
            .HasMaxLength(75);
            
        builder.HasOne(aa => aa.Alarm)
            .WithMany(a => a.Actions)
            .HasForeignKey(aa => aa.AlarmId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
