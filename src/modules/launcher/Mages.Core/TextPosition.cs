namespace Mages.Core
{
    using System;

    /// <summary>
    /// Represents a position within a text source.
    /// </summary>
    public struct TextPosition : IEquatable<TextPosition>
    {
        #region Fields

        private Int32 _row;
        private Int32 _column;
        private Int32 _index;

        #endregion

        #region ctor

        /// <summary>
        /// Creates a new text position.
        /// </summary>
        /// <param name="row">The row number.</param>
        /// <param name="column">The column number.</param>
        /// <param name="index">The character index.</param>
        public TextPosition(Int32 row, Int32 column, Int32 index)
        {
            _row = row;
            _column = column;
            _index = index;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the row in the source code.
        /// </summary>
        public Int32 Row
        {
            get { return _row; }
        }

        /// <summary>
        /// Gets the column in the source code.
        /// </summary>
        public Int32 Column
        {
            get { return _column; }
        }

        /// <summary>
        /// Gets the index (absolute position) in the source code.
        /// </summary>
        public Int32 Index
        {
            get { return _index; }
        }

        #endregion

        #region Operators

        /// <summary>
        /// Compares the index of the left text position against the index of the right text position.
        /// </summary>
        public static Boolean operator <(TextPosition left, TextPosition right)
        {
            return left.Index < right.Index;
        }

        /// <summary>
        /// Compares the index of the left text position against the index of the right text position.
        /// </summary>
        public static Boolean operator >(TextPosition left, TextPosition right)
        {
            return left.Index > right.Index;
        }

        /// <summary>
        /// Compares the index of the left text position against the index of the right text position.
        /// </summary>
        public static Boolean operator <=(TextPosition left, TextPosition right)
        {
            return left.Index <= right.Index;
        }

        /// <summary>
        /// Compares the index of the left text position against the index of the right text position.
        /// </summary>
        public static Boolean operator >=(TextPosition left, TextPosition right)
        {
            return left.Index >= right.Index;
        }

        /// <summary>
        /// Compares the index of the left text position against the index of the right text position.
        /// </summary>
        public static Boolean operator ==(TextPosition left, TextPosition right)
        {
            return left.Index == right.Index;
        }

        /// <summary>
        /// Compares the index of the left text position against the index of the right text position.
        /// </summary>
        public static Boolean operator !=(TextPosition left, TextPosition right)
        {
            return left.Index != right.Index;
        }

        #endregion

        #region Equatable

        /// <summary>
        /// Checks the types for equality.
        /// </summary>
        public override Boolean Equals(Object obj)
        {
            var other = obj as TextPosition?;
            return other.HasValue ? Equals(other.Value) : false;
        }

        /// <summary>
        /// Returns the index of the text position.
        /// </summary>
        public override Int32 GetHashCode()
        {
            return _index;
        }

        /// <summary>
        /// Checks the types for equality.
        /// </summary>
        public Boolean Equals(TextPosition other)
        {
            return this == other;
        }

        #endregion
    }
}
