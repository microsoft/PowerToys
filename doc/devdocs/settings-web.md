## Build Commands

Here are the commands to build and test this project:

### To start the development server

```
npm install
npm run start
```

### Building and integrating into PowerToys settings project

```
npm run build
```

## Updating the icons

Icons inside [`src/icons/`](/src/settings-web/src/icons/) were generated from the [Office UI Fabric Icons subset generation tool.](https://uifabricicons.azurewebsites.net/)

In case the subset needs to be changed, additional steps are needed to include the icon font in the built `dist/bundle.js`:
- Copy the inline font data taken from [`src/icons/css/fabric-icons-inline.css`](src/icons/css/fabric-icons-inline.css) and place it in the `fontFace` `src` value in [`src/icons/src/fabric-icons.ts`](src/icons/src/fabric-icons.ts).

A list of the current icons in the subset can be seen in the `icons` object in [`src/icons/src/fabric-icons.ts`](src/icons/src/fabric-icons.ts).

SVG icons, including the icons for each PowerToy listed in the Settings, are contained in [`src/svg/`](src/svg/). To add additional SVG icons add them to [`src/svg/`](src/svg/) and register them in [`src/setup_icons.tsx`](src/setup_icons.tsx).

## Code Structure

The project structure is based on the [`UI Fabric` scaffold](https://developer.microsoft.com/en-us/fabric#/get-started/web#option-1-quick-start) obtained by initializing it with `npm init uifabric`.

#### [index.html](/src/settings-web/index.html)
The HTML entry-point of the project.
Loads the `ReactJS` distribution script.
Defines JavaScript functions to receive and send messages to the [PowerToys Settings](/src/editor) window.

#### [src/index.tsx](/src/settings-web/src/index.tsx)
Main `ReactJS` entrypoint, initializing the `ReactDOM`.

#### [src/setup_icons.tsx](/src/settings-web/src/setup_icons.tsx)
Defines the `setup_powertoys_icons` function that registers the icons to be used in the components.

#### [src/components/](/src/settings-web/src/components/)
Contains the `ReactJS` components, including the Settings controls for each type of setting.

#### [src/components/App.tsx](/src/settings-web/src/components/App.tsx)
Defines the main App component, containing the UI layout, navigation menu, dialogs and main load/save logic.

#### [src/components/GeneralSettings.tsx](/src/settings-web/src/components/GeneralSettings.tsx)
Defines the PowerToys General Settings component, including logic to construct the object sent to PowerToys to change the General settings.

#### [src/components/ModuleSettings.tsx](/src/settings-web/src/components/ModuleSettings.tsx)
Defines the component that generates the settings screen for a PowerToy depending on its settings definition.

#### [src/components/BaseSettingsControl.tsx](/src/settings-web/src/components/BaseSettingsControl.tsx)
Defines the base class for a Settings control.

#### [src/css/layout.css](/src/settings-web/src/css/layout.css)
General layout styles.

#### [src/icons/](/src/settings-web/src/icons/)
Icons generated from the [Office UI Fabric Icons subset generation tool.](https://uifabricicons.azurewebsites.net/)

#### [src/svg/](/src/settings-web/src/svg/)
SVG icon assets.

## Creating a new settings control

The [`BaseSettingsControl` class](/src/settings-web/src/components/BaseSettingsControl.tsx) can be extended to create a new Settings control type.

```tsx
export class BaseSettingsControl extends React.Component <any, any> {
  parent_on_change: Function;
  constructor(props:any) {
    super(props);
    this.parent_on_change=props.on_change;
  }
  public get_value():any {
    return null;
  }
}
```

A settings control overrides the `get_value` function to return the value to be used for the Setting the control is representing.
It will use the `parent_on_change` property to signal that the user made some changes to the settings.

Here's the [`StringTextSettingsControl`](/src/settings-web/src/components/StringTextSettingsControl.tsx) component to serve as an example:

```tsx
export class StringTextSettingsControl extends BaseSettingsControl {
  textref:any = null; // Keeps a reference to the corresponding TextField in the DOM.

  constructor(props:any) {
    super(props);
    this.textref = null;
    this.state={
      property_values: props.setting
    }
  }

  componentWillReceiveProps(props: any) {
    // Fully controlled component.
    // Reacting to a property change so that the control is redrawn properly.
    this.setState({ property_values: props.setting })
  }

  public get_value() : any {
    // Returns the TextField value.
    return {value: this.textref.value};
  }

  public render(): JSX.Element {
    // Renders a UI Fabric TextField.
    return (
      <TextField
        onChange = {
          (_event,_new_value) => {
            // Updates the state with the new value introduced in the TextField.
            this.setState( (prev_state:any) => ({
                property_values: {
                  ...(prev_state.property_values),
                  value: _new_value
                }
              })
            );
            // Signal the parent that the user changed a value.
            this.parent_on_change();
          }
        }
        value={this.state.property_values.value}
        label={this.state.property_values.display_name}
        componentRef= {(input) => {this.textref=input;}}
      />
    );
  }
}
```

Each settings property has a `editor_type` field that's used to differentiate between the Settings control types:
```js
'test string_text': {
  display_name: 'This is what a string_text looks like',
  editor_type: 'string_text',
  value: 'A sample string value'
}
```

A new Settings control component can be added to [`src/components/`](/src/settings-web/src/components/).
To render the new Settings control, its `editor_type` and component instance need to be added to the [`ModuleSettings` component render()](/src/settings-web/src/components/ModuleSettings.tsx):
```tsx
import React from 'react';
import {StringTextSettingsControl} from './StringTextSettingsControl';

...

export class ModuleSettings extends React.Component <any, any> {
  references: any;
  parent_on_change: Function;
...
  public render(): JSX.Element {
    let power_toys_properties = this.state.powertoy.properties;
    return (
      <Stack tokens={{childrenGap:20}}>
...
        {
          Object.keys(power_toys_properties).
          map( (key) => {
            switch(power_toys_properties[key].editor_type) {
...
              case 'string_text':
                return <StringTextSettingsControl
                  setting = {power_toys_properties[key]}
                  key={key}
                  on_change={this.parent_on_change}
                  ref={(input) => {this.references[key]=input;}}
                  />;
...
```
