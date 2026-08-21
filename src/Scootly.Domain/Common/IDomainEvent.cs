using System;
using System.Collections.Generic;
using System.Text;

namespace Scootly.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
