namespace Mages.Core.Runtime.Functions
{
    using System;

    /// <summary>
    /// Defines a procedure to set values.
    /// </summary>
    /// <param name="arguments">The index arguments.</param>
    /// <param name="value">The value to set.</param>
    public delegate void Procedure(Object[] arguments, Object value);
}
