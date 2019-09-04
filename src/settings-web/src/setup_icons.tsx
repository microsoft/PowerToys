import React from 'react';
import {registerIcons} from 'office-ui-fabric-react';
import {initializeIcons} from './icons/src';

// Import SVG files for the icons here.
import AnimatedGifRecorderSVG from './svg/animated_gif_recorder.svg';
import BatchRenamerSVG from './svg/batch_renamer.svg';
import FancyZonesSVG from './svg/fancy_zones.svg';
import ShortcutGuideSVG from './svg/shortcut_guide.svg';
import TerminateToolSVG from './svg/terminate_tool.svg';
import MaximizeNewDesktopSVG from './svg/maximize_new_desktop.svg';

export function setup_powertoys_icons(): void {
  initializeIcons('icons/fonts/');

  registerIcons({
    icons: {
      'pt-animated-gif-recorder': ( <AnimatedGifRecorderSVG /> ),
      'pt-batch-renamer': ( <BatchRenamerSVG /> ),
      'pt-fancy-zones': ( <FancyZonesSVG /> ),
      'pt-shortcut-guide': ( <ShortcutGuideSVG /> ),
      'pt-terminate-tool': ( <TerminateToolSVG /> ),
      'pt-maximize-new-desktop': ( <MaximizeNewDesktopSVG /> )
    }
  });
}
