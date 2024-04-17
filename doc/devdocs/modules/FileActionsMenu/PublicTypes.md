# Public types (Relevant to plugins)

## `FileActionsMenu.Interfaces.Seperator`

One predefined action representing a seperator.

## `FileActionsMenu.Interfaces.CheckedMenuItemsDictionary`

An alias for `Dictionary<string, List<(MenuFlyoutItemBase, IAction)>>`.

The string represents the UUID of a group of checkable items.

The list contains the corresponding elements and action definitions.
