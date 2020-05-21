# PowerToys FancyZones Custom Layout Hotkeys

- **What it is:** Allow users to change layouts using hotkeys, without going through the editor 
- **Authors:** Kamy
- **Spec Status:** Draft

# TODO's
 - Success Metrics
 - Requirement priorities
 
# 1. Overview
## 1.1 Elevator Pitch / Narrative

Changing layouts through the editor takes too long (2-5 secs) depending on computing power. A lot of users have raised this pain point (see section 6). Allowing users to assign hotkeys to their custom layouts would allow them to apply these instantly.

## 1.2 Customers
FancyZones who change their layouts often to:
- Swap to layouts with more zones as their number of open applications change
- Swap to layouts where the size of current zones are different 
- Change workplaces where the setup of monitors changes
	
	
## 1.3 Problem Statement and Supporting Customer Insights
See gathered [feedback form](https://forms.office.com/Pages/DesignPage.aspx?fragment=FormId%3Dv4j5cvGGr0GRqy180BHbR_pRopwr7jdKo2d3Zl9VMBRUQUxETTQ4NVBGOEY2SDNITVpJWE5RUlU5NS4u%26Token%3Dc59cde98dfbb4535b83b3aac790ab377). Many users change their layouts at least once a day, if not multiple times a day. Nearly all users change the layout of one display at a time. To do this, the user has to open the editor and apply the layout manually each time, which can take on average 5 secs. This has been enough of a pain point to raise multiple issues in the repo.
	
## 1.4 Existing Solutions or Expectations
To change layouts, the user currently needs to open the fancyzones editor, navigate to the desired layout, select the layout, then apply the layout. This takes up to 5 secs, depending on computer speed.
	
## 1.5 Goals / Non-goals

### Goal
- Custom hotkeys for custom layouts
- Apply layout through hotkeys on any display
- Toggle through all custom displays using hotkey
- Display notification when layout of different resolution is applied on a display
	
### Non-Goal
- Presets of multiple layouts (with hotkey)

# 2. Definition of Success

## 2.1. Expected Impact: Business, Customer, and Technology Outcomes, Experiments + Measures

| No. | Outcome | Measure | Priority |
|-----|---------|---------|----------|
| 1 | TBD | TBD | 0 |

# 3. Requirements

The proposed solution will allow users to assign a custom hotkey to any custom layout, and allow the user to apply the layout through hotkeys on any display.

# 3.1 Functional Requirements

| No. | Requirement | Measure |
|-----|---------|---------|
| 1 | Ability to assign a custom hotkey to a custom layout | TBD |
| 2 | Ability to edit a custom hotkey assigned to a custom layout | TBD |
| 3 | Ability to toggle layouts between custom layouts on the fly using a specific hotkey | TBD |
| 4 | User cannot assign the same hotkey to two custom layouts | TBD |
| 5 | Display notification when layout of different resolution is applied on a display through a hotkey | TBD |
| 6 | User can change layout through hotkey while "shift-dragging" a window into a zone | TBD |

# 4. Mocks
## 4.1 Custom layout hotkey
![image](https://user-images.githubusercontent.com/32441857/81828856-1c3bd800-94ef-11ea-8b22-d60341b82a3a.png)
### Setup
- Assign hotkeys for single custom layouts when creating or editing them

### Usage
- Gain focus of a display (through window or mouse hover)
- Quickly change layouts on any display by pressing the assigned hotkey

# 5. Reference Material 
## 5.1 Related GitHub Issues
[Aggregate Issue #2947](https://github.com/microsoft/PowerToys/issues/2947): Contains community feedback



