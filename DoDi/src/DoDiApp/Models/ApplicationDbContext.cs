using Microsoft.EntityFrameworkCore;

namespace DoDiApp.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Term> Terms { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Term entity
            modelBuilder.Entity<Term>()
                .HasIndex(t => t.Name)
                .IsUnique();

            // Set default values
            modelBuilder.Entity<Term>()
                .Property(t => t.CreatedAt)
                .HasDefaultValueSql("NOW()");

            modelBuilder.Entity<Term>()
                .Property(t => t.UpdatedAt)
                .HasDefaultValueSql("NOW()");

            modelBuilder.Entity<Term>()
                .Property(t => t.Version)
                .HasDefaultValue(1);
        }
    }
}