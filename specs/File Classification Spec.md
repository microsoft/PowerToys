# **PowerToys File Classification Spec**

![MS PowerToys](https://hothardware.com/ContentImages/NewsItem/48038/content/Microsoft_PowerToys.jpg "PowerToys")
## Users can quickly rename and group files.
### Authors: Benjamin Leverette and Prudence Phillips
### Spec Status: Draft
> ## 1. Overview
&nbsp; &nbsp; &nbsp; &nbsp; **1.1. Elevator Pitch / Narrative** 

&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp;  David has recently uploaded thousands of pictures from his camera to his Surface Laptop.  Unfortunately, the images all have generic names such as 'IMG_141' instead of labels that he can identify.  David downloads the File Classification PowerToy that allows him to create new folders with files that share a label, rename a group of files with a label, or change the name or portion of a name in a group of files to another name.

&nbsp; &nbsp; &nbsp; &nbsp; **1.2. Customers**

&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; Like all utilities from PowerToys, the File Classification feature is for power users and developers who are looking to tune and streamline their Windows experience for greater productivity.
  
&nbsp; &nbsp; &nbsp; &nbsp; **1.3. Problem Statement and Supporting Customer Insights**

&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; Power users need a better way to organize files, from renaming files to creating new folders for similar files.  Our PowerToys Consumer Survey received feedback validating the usefulness of a feature that provides such functionality.

&nbsp; &nbsp; &nbsp; &nbsp; **1.4. Existing Solutions or Expectations**

&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; Users currently have to highlight a group of files and right click to rename them.  Users must manually create a folder, move files to that folder, and rename them.  There are third-party resources that allow users to rename files similarly.

&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; We expect users to install and enable PowerToys for Windows in order to access the File Classification utility.

&nbsp; &nbsp; &nbsp; &nbsp; **1.5. Goals/Non-Goals**

&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; Design and develop a feature that can rename and group files within an 8 week period.

> ## 2. Definition of Success
&nbsp; &nbsp; &nbsp; &nbsp; **2.1. Expected Impact: Customer, and Technology Outcomes, Experiments + Measures**

&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; Our PowerToys Consumer Survey received an abundant amount of participation and feedback from a community of passionate power users.  This feature will give them the ability to rename, group and organize files in a way that makes their virtual library of files more organized and efficient.  As interns, we would have 8 weeks to complete the project.

> ## 3. Requirements
&nbsp; &nbsp; &nbsp; &nbsp; **3.1.	Functional Requirements**

&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; - Program that automates folder creation and files movement after receiving a label determined by the user.

&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; - Program that renames all highlighted files after receiving a label determined by the user.

&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; - Program that identifies a string of characters in the names of highlighted files and changes it to a string determined by the user.

&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; - Shortcut should take accessibility into account.

&nbsp; &nbsp; &nbsp; &nbsp; **3.2. Measure Requirements**

&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; - Survey what power users want out of the File Classification feature through forms and Github.

> ## 4. Dependencies
&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; - Explore Internship Program limits us to an 8-week window to complete the task.

&nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; &nbsp; - Availability of public API's