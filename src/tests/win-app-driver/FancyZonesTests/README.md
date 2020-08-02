# FancyZones Test Plan

## Settings
  - [x] Test if settings are saved in file properly

## Editor
  - [x] Open editor by clicking button from settings 
      - [x] without settings file
      - [x] without settings folder
      - [x] with valid settings file
      - [x] with valid settings file contained cyrillic characters
      - [x] with invalid settings file
      - [x] with cropped file
  - [x] Open editor by hotkey 
      - [x] without settings file
      - [x] without settings folder
      - [x] with valid settings file
      - [x] with valid settings file contained cyrillic characters
      - [x] with invalid settings file
      - [x] with cropped file
  - [ ] Increase/decrease zone count, check min and max possible values
  - [ ] Test if settings are saved in file properly
    - [ ] `Show spacing` checked/unchecked
    - [ ] `Space around zone` saved correctly
  - [ ] `Space around zone` possible input values
  - [ ] Edit templates, check settings files
  - [ ] Create new custom layout
    - [ ] empty
    - [ ] one zone
        - [ ] fullscreen
        - [ ] not fullscreen
    - [ ] many zones
      - [ ] overlapping
      - [ ] non-overlapping
    - [ ] utf-16 layout name
    - [ ] empty layout name
    - [ ] special characters in layout name
  - [ ] Remove custom layout
  - [ ] Edit selected layout

### Usage
