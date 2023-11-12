using System;

namespace AutoComponentProperty
{
    [Flags]
    public enum GetFrom
    {
        This  = 1,
        Children = 1 << 1,
        Parent = 1 << 2,
    }
}