import React from 'react';
import { BaseSettingsControl } from './BaseSettingsControl';
import { ChoiceGroup } from 'office-ui-fabric-react';

export class ChoiceGroupSettingsControl extends BaseSettingsControl {
  choiceref:any = null; // Keeps a reference to the corresponding item in the DOM.

  constructor(props:any) {
    super(props);
    this.choiceref = null;
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
    return {'value': this.choiceref.checkedOption.key};
  }

  public render(): JSX.Element {
    return (
      <ChoiceGroup
        className="defaultChoiceGroup"
        defaultSelectedKey={this.state.property_values.value}
        options={this.state.property_values.options}
        label={this.state.property_values.display_name}
        componentRef={(element) => {this.choiceref=element;}}
        onChange={()=>{
          this.parent_on_change();
        }}
      />
    );
  }
}
