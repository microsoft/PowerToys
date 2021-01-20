namespace Mages.Core
{
    using System;

    /// <summary>
    /// Represents the exception that is thrown on trying
    /// to interpret invalid code.
    /// </summary>
    public class ParseException : Exception
    {
        /// <summary>
        /// Creates a new parse exception.
        /// </summary>
        /// <param name="error">The error that occured.</param>
        public ParseException(ParseError error)
            : base("The given source code contains errors.")
        {
            Error = error;
        }

        /// <summary>
        /// Gets the detected parse error.
        /// </summary>
        public ParseError Error
        {
            get;
            private set;
        }
    }
}
