# Developer Command Palette

Also known as "Developer Search"

Also known as "WSB" <!-- 🚀💎🚀 -->

Also known as "devcmdpal"

Also known as "Dev Pal"

## Building

Right now there are two separate slns that need to be build. The first is the
`DeveloperCommandPalette.sln` solution which is the main dev pal app. The second
is the `extensions\SampleExtension.sln` solution which has a bunch of test
extensions in it.

```
start DeveloperCommandPalette.sln
start extensions\SampleExtension.sln
```

You may also need to manually

```
nuget restore DeveloperCommandPalette.sln
```

cause sometimes VS is just daft when it comes to C++ projects using nuget.
