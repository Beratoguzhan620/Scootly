using System;
using System.Collections.Generic;
using System.Text;

namespace Scootly.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
