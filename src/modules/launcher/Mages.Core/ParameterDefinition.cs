namespace Mages.Core
{
    using System;

    /// <summary>
    /// Represents the definition of a function parameter.
    /// </summary>
    public struct ParameterDefinition : IEquatable<ParameterDefinition>
    {
        private readonly String _name;
        private readonly Boolean _required;

        /// <summary>
        /// Creates a new parameter definition.
        /// </summary>
        public ParameterDefinition(String name, Boolean required)
        {
            _name = name;
            _required = required;
        }

        /// <summary>
        /// Gets the name of the parameter.
        /// </summary>
        public String Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Gets if the parameter is required.
        /// </summary>
        public Boolean IsRequired
        {
            get { return _required; }
        }

        /// <summary>
        /// Gets the hash code.
        /// </summary>
        public override Int32 GetHashCode()
        {
            return _name.GetHashCode() + _required.GetHashCode();
        }

        /// <summary>
        /// Checks for equality to the other object.
        /// </summary>
        public override Boolean Equals(Object obj)
        {
            if (obj is ParameterDefinition)
            {
                return Equals((ParameterDefinition)obj);
            }

            return false;
        }

        /// <summary>
        /// Checks for equality to the other parameter definition.
        /// </summary>
        public Boolean Equals(ParameterDefinition other)
        {
            return String.CompareOrdinal(_name, other._name) == 0 && 
                _required == other._required;
        }
    }
}
