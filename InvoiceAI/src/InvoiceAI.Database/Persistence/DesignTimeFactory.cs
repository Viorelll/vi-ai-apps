using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace InvoiceAI.Database.Persistence;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=invoiceai;Username=invoiceai;Password=placeholder").Options);
}
