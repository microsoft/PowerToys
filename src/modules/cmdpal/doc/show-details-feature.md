# Show Details Context Action Feature

## Overview
This feature automatically adds a "Show Details" context action to ListItems that have Details when the parent Page has `ShowDetails = false`. This enables extensions to implement lazy detail loading where users can choose when to fetch and display detailed information.

## Use Case
Originally requested for GitHub extensions that may not want to always fetch issue bodies by default, but could provide a "Show Details" action to let users request the information when needed.

## How It Works
1. When a ListItem with Details is on a page with `ShowDetails = false`
2. A "Show Details" context action is automatically added to the item's context menu
3. When activated, it sends the same `ShowDetailsMessage` used for automatic details display
4. The details panel is populated and shown to the user

## Manual Testing
To test this feature:
1. Build and run PowerToys CmdPal
2. Search for "sample" and select "Sample Pages"
3. Navigate to "List Page With Details (ShowDetails=false)"
4. Right-click items with Details to see the "Show Details" action
5. Verify the action displays details when activated

## Implementation Location
- Core logic: `Microsoft.CmdPal.Core.ViewModels/ListItemViewModel.cs`
- Test page: `SamplePagesExtension/Pages/SampleListPageWithDetailsNoShow.cs`