import React from 'react';
import {registerIcons} from 'office-ui-fabric-react';
import {initializeIcons} from './icons/src';

// Import SVG files for the icons here.
import FancyZonesSVG from './svg/fancy_zones.svg';
import PowerRenameSVG from './svg/power_rename.svg';
import ShortcutGuideSVG from './svg/shortcut_guide.svg';
import ImageResizerSVG from './svg/image_resizer.svg';
import PowerPreviewSVG from './svg/power_preview.svg';

export function setup_powertoys_icons(): void {
  initializeIcons('icons/fonts/');

  registerIcons({
    icons: {
      'pt-fancy-zones': ( <FancyZonesSVG /> ),
      'pt-power-rename': ( <PowerRenameSVG /> ),
      'pt-shortcut-guide': ( <ShortcutGuideSVG /> ),
      'pt-power-preview': (<PowerPreviewSVG /> ),
      'pt-image-resizer': ( <ImageResizerSVG />) ,
    }
  });
}
