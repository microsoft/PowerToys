# PowerToys Out Of Box Experience Proposal and Code

##### Developers: [Furaha Damién](https://github.com/furahadamien), [Letitia Kwan](https://github.com/letitiakwan), [Jessica Lim](https://github.com/JessicaLim8)
##### Program Manager : [Eunice Choi](https://github.com/eunicechoi98)
##### Designer : Rafael Flora

## Overview
A team of Microsoft Garage interns have created a proposal for the out-of-box experience after the first time install of the PowerToys app, and a proposal for the UI experience of updates.

## 1. Welcome Screen

#### Rationale:
PowerToys v17 has a silent launch after first time installation and update. As evidenced in issue #1285, users are left wondering if anything happened at all. The aim of this new feature is to allow users a chance to appreate the install/update of PowerToys while at the same time giving them a chance to explore what PowerToys has to offer if they so wish to. 

#### Design:
<img align="left" src="./images/welcomeWindow.png" /> Welcome window


#### Code:
The frontend and the backend for this window are in [onLaunchContentDialog.xaml](https://github.com/microsoft/PowerToys/blob/interns/users/t-fudami/documentation/POC-OOBE/OOBE-Sandbox/PowerToys%20Settings%20Sandbox/Views/onLaunchContentDialog.xaml) and [onLaunchContentDialog.xaml.cs](https://github.com/microsoft/PowerToys/blob/interns/users/t-fudami/documentation/POC-OOBE/OOBE-Sandbox/PowerToys%20Settings%20Sandbox/Views/onLaunchContentDialog.xaml.cs) respectively. It is later called in the MainPage as below:

```
 private async void powerOnLaunchDialog()
        {
            onLaunchContentDialog dialog = new onLaunchContentDialog();
            dialog.PrimaryButtonClick += Dialog_PrimaryButtonClick;
            await dialog.ShowAsync();
        }

        private void Dialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            OpenFirstGeneralSettingsTip();
        }
```

#### Considerations: 
The aim of the feature is to introduce PowerToys to the user while at the same time not getting in their way. To achieve this we decided to use a [contentDialog](https://docs.microsoft.com/en-us/uwp/api/Windows.UI.Xaml.Controls.ContentDialog?view=winrt-19041). This allows the user to either explore PowerToys features as soon as they install/update the application or do the exploration at a later time. For users who chose the later, The window privides animated gifs that give a glimpse of what PowerToys has to offer. Using a contentDialog also allows us to fire a toast notifcation at a later time.
