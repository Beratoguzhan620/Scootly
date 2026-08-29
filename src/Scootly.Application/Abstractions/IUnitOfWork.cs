using System;
using System.Collections.Generic;
using System.Text;

namespace Scootly.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
