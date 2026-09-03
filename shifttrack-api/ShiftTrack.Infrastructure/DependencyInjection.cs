using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShiftTrack.Infrastructure.Persistence;

namespace ShiftTrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddShiftTrackPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ShiftTrackDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = configuration.GetConnectionString("DefaultConnection");
            }

            options.UseSqlServer(string.IsNullOrWhiteSpace(connectionString)
                ? "Server=localhost\\SQLEXPRESS;Database=ShiftTrackDb;Trusted_Connection=True;TrustServerCertificate=True;"
                : connectionString);
        });

        services.AddScoped<ShiftTrackSchemaValidator>();

        return services;
    }
}
