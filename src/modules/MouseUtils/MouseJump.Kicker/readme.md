## MouseJump.Kicker

This is a simple "kicker" app that can be used to launch the Mouse Jump background process
and activate the hotkey without needing to build and run the entire "runner" app.

It's intended to be used for testing and development purposes, and is not meant to be
included in the main PowerToys release footprint.

When launching Mouse Jump via the kicker, it will look in the build output folder and
launch the last successful build. Make sure to build the Mouse Jump project before
launching the kicker, otherwise it won't run the latest version of Mouse Jump.