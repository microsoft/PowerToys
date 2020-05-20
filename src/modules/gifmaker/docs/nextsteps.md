# GIF Maker

## Animated GIF Recorder

PM: [Emma Gray](https://github.com/em-gray)
Devs: [Armianto Sumitro](https://github.com/armiantos), [Nancy Zhao](https://github.com/zhaonancy)

### Next Steps (Hand-Off)

#### Future Features

There were several features that didn’t fit into the scope of our hackathon timeline, and they’re outlined below:

| Future Feature                                          | Notes / Comments                                                                                                                                                      |
| ------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Launch on keyboard shortcut                             | - Users expect screen capture tools at the push of a hotkey or keyboard shortcut. This feature should definitely be a priority!                                       |
| GIF Editor – trim, crop, save copy                      | - Would be accessible from the “GIF Saved” toast at the end of capture, as well as through a right-click menu <br/> - See Appendix 5.1 for more information           |
| Region selectors – full screen, snap to window          | - Similar to Snip & Sketch functionality – our project only covers the drag and drop region selection use case.                                                       |
| Copy to clipboard in addition to save at stop recording | - A valuable Snip & Sketch feature that would be worth porting over. Helpful to users who value speed and convenience.                                                |
| Frame frequency control                                 | - Giving users control over the FPS of their GIF capture, which allows customization of smoothness and file size                                                      |
| Settings page                                           | - To customize launch hot key, compression, frame rate, etc.                                                                                                          |
| Countdown to delay on record start                      | - A countdown “3, 2, 1” could appear on pressing record before starting recording to give the user more time for preparation                                          |
| Multi-monitor support                                   | - Currently only works in primary display, can select region in secondary display but records same area in primary – can be solved by spawning overlay to all screens |

*Additional features beyond the scope of the hackathon.*

#### Bugs That Need Squashing

- Translate coordinates in scaled displays, which is already implemented in ScreenToGif; currently supports displays scaled at 100% only
- Handle case of click Capture → click Record → click Pause → click Record → click Stop; currently is unstable – saving notification pops up but app exits before save completes
- Handle case of click Capture → click Record without having selected any screen area; currently the application crashes as the rectangle is null 
- Pass controls to applications behind overlay; currently cannot interact with background applications unless in the selected rectangle area during recording 
- Adjust dark overlay when selecting capture region (ExclusionPath in RecordArea.xaml) to fit screen size instead the hardcoded 10000 x 10000 px2
- Refactor to make UI and controls behind more modular, MVVM pattern preferably
- GIF speed is slower than the actual content during recording, solution is to specify individual frame delays during GIF encoding
