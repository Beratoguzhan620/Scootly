using System.Data;
using Microsoft.EntityFrameworkCore;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Concurrency.Tests;

public static class IsolationLevelTestHelper
{
    public static async Task<bool> TryReserveWithIsolationLevel(
        ScootlyDbContext dbContext,
        Guid vehicleId,
        IsolationLevel isolationLevel)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(isolationLevel);

        try
        {
            var vehicle = await dbContext.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId);

            if (vehicle is null)
                return false;

            vehicle.Reserve();

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            return false;
        }
    }
}