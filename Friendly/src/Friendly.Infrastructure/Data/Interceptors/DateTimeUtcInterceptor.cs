using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Friendly.Infrastructure.Data.Interceptors;

public class DateTimeUtcInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ConvertDateTimesToUtc(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, 
        InterceptionResult<int> result, 
        CancellationToken cancellationToken = default)
    {
        ConvertDateTimesToUtc(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ConvertDateTimesToUtc(DbContext? context)
    {
        if (context == null) return;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                foreach (var property in entry.Properties)
                {
                    var value = property.CurrentValue;
                    
                    if (value is DateTime dateTime)
                    {
                        DateTime utcDateTime = dateTime.Kind switch
                        {
                            DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
                            DateTimeKind.Local => dateTime.ToUniversalTime(),
                            DateTimeKind.Utc => dateTime,
                            _ => dateTime.ToUniversalTime()
                        };
                        
                        property.CurrentValue = utcDateTime;
                    }
                    else if (property.Metadata.ClrType == typeof(DateTime?))
                    {
                        if (value is DateTime nullableDateTime)
                        {
                            DateTime utcDateTime = nullableDateTime.Kind switch
                            {
                                DateTimeKind.Unspecified => DateTime.SpecifyKind(nullableDateTime, DateTimeKind.Utc),
                                DateTimeKind.Local => nullableDateTime.ToUniversalTime(),
                                DateTimeKind.Utc => nullableDateTime,
                                _ => nullableDateTime.ToUniversalTime()
                            };
                            
                            property.CurrentValue = utcDateTime;
                        }
                    }
                }
            }
        }
    }
}
