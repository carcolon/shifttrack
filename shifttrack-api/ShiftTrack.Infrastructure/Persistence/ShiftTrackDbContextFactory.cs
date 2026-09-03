using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShiftTrack.Infrastructure.Persistence;

public sealed class ShiftTrackDbContextFactory : IDesignTimeDbContextFactory<ShiftTrackDbContext>
{
    public ShiftTrackDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ShiftTrackDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? Environment.GetEnvironmentVariable("SHIFTTRACK_DB_CONNECTION_STRING");

        options.UseSqlServer(string.IsNullOrWhiteSpace(connectionString)
            ? "Server=localhost\\SQLEXPRESS;Database=ShiftTrackDb;Trusted_Connection=True;TrustServerCertificate=True;"
            : connectionString);

        return new ShiftTrackDbContext(options.Options);
    }
}
