using ShiftTrack.Domain.Entities;

namespace ShiftTrack.Application.Interfaces;

public interface IRequestExportRepository
{
    Task CreateAsync(RequestExportJob job);
    Task<RequestExportJob?> GetAsync(Guid id);
    Task<int> SetHangfireJobIdAsync(Guid id, string hangfireJobId);
    Task<int> MarkProcessingAsync(Guid id);
    Task<int> MarkCompletedAsync(Guid id, string fileName, string contentType, byte[] fileContent, DateTime expiresAtUtc);
    Task<int> MarkFailedAsync(Guid id, string errorMessage);
    Task<IEnumerable<User>> GetUsersForExportAsync();
    Task<IEnumerable<PtoRequest>> GetPtoRequestsForExportAsync();
    Task<IEnumerable<SwapRequest>> GetSwapRequestsForExportAsync();
}
