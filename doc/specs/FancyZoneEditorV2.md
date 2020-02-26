# PowerToys FancyZones Editor v2

<img align="right" width="200" src="../images/Logo.jpg" />

- **What is it:** Improving the end user experience for setting and creating new zones for FancyZones.
- **Authors:** Clint Rutkas
- **Spec Status:** Draft

# TODO's

- Possible UX look/feel comps
- Success metrics
- more requirements for all of section 4.1
- 4.1.2 - Think more about how keyboard UX for editor would work.
  - Rather than title bar handle for moving, do we have modes (Move, Resize, Split, Edit (Rename, L/T/W/H))?
    - I feel like this would heavily simplify information overload and easier for keyboard interaction.

# 1. Overview

## 1.1. Elevator Pitch / Narrative

Make a straight-forward layout selector / editor for FancyZone users. *mic drop*
The Github issue which encompasses many of the asks, #1032, is an epic of a lot of user pain points.  This is also part of the v1.0 strategy under "Raise product quality".

## 1.2. Customers

PowerToys exist for two reasons. Users want to squeeze more efficiency out of the Windows 10 shell and customize it to their individual workflows. We can be more targeted for scenarios to help do rapid iterations. Think about the countless small utilities that Microsoft engineers have written to make themselves more productive.

## 1.3. Problem Statement and Supporting Customer Insights

Looking at our Github issue page with the editor tag, we see lots of ‘ease of use’ issues.  The most common, figuring out how to put multi-monitor zones, is an example of a pain point. The editor itself does not expose this so you must discover it manually.  Another issue is the two separate editors. Once you create a layout in one, you are locked into that style. On this topic, if you used the grid-based editor, deleting zones isn’t straight forward. You have to merge them by selecting zones instead of just deleting a divider.

## 1.4. Existing Solutions or Expectations

See Section 2.

## 1.5. Goals/Non-Goals

### Goals

- Easier multi-monitor experience
- Virtual Desktop experience
- Unified editor experience
- Easier adjustments for zones
- Easier shareability across multiple monitors
- Easier discoverability for what zone you're in
- Accessibility
- Localized ready UX

### Non-goals

- Rewrite entire base
- Adopt WinUI 3 for this iteration
- Profiles (Home vs Work)
- Directly doing work for the actual zone interaction model
  - There will be work done to improve it within this work item
- Improve OOBE for first-time user (will be addressed in #1285)

We will discuss what currently is there for a knowledge base.

# 2. Existing Experience

## 2.1. Launching the editor / pick your layout

you must either use:

- Hot key (WinKey+~)
- Settings -> Fancy Zones -> Click “edit zones”

## 2.2. How to pick a monitor for layout

Which monitor your screen is on determines the monitor the layout editor is launched.

## 2.3. Types of editors

- Canvas Editor – This allows for free form zone creation along with overlapping zones.
- Grid Editor – Allows for zones to be merged and divided. You cannot do overlapping zones in this mode.

![alt text][newLayoutDialog]

## 2.4. Canvas Editor

![alt text][canvasEditor]

## 2.5. Grid Editor

![alt text][gridEditor]

# 3. Definition of Success

## 3.1. Expected Impact: Business, Customer, and Technology Outcomes, Experiments + Measures

| No. | Outcome | Measure | Priority |
|-----|---------|---------|----------|
| 1 | TBD | TBD | 0 |

# 4. Requirements

The proposed solution will be a hybrid model that will merge the canvas and grid based editors together.  We'll add in fine adjustment controls that will allow for both end user tweaking but will also make the system accessible to people that have low-motor control.

TODO: CLINT ADD IN MOCKS

## 4.1. Functional Requirements

### 4.1.1. General

| No. | Requirement | Priority |
| --- | ----------- | -------- |
| 1 | The default experience is a grid based layout | 0 |
| 3 | User can add a zone on top of the existing master grid based layout (This would enable the hybrid canvas scenario) | 0 |
| 4 | For applying layouts, the zone layout will resize smoothly to accordingly. *see edge cases | 0 |
| 5 | Top center would have name,  | 0 |
| 6 | Ability to delete layout from template | 0 |
| 7 | Ability to resize window | 2 |
| 7 | Resize window remembered | 2 |
| x | x | 0 |

**TODO: CLINT ADD IN MORE**

### 4.1.2 General Zone Editor

| No. | Requirement | Priority |
| --- | ----------- | -------- |
| 1 | All Zones can have their height & width adjusted | 0 |
| 2 | All Zones will have have a number that is clearly visible | 0 |
| 3 | The zone number is alterable, when conflict happens, the conflict will become last #. | 0 |
| 4 | Free form Zones can have their x/y top left coordinate adjusted | 0 |
| 5 | Ability to save, cancel, rename layout | 0 |
| x | x | 0 |

### 4.1.3 Grid-based Zones

| No. | Requirement | Priority |
| --- | ----------- | -------- |
| 1 | Grid-based dividers can move via arrow keys | 0 |
| 2 | Grid-based dividers, when focused, can be deleted with backspace or delete key | 0 |
| 3 | Grid-based dividers can be deleted with via right click->Delete | 0 |
| 4 | Grid-based zones can be merged together right click->merge | 0 |
| x | x | 0 |

### 4.1.3 Canvas-based Zones

| No. | Requirement | Priority |
| --- | ----------- | -------- |
| 1 | Zone can be added in via | 0 |
| 2 | Users can adjust Top / Left of Zone | 0 |
| 3 | This canvas zone can be divided | 0 |
| 4 | The canvas itself can be deleted | 0 |
| x | x | 0 |

**TODO: CLINT ADD IN MORE**

### 4.1.3 Updates to FancyZones

| No. | Requirement | Priority |
| --- | ----------- | -------- |
| 1 | Zone Number visible | 0 |
| 2 | Tab order is respected via WinKey+Arrow keys | 0 |
| x | x | 0 |

## 4.2. Measure Requirements

| No. | Outcome | Measure | Priority |
|-----|---------|---------|----------|
| 1 | TBD | TBD | TBD |

# 5. Edge cases

There are areas in this where we need to be aware of edge cases.  Automatic resizing due to monitor size difference is one area where we will hit it.

- Reasons why i think we have to percentage based to allow for scale to fit vs allowing a hybrid of hard-coded & percentages.
  - Lets use my 2 monitors for instance, 3840px in width versus 2256.  Any hard-coded layout would have to adjust 1584px.  
  - the system would have to be aware at layout design time of monitor restrictions like not allowing a zone being designed on the larger screen to be larger than the smaller screen.
  - if you get a new monitor, excellent chance it will have a different resolution/DPI.
  - As an end user, would you really ever know that something was 12% of your screen versus a hard-coded 200px unless you measured and it was 201px?
  - Also think about Left/Top, not just width, an item could easily be off screen.
- We cannot automatically assume same Ratio, below is my 4k monitor and my surface laptops, different resolutions and screen ratios
  - ![alt text][multimMonStyleLayout]

[canvasEditor]: images/specs/fzv2/canvasEditor.png "v1 Canvas Editor screenshot"
[gridEditor]: images/specs/fzv2/gridEditor.png "v1 Grid Editor screenshot"
[multimMonStyleLayout]: images/specs/fzv2/multiMonStyleLayout.png "One permutation for multi-monitors"
[newLayoutDialog]: images/specs/fzv2/newLayoutDialog.png "v1 New layout dialog prompt"
<!-- [x]: images/specs/fzv2/x.png "x" -->
