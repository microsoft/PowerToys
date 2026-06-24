# Test Data Attribution

This directory contains test image files used for PowerRename metadata extraction unit tests. These images are sourced from Wikimedia Commons and are used under the Creative Commons licenses specified below.

## Test Files and Licenses

### Files from Carlseibert

**License:** [Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0)](https://creativecommons.org/licenses/by-sa/4.0/)

- `exif_test.jpg` - Uploaded by [Carlseibert](https://commons.wikimedia.org/wiki/File%3AMetadata_test_file_-_includes_data_in_IIM%2C_XMP%2C_and_Exif.jpg) on Wikimedia Commons
- `xmp_test.jpg` - Uploaded by [Carlseibert](https://commons.wikimedia.org/wiki/File%3AMetadata_test_file_-_includes_data_in_IIM%2C_XMP%2C_and_Exif.jpg) on Wikimedia Commons

### Files from Edward Steven

**License:** [Creative Commons Attribution-ShareAlike 2.0 Generic (CC BY-SA 2.0)](https://creativecommons.org/licenses/by-sa/2.0/)

- `exif_test_2.jpg` - Uploaded by [Edward Steven](https://commons.wikimedia.org/wiki/File%3AAreca_hutchinsoniana.jpg) on Wikimedia Commons
- `xmp_test_2.jpg` - Uploaded by [Edward Steven](https://commons.wikimedia.org/wiki/File%3AAreca_hutchinsoniana.jpg) on Wikimedia Commons

## Acknowledgments

We gratefully acknowledge the contributions of Carlseibert and Edward Steven for making these images available under Creative Commons licenses. Their work enables us to test metadata extraction functionality with real-world EXIF and XMP data.

## Usage

These test images are used in PowerRename's unit tests to verify correct extraction of:
- EXIF metadata (camera make, model, ISO, aperture, shutter speed, etc.)
- XMP metadata (creator, title, description, copyright, etc.)
- GPS coordinates
- Date/time information

## License Compliance

These test images are distributed as part of the PowerToys source code repository under their original Creative Commons licenses:
- Files from Carlseibert: CC BY-SA 4.0
- Files from Edward Steven: CC BY-SA 2.0

**Modifications:** These images have not been modified from their original versions downloaded from Wikimedia Commons. They are used in their original form for metadata extraction testing purposes.

**Distribution:** These test images are included in the PowerToys source repository and comply with the terms of their respective Creative Commons licenses through proper attribution in this file. While included in the source code, these images are not distributed in end-user installation packages or releases.

**Derivatives:** Any modifications or derivative works of these images must comply with the respective CC BY-SA license terms, including proper attribution and applying the same license to the modified versions.

For more information about Creative Commons licenses, visit: https://creativecommons.org/licenses/
