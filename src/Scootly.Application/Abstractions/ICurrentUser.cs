using System;
using System.Collections.Generic;
using System.Text;

namespace Scootly.Application.Abstractions;

public interface ICurrentUser
{
    Guid UserId { get; }
    string Role { get; }
}
