# Interfaces

## `IFileActionsMenuPlugin`

This interface is for defining a File Actions menu plugin. Currently there is no use to the properties (except for `TopLevelMenuActions`), but this will be added in the future.

### `string Name`

Name of the plugin.

### `string Description`

A short description about the capabilities of the plugin.

### `string Author`

The author/the company that developed the plugin.

### `IAction[] TopLevelMenuActions`

An array of actions shown at the top-level of the menu.

## `IAction`

This interface defines a single action, that you can invoke from the menu.

### `string[] SelectedItems`

When the action is invoked this array will contain the paths to the selected files and folders.

### `string Title`

The title of the action shown in the menu.

### `ItemType Type`

The type of the action.

### `IAction[]? SubMenuItems`

The actions displayed in the sub menu, when `ItemType` is `HasSubMenu`.

### `int Category`

The category number of the action. Different categories are seperated by a seperator in the top-level menu. Best practice is that third-party plugins use numbers between `100` and `998`.

### `IconElement? Icon`

An optional icon, that is displayed in the menu alongside the action.

### `bool IsVisible`

Determines whetever the action is visible in the menu or not.

### `Task Execute(object sender, RoutedEventArgs e)`

This function is called when the action is executed. `sender` is the `MenuItem` element that invoked the element. `e` are the event arguments of the click event. This function is only called if the `ItemType` is `SingleItem`.

Returned is a Task that when completed causes the menu to close.

## `ICheckabableAction`

Abstract class that defines a checkable action. You always have to have atleast two checkable actions. If you check one item of a group, all the other items will be unchecked.

Following abstract properties follow the same rules as the ones of the same name in `IAction`:

* `SelectedItems`
* `Title`
* `Icon`
* `IsVisible`

### `bool IsChecked`

Get whetever the current element is checked or not.

### `bool IsCheckedByDefault`

Whetever the current item is checked by default. There must be exactly one item per group that has this property set to `true`.

### `string? CheckableGroupUUID`

A UUID that is exclusive to the current group of checkable items.

## `IActionAndRequestCheckedMenuItems`

The same as `IAction`, but you request access to all the checked menu items.

### `CheckedMenuItemsDictionary CheckedMenuItemsDictionary`

This dictionairy contains all the checkable menu items per group (defined by the group UUID).
