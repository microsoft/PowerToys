  // Your use of the content in the files referenced here is subject to the terms of the license at https://aka.ms/fabric-assets-license

// tslint:disable:max-line-length

import {
  IIconOptions,
  IIconSubset,
  registerIcons
} from '@uifabric/styling';

export function initializeIcons(
  baseUrl: string = '',
  options?: IIconOptions
): void {
  const subset: IIconSubset = {
    style: {
      MozOsxFontSmoothing: 'grayscale',
      WebkitFontSmoothing: 'antialiased',
      fontStyle: 'normal',
      fontWeight: 'normal',
      speak: 'none'
    },
    fontFace: {
      fontFamily: `"FabricMDL2Icons"`,
      // Inline Data, taken from ../css/fabric-icons-inline.css
      src: `url('data:application/octet-stream;base64,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') format('truetype');`
    },
    icons: {
      'GlobalNavButton': '\uE700',
      'ChevronDown': '\uE70D',
      'ChevronUp': '\uE70E',
      'Cancel': '\uE711',
      'Settings': '\uE713',
      'ChevronLeft': '\uE76B',
      'ChevronRight': '\uE76C',
      'ChevronUpSmall': '\uE96D',
      'ChevronDownSmall': '\uE96E',
      'ChevronLeftSmall': '\uE96F',
      'ChevronRightSmall': '\uE970',
      'ChevronUpMed': '\uE971',
      'ChevronDownMed': '\uE972',
      'ChevronLeftMed': '\uE973',
      'ChevronRightMed': '\uE974',
      'CircleRing': '\uEA3A'
    }
  };

  registerIcons(subset, options);
}
