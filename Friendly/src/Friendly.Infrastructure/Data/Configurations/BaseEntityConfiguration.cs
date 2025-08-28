using Friendly.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Friendly.Infrastructure.Data.Configurations;

public abstract class BaseEntityConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(e => e.Id);
        
        // Configure DateTime properties to use timestamp without time zone
        // and let the interceptor handle UTC conversion
        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp without time zone")
            .IsRequired();
            
        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp without time zone");
            
        builder.Property(e => e.CreatedBy)
            .HasMaxLength(100);
            
        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(100);
    }
}
