# GIF Maker Spec

![Terminate](../images/Logo.jpg "Power Toys")

- **What is it:** Users can record their screen and turn the recording into a GIF.
- **Authors:** Benjamin Leverette and Prudence Phillips
- **Spec Status:** Draft

## 1. Overview

### 1.1. Elevator Pitch / Narrative

 David loves using GIFs to add creativity ti his typical responses to messages.  His friend Amanda emails him and asks what he would like to eat for lunch today.  David recently watched a burrito video, so he uses the GIF maker to screen record a brief moment from the video in order to tell Amanda that its "Burrito Day."

### 1.2. Customers

Like all utilities from PowerToys, the GIF Maker feature is for power users and developers who are looking to tune and streamline their Windows experience for greater productivity.
  
### 1.3. Problem Statement and Supporting Customer Insights

 Power users need a way to better record their screens and use the recordings to create GIFs.  A teammate held a personal interactions with the community that validate the usefulness of a feature that provides such functionality.

### 1.4. Existing Solutions or Expectations

Users currently must use the game bar in order to record the screen, and there is no way to turn recordins into GIFs.  There are third-party resources that allow users to create GIFs online after uploading images and videos.

### 1.5. Goals/Non-Goals

 Design and develop a feature that can record screens and turn them into GIFs within an 8 week period.

## 2. Definition of Success

### 2.1. Expected Impact: Customer, and Technology Outcomes, Experiments + Measures

This feature will give users the ability to screen record and create GIFs effectively and add a new dimension to using GIFs on Windows. Measures of success include:

- A score of an average of 3.75 stars on a new Consumer Design Satisfaction Survey
- 5% increase in number of stars on the Github repo within first month of release
- Installed by 10% of users who have starred the PowerToys Github
- Uninstalled by less than 30% of users who installed
- Launched by over 60% of users who installed

## 3. Requirements

### 3.1. Functional Requirements

#### 3.1.1. Initial UX/UI

Users may or may not have any open windows on their screen. They can access this utility the same way you would access the Snip & Sketch tool on Windows or using an assigned keyboard shortcut. After the tool has been accessed by the user they can:

- Start a new screen recording
  - The tool will record the user's entire screen
    - Video can be saved in multiple formats (.GIF, .MP4, .HEIC, .M4A, etc...)
- Edit an already existing video into a GIF

- Keyboard Shortcut Suggestions:
  - Ctrl + Alt + R
  - Ctrl + Alt + G

![GIF Maker UI](images/GIF%20Maker%20Spec.png "GIF Maker UI")

#### 3.1.2. File Button Features

Upon opening the GIF Maker utility, users must click 'New' to begin.  They will have two options: 'Record' and 'Open'.

- Record
  - Choosing this option closes the GIF Maker window and displays a record button.
  - When they click the record button, it starts a brief countdown. Once the countdown concludes, it records the user's entire screen.
  - User clicks the stop button to end the recording
- Open
  - If the user clicks 'Open', the mini File Explorer window opens for the user to select a video.
  - Once a video is selected, it appears in the GIF Maker, below the tool bar, for the user to edit.
- Saving
  - Recorded videos can be saved in multiple video formats
  - Users can further edit their videos and save them as GIFs

#### 3.1.3. GIF Editing Tools

- Add Text
  - Users can add text to their videos
  - Default Windows fonts will be available
- Crop Video
  - Users can crop their videos manually or use the default cropping options. The default options will include:
    - 1:1
    - 4:3
    - 3:2
    - 16:9
  - Users can also leave their video in their original dimensions
- Trim Video
  - Users can trim the length of their videos
  - Users can make GIFs with a minimum time of 0.05 seconds and a maximum time of 6.00 seconds

#### 3.1.4. Settings

The PowerToys app will have a settings framework for the GIF Maker utility to plug into. The settings framework has a UI frame that creates a page for the utility. Its settings will be represented as a json blob with the following features:

### 3.2. Enable/Disable

- The user can select to enable or disable the GIF Maker utility's functionality, which initializes or suspends its resource use.

### 3.3. Custom Configuration

- Similar to the functionality of a switch or radio button, the user will be able to select options for the countdown: 0, 5, or 10 secs.

## 4. Dependencies

- Explore Internship Program limits us to an 8-week window to complete the task.
- Availability of public API's