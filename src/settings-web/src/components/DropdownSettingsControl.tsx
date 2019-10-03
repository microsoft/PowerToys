import React from 'react';
import { BaseSettingsControl } from './BaseSettingsControl';
import { Dropdown } from 'office-ui-fabric-react';

export class DropdownSettingsControl extends BaseSettingsControl {
  dropref:any = null; // Keeps a reference to the corresponding item in the DOM.

  constructor(props:any) {
    super(props);
    this.dropref = null;
    this.state = {
      property_values: props.setting
    }
  }

  componentWillReceiveProps(props: any) {
    // Fully controlled component.
    // Reacting to a property change so that the control is redrawn properly.
    this.setState({ property_values: props.setting })
  }

  public get_value() : any {
    if (this.dropref.selectedOptions.length === 0) {
      return null;
    } else {
      return {'value': this.dropref.selectedOptions[0].key};
    }
  }

  public render(): JSX.Element {
    return (
      <Dropdown
        styles={{
          root:{
            width: '350px',
            alignSelf: 'start'
          }}}
        defaultSelectedKey={this.state.property_values.value}
        options={this.state.property_values.options}
        label={this.state.property_values.display_name}
        componentRef={(element) => {this.dropref=element;}}
        onChange={()=>{
          this.parent_on_change();
        }}
      />
    );
  }
}
