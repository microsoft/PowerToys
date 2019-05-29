# **PowerToys File Classification Spec**

![MS PowerToys](https://hothardware.com/ContentImages/NewsItem/48038/content/Microsoft_PowerToys.jpg "PowerToys")
## Users can quickly rename and group files.
### Authors: Benjamin Leverette and Prudence Phillips
### Spec Status: Draft
## 1. Overview
---
**1.1. Elevator Pitch / Narrative** 

David has recently uploaded thousands of pictures from his camera to his Surface Laptop.  Unfortunately, the images all have generic names such as 'IMG_141' instead of labels that he can identify.  David downloads the File Classification PowerToy that allows him to create new folders with files that share a label, rename a group of files with a label, or change the name or portion of a name in a group of files to another name.

**1.2. Customers**

Like all utilities from PowerToys, the File Classification feature is for power users and developers who are looking to tune and streamline their Windows experience for greater productivity.
  
**1.3. Problem Statement and Supporting Customer Insights**

Power users need a better way to organize files, from renaming files to creating new folders for similar files.  Our PowerToys Consumer Survey received feedback validating the usefulness of a feature that provides such functionality.

**1.4. Existing Solutions or Expectations**

Users currently have to highlight a group of files and right click to rename them.  Users must manually create a folder, move files to that folder, and rename them.  There are third-party resources that allow users to rename files similarly.

We expect users to install and enable PowerToys for Windows in order to access the File Classification utility.

**1.5. Goals/Non-Goals**

Design and develop a feature that can rename and group files within an 8 week period.

## 2. Definition of Success
---
**2.1. Expected Impact: Customer, and Technology Outcomes, Experiments + Measures**

Our PowerToys Consumer Survey received an abundant amount of participation and feedback from a community of passionate power users.  This feature will give them the ability to rename, group and organize files in a way that makes their virtual library of files more organized and efficient.  As interns, we would have 8 weeks to complete the project.

## 3. Requirements
---
**3.1.	Functional Requirements**

- Users must select a group of files by checking each file or using Shift + directional keys.  They must then use a shortcut to open the File Classification utility, which provides three options:
  - Automates folder creation and movement of user-selected files after receiving a label determined by the user.
  - Renames all user-selected files after receiving a label determined by the user.
  - Identifies a string of characters in the names of user-selected files and changes it to a string determined by the user.
- Labels are used to rename files by completely overwriting the files' current names with the custom 'Label' typed by the user and each file receives a counting number.
- Shortcut should take accessibility into account.
  - Perhaps Windows key + C?

![](PT Images/File Classification Design Blurred.png?raw=true)

**3.2. Measure Requirements**

- Survey what power users want out of the File Classification feature through forms and Github.

## 4. Dependencies
---
- Explore Internship Program limits us to an 8-week window to complete the task.

- Availability of public API's