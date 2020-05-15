# PowerToys Out Of Box Experience Proposal and Code

##### Developers: [Furaha Damién](https://github.com/furahadamien), [Letitia Kwan](https://github.com/letitiakwan), [Jessica Lim](https://github.com/JessicaLim8)
##### Program Manager : [Eunice Choi](https://github.com/eunicechoi98)
##### Designer : Rafael Flora

## Overview
A team of Microsoft Garage interns have created a proposal for the out-of-box experience after the first time install of the PowerToys app, and a proposal for the UI experience of updates.

## 1. Welcome Screen

#### Rationale:
PowerToys v17 has a silent launch after first time installation and update. As evidenced in issue [#1285](https://github.com/microsoft/PowerToys/issues/1285), users are left wondering if anything happened at all. The aim of this new feature is to allow users a chance to appreate the install/update of PowerToys while at the same time giving them a chance to explore what PowerToys has to offer if they so wish to. 

#### Design:
![](./images/welcomeWindow.png)
*Welcome window*

<<<<<<< HEAD
=======
#### Code:

#### Considerations:

## 2. Adaptive Sizing Layout Change

#### Rationale: 
When the General Settings window is minimized, the "About Feature" section goes to the bottom of Settings - harder to find when there are lots of settings. We propose moving the "About Feature" section to the top of the Settings when minimized.

#### Design:

<img align="left" src="./images/FancyZones_extended_window_new.png" /> Extended window UI
<img align="left" src="./images/FancyZones_smaller_window_new.png" /> Smaller window UI 
>>>>>>> 44e3cb838e914bb1c99762e3aaa0dd01101128bb

#### Code:
The frontend and the backend for this window are in [onLaunchContentDialog.xaml](https://github.com/microsoft/PowerToys/blob/interns/dev-oobe/POC-OOBE/OOBE-Sandbox/PowerToys%20Settings%20Sandbox/Views/onLaunchContentDialog.xaml) and [onLaunchContentDialog.xaml.cs](https://github.com/microsoft/PowerToys/blob/interns/dev-oobe/POC-OOBE/OOBE-Sandbox/PowerToys%20Settings%20Sandbox/Views/onLaunchContentDialog.xaml.cs) respectively. It is later called in the [MainPage.xaml.cs](https://github.com/microsoft/PowerToys/blob/interns/dev-oobe/POC-OOBE/OOBE-Sandbox/PowerToys%20Settings%20Sandbox/Views/MainPage.xaml.cs) as below:

The following Grid would exist as each tool's Grid element on their repective settings page. Add styling as necessary. Check out '/PowerToys Settings Sanbox/Views/FancyZonesPage.xaml' to see it in action:

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
The aim of the feature is to introduce PowerToys to the user while at the same time not getting in their way. To achieve this we decided to use a [contentDialog](https://docs.microsoft.com/en-us/uwp/api/Windows.UI.Xaml.Controls.ContentDialog?view=winrt-19041). This allows the user to either explore PowerToys features as soon as they install/update the application or do the exploration at a later time. For users who chose the latter, The window privides animated gifs that give a glimpse of what PowerToys has to offer. Using a contentDialog also allows us to fire a toast notifcation at a later time.
=======
    <Grid>

        <VisualStateManager.VisualStateGroups>
            <VisualStateGroup x:Name="LayoutVisualStates">
                <VisualState x:Name="WideLayout">
                    <VisualState.StateTriggers>
                        <AdaptiveTrigger MinWindowWidth="{INSERT WIDE LAYOUT WINDOW WIDTH HERE}" />
                    </VisualState.StateTriggers>
                    <VisualState.Setters>
                        <Setter Target="SidePanel.(Grid.Column)" Value="1" />
                        <Setter Target="SidePanel.(Grid.Row)" Value="1" />
                        <Setter Target="ToolSettingsTitle.(Grid.Row)" Value="0" />
                        <Setter Target="ToolSettingsView.(Grid.Row)" Value="1" />
                    </VisualState.Setters>
                </VisualState>
                <VisualState x:Name="SmallLayout">
                    <VisualState.StateTriggers>
                        <AdaptiveTrigger MinWindowWidth="{INSERT MIN LAYOUT WINDOW WIDTH HERE}" />
                    </VisualState.StateTriggers>
                    <VisualState.Setters>
                        <Setter Target="SidePanel.(Grid.Column)" Value="0" />
                        <Setter Target="SidePanel.(Grid.Row)" Value="1" />
                        <Setter Target="SidePanel.(Orientation)" Value="Horizontal" />
                        <Setter Target="ToolSettingsView.(Grid.Row)" Value="2" />
                    </VisualState.Setters>
                </VisualState>
            </VisualStateGroup>
        </VisualStateManager.VisualStateGroups>


        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>


        <StackPanel x:Name="ToolSettingsTitle" Orientation="Vertical" >
                { INSERT TITLE AND DESCRIPTION HERE }
        </StackPanel>

        <StackPanel x:Name="SidePanel" Grid.Column="1" Orientation="Vertical" HorizontalAlignment="Left">
            <Image Source="{INSERT GIF OR IMAGE OF TOOL HERE}">
            <StackPanel x:Name="SidePanelText" Grid.Column="1" Orientation="Vertical" HorizontalAlignment="Left">
                { INSERT SIDE PANEL TEXT HERE }
            </StackPanel>
        </StackPanel>

        <StackPanel  x:Name="ToolSettingsView" Orientation="Vertical">
            { INSERT TOOL SETTINGS HERE }
        </StackPanel>
    </Grid>
```

