using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Scootly.Domain.Common;

public abstract class Entity
{
    public Guid Id
    {
        get; private init;
    }
    protected Entity(Guid ıd)
    {
        Id = ıd;
    }
    protected Entity() { }
    
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (GetType() != other.GetType()) 
            return false;
        return Id == other.Id;
    }
    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }
    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }
}
