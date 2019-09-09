import React from 'react';
import {BaseSettingsControl} from './BaseSettingsControl';
import { TextField } from 'office-ui-fabric-react';

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
