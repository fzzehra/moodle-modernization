using Microsoft.EntityFrameworkCore;

namespace Moodle.Infrastructure.Persistence;

/// <summary>
/// Tek veritabanı bağlamı. Her modül kendi varlıklarını bir <c>IEntityTypeConfiguration&lt;T&gt;</c> sınıfıyla
/// tanımlar ve kendi <c>DbSet</c>'ini <b>burada değil</b>, <c>Persistence/Configurations/&lt;Modül&gt;/</c> altında
/// yapılandırır; böylece modüller aynı dosyada çakışmaz. Yapılandırmalar bu derlemeden otomatik bulunur.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
