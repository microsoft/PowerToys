import React from 'react';
import {BaseSettingsControl} from './BaseSettingsControl';
import { Toggle } from 'office-ui-fabric-react';

export class BoolToggleSettingsControl extends BaseSettingsControl {
  toggleref:any = null;

  constructor(props:any) {
    super(props);
    this.toggleref = null;
    this.state={
      property_values: props.setting
    }
  }

  componentWillReceiveProps(props: any) {
    this.setState({ property_values: props.setting })
  }


  public get_value() : any {
    return {value: this.toggleref.checked};
  }

  public render(): JSX.Element {
    return (
      <Toggle
        onChange={
          (_event,_check) => { 
            this.setState( (prev_state:any) => ({
                property_values: { 
                  ...(prev_state.property_values),
                  value: _check
                }
              })
            );
            this.parent_on_change();
          }
        }
        checked={this.state.property_values.value}
        label={this.state.property_values.display_name}
        onText="On"
        offText="Off"
        componentRef= {(input) => {this.toggleref=input;}}
      />
    );
  }

}