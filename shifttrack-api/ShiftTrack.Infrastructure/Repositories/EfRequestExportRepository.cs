using Microsoft.EntityFrameworkCore;
using ShiftTrack.Application.Interfaces;
using ShiftTrack.Domain.Entities;
using ShiftTrack.Infrastructure.Persistence;

namespace ShiftTrack.Infrastructure.Repositories;

public sealed class EfRequestExportRepository(ShiftTrackDbContext dbContext) : IRequestExportRepository
{
    public async Task CreateAsync(RequestExportJob job)
    {
        dbContext.RequestExportJobs.Add(job);
        await dbContext.SaveChangesAsync();
    }

    public Task<RequestExportJob?> GetAsync(Guid id) =>
        dbContext.RequestExportJobs.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);

    public Task<int> SetHangfireJobIdAsync(Guid id, string hangfireJobId) =>
        dbContext.RequestExportJobs
            .Where(item => item.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.HangfireJobId, hangfireJobId)
                .SetProperty(item => item.Status, "queued"));

    public Task<int> MarkProcessingAsync(Guid id) =>
        dbContext.RequestExportJobs
            .Where(item => item.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, "processing")
                .SetProperty(item => item.StartedAtUtc, DateTime.UtcNow)
                .SetProperty(item => item.ErrorMessage, (string?)null));

    public Task<int> MarkCompletedAsync(Guid id, string fileName, string contentType, byte[] fileContent, DateTime expiresAtUtc) =>
        dbContext.RequestExportJobs
            .Where(item => item.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, "completed")
                .SetProperty(item => item.FileName, fileName)
                .SetProperty(item => item.ContentType, contentType)
                .SetProperty(item => item.FileContent, fileContent)
                .SetProperty(item => item.CompletedAtUtc, DateTime.UtcNow)
                .SetProperty(item => item.ExpiresAtUtc, expiresAtUtc)
                .SetProperty(item => item.ErrorMessage, (string?)null));

    public Task<int> MarkFailedAsync(Guid id, string errorMessage) =>
        dbContext.RequestExportJobs
            .Where(item => item.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, "failed")
                .SetProperty(item => item.ErrorMessage, errorMessage)
                .SetProperty(item => item.CompletedAtUtc, DateTime.UtcNow));

    public async Task<IEnumerable<User>> GetUsersForExportAsync() =>
        await dbContext.Users.AsNoTracking().ToArrayAsync();

    public async Task<IEnumerable<PtoRequest>> GetPtoRequestsForExportAsync() =>
        await dbContext.PtoRequests
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToArrayAsync();

    public async Task<IEnumerable<SwapRequest>> GetSwapRequestsForExportAsync() =>
        await dbContext.SwapRequests
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToArrayAsync();
}
