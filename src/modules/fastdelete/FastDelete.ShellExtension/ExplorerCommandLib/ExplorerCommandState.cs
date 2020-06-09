using System;

namespace ExplorerCommandLib
{
    /// <summary>
    /// Describes the state of a subclass of <see cref="ExplorerCommandBase"/>.
    /// </summary>
    [Flags]
    public enum ExplorerCommandState : int
    {
        /// <summary>
        /// The command is enabled. This is the default.
        /// </summary>
        Enabled = 0x0,

        /// <summary>
        /// The command is disabled.
        /// </summary>
        Disabled = 0x1,

        /// <summary>
        /// The command is not shown to the user.
        /// </summary>
        Hidden = 0x2,

        /// <summary>
        /// The command is displayed with a checkbox, but it is not checked.
        /// </summary>
        CheckBox = 0x4,

        /// <summary>
        /// The command is show with a checkmark.
        /// </summary>
        Checked = 0x8,

        /// <summary>
        /// A radio button symbol (bullet) is displayed instead of a checkmark if the command is checked.
        /// </summary>
        RadioCheck = 0x10,
    }
}