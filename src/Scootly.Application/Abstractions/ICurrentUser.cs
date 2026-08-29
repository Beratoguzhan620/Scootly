using System;
using System.Collections.Generic;
using System.Text;

namespace Scootly.Application.Abstractions
{
    internal class ICurrentUser
    {
        Guid UserId { get; }
        string Role { get; }

    }
}
