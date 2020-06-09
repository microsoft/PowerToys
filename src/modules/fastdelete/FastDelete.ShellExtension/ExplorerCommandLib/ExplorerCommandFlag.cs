using System;

namespace ExplorerCommandLib
{
    /// <summary>
    /// Describes the behavior of a subclass of <see cref="ExplorerCommandBase"/>.
    /// </summary>
    [Flags]
    public enum ExplorerCommandFlag
    {
        /// <summary>
        /// No special behavior is desired.
        /// </summary>
        Default = 0x0,

        /// <summary>
        /// The command should be displayed as a split button if any sub-commands are defined.
        /// </summary>
        HasSplitButton = 0x2,

        /// <summary>
        /// Hide the label for the command.
        /// </summary>
        HideLabel = 0x4,

        /// <summary>
        /// The command represents a separator item.
        /// </summary>
        IsSeparator = 0x8,

        /// <summary>
        /// Display an elevation shield icon next to the command.
        /// </summary>
        ShowsElevationShield = 0x10,

        /// <summary>
        /// Display a separator before this command.
        /// </summary>
        ShowSeparatorBefore = 0x20,

        /// <summary>
        /// Display a separator after this command.
        /// </summary>
        ShowSeparatorAfter = 0x40,

        /// <summary>
        /// The command should be displayed as a drop-down button if any sub-commands are defined.
        /// </summary>
        IsDropDown = 0x80,
    }
}