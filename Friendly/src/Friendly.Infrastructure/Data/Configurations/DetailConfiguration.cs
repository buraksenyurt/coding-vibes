using Friendly.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friendly.Infrastructure.Data.Configurations;

public class DetailConfiguration : IEntityTypeConfiguration<Detail>
{
    public void Configure(EntityTypeBuilder<Detail> builder)
    {
        builder.ToTable("Details");
        
        builder.HasKey(d => d.Id);
        
        builder.Property(d => d.Type)
            .IsRequired()
            .HasConversion<int>();
            
        builder.Property(d => d.Value)
            .IsRequired()
            .HasMaxLength(500);
            
        builder.Property(d => d.Label)
            .HasMaxLength(100);
            
        builder.Property(d => d.AdditionalInfo)
            .HasMaxLength(1000);
            
        builder.HasOne(d => d.Person)
            .WithMany(p => p.Details)
            .HasForeignKey(d => d.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
