using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Moodle.Domain.Auth;

namespace Moodle.Infrastructure.Auth;

/// <summary>
/// AUTH veri modeli (WBS-2 §3.1.2): ASP.NET Core Identity tabloları (<c>AspNetUsers</c> vb.) ve
/// <see cref="SecurityEvent"/> günlüğü. crs_/grd_ tablolarına yabancı anahtar kurulmaz; diğer modüller
/// kullanıcıya yalnızca kimlik (int) ile başvurur (WBS-2 §2.3).
/// </summary>
public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<int>, int>(options)
{
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(e =>
        {
            // Örnek veri (ortam/ornek-veri.feature) ile diğer modüllerin kimlikleri aynı sırada
            // üretilmesi için kimlikler veritabanı tarafından değil, tohumlama tarafından atanır
            // (bkz. SeedDataset.UserId, Moodle.Application.Seeding).
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
        });

        builder.Entity<SecurityEvent>(e =>
        {
            e.ToTable("auth_security_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.AttemptedUsername).HasMaxLength(256).IsRequired();
            e.Property(x => x.EventType).HasConversion<string>().HasMaxLength(40);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.OccurredAt);
        });
    }
}
