using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KisanMitraAI.Infrastructure.Data;

public class KisanMitraDbContextFactory : IDesignTimeDbContextFactory<KisanMitraDbContext>
{
    public KisanMitraDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<KisanMitraDbContext>();
        
        // Use connection string from environment or default to localhost
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSQL") 
            ?? "Host=localhost;Database=kisanmitradb;Username=postgres;Password=postgres";
        
        optionsBuilder.UseNpgsql(connectionString);
        
        return new KisanMitraDbContext(optionsBuilder.Options);
    }
}
