# PowerToys Out Of Box Experience Proposal and Code

### Developers: [Furaha Damién](https://github.com/furahadamien), [Letitia Kwan](https://github.com/letitiakwan), [Jessica Lim](https://github.com/JessicaLim8)
### Program Manager : [Eunice Choi](https://github.com/eunicechoi98)
### Designer : Rafael Flora

## Overview
A team of Microsoft Garage interns have created a proposal for the out-of-box experience after the first time install of the PowerToys app, and a proposal for the UI experience of updates.

## 1. Welcome Screen

#### Rationale:
PowerToys v17 has a silent launch after first time installation and update. As eidenced in issue #1285, users are left wondering if anything happened at all. The aim of this new feature is to allow users a chance to appreate the installing/update of PowerToys while at the same time giving them a chance to explore what PowerToys has to offer if they so wish to. 

#### Design:
<img align="left" src="./images/welcomeWindow.png" /> Welcome window


#### Code:

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

## 2. FancyZones Adaptive Sizing Layout Change

#### Rationale: 
The Settings update in v18 will introduce the "About Feature" side tab. When minimized, the "About Feature" section goes to the bottom of Settings - harder to find when there are lots of settings. We propose moving the "About Feature" section to the top of the Settings when minimized.

#### Design:

<img align="left" src="./images/FancyZones_extended_window_new.png" /> Extended window UI
<img align="left" src="./images/FancyZones_smaller_window_new.png" /> Smaller window UI 

#### Code:

```
test
```

#### Considerations: