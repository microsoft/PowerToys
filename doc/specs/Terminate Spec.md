# **Terminate Program Spec**

![Terminate](../images/Logo.jpg "Power Toys")

- **What is it:** Shortcut for users to quickly terminate a running program
- **Authors:** Benjamin Leverette and Prudence Phillips
- **Spec Status:** Draft

## 1. Overview

### 1.1. Elevator Pitch / Narrative

Mike is debugging his code in Visual Studio and the program freezes with the "App not responding" text displaying on the title bar, informing him that he cannot continue his work without closing the program and restarting it. Mike has tried to close the program using the close button and end task via the task manager, but none of these mechanisms work in helping him terminate the process so he can proceed with his work. With this PowerToy installed, Mike now has a visual and accessible last-resort method to help him kill the process.

### 1.2. Customers

PowerToys is mainly targeted towards Windows Power Users though it is available to users who want to experience using windows in a more efficient and productive way.
  
### 1.3. Problem Statement and Supporting Customer Insights

Windows users need an accessible mechanism to completely kill a process when it is being unresponsive and hindering work flow. The team is still required to find solutions for:

- A public name for this PowerToy.
- The degree to which the process will be terminated.

### 1.4. Existing Solutions or Expectations

The current methods a user can close a running program in Windows include:

- Clicking on the close button in the program
- Closing the program via task manager
- Using the keyboard shortcut Alt + F4 to close the program

### 1.5. Goals/Non-Goals

- Develop this PowerToy and have sufficient time for testing and integration within our assigned 8 weeks for the project. 

- Meet customers expectation with end result of project.

## 2. Definition of Success

### 2.1. Expected Impact: Customer, and Technology Outcomes, Experiments + Measures

The PowerToys repo currently has 200+ people watching, over 4000 stars and 109 forks on github despite having an empty repo. Also, this particular PowerToy received a rating of 3.44/5 in the survey we sent out to the community asking them to rate how useful they think it will be. after the release of this PowerToy, the following will be used to measure our success rate: 

- At least a 5% increase in Github stars within a month of release
- A 3.75/5 rating on a post-completion Consumer Satisfaction Survey on this PowerToy.
- 100 downloads & installs within the first month of release.
- Less than 40% of unistalls by users who install this PowerToy

## 3. Requirements

### 3.1. Functional Requirements

To use this PowerToy, a user will:

- Press the chosen keyboard shortcut. This will display the 'Terminate' window.
- Click the left mouse button anywhere in the Terminate Window and drag the cursor to whatever other window they wish to kill.
  - Note: users can configure in their options to disable the yes/no prompt and just terminate apps instantly.
- Release the left mouse button over the new app to kill it.
- Once they are done with the utility, user closes the terminate window to exit.

![Terminate](images/Terminate%20Blurred.png "Terminate")

- Keyboard shortcut
  - Alt + Shift + X

3.1.1 Settings

The PowerToys app will have a settings framework for the Terminate utility to plug into. The settings framework has a UI frame that creates a page for the utility. Its settings will be represented as a json blob with the following features:

- Enable/Disable
  - The user can select to enable or disable the Terminate utility's functionality, which initializes or suspends its resource use.

- Custom Configuration
  - Similar to the functionality of a switch or radio button, the user will be able to select options for the appearance of the mouse cursor.

### 3.2 Public Name

The initially proposed name for this app is Terminate App. However, there are multiple other alternative names that we are also considering:

- Destroy App
- Knockdown Program
- Dismantle App

## 4. Dependencies

- The 8 week time limit of the explore internship
- Availability of public APIs, since we intend to make this project open source.
