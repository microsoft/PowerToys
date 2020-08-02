import React from 'react';
import {BaseSettingsControl} from './BaseSettingsControl';
import { SpinButton } from 'office-ui-fabric-react';
import { Position } from 'office-ui-fabric-react/lib/utilities/positioning';

export class IntSpinnerSettingsControl extends BaseSettingsControl {
  spinbuttonref:any = null;

  constructor(props:any) {
    super(props);
    this.spinbuttonref = null;
    this.state={
      property_values: props.setting
    }
  }

  componentWillReceiveProps(props: any) {
    this.setState({ property_values: props.setting });
  }

  public get_value() : any {
    return {value: parseInt(this.spinbuttonref.value)};
  }

  public render(): JSX.Element {
    return (
      <SpinButton
        styles= {{
          spinButtonWrapperTopBottom: {
            maxWidth:'250px',
            alignSelf: 'start'
          },
          input: {
            // The input area of the SpinButton overlaps the border, causing
            // graphical issues depending on the Display scaling settings.
            // Removing background color fixes the graphical issues.
            backgroundColor: 'transparent',
          },
        }}
        value={this.state.property_values.value}
        onValidate={(value: string) => {
          if(value.trim().length === 0 || isNaN(+value)) {
            value=String(this.state.property_values.value);
          } else if (Number(value)<this.spinbuttonref.props.min) {
            value=String(this.spinbuttonref.props.min);
          } else if (Number(value)>this.spinbuttonref.props.max) {
            value=String(this.spinbuttonref.props.max);
          }
          this.setState( (prev_state:any) => ({
            property_values: {
              ...(prev_state.property_values),
              value: parseInt(value)
            }
          }));
          this.parent_on_change();
          return value;
        }}
        onIncrement={(value: string) => {
          if (Number(value) + this.spinbuttonref.props.step > this.spinbuttonref.props.max) {
            value = String(this.spinbuttonref.props.max);
          } else {
            value = String(+value + this.spinbuttonref.props.step) ;
          }
          this.setState( (prev_state:any) => ({
            property_values: {
              ...(prev_state.property_values),
              value: parseInt(value)
            }
          }));
          this.parent_on_change();
          return value;
        }}
        onDecrement={(value: string) => {
          if (Number(value) - this.spinbuttonref.props.step < this.spinbuttonref.props.min) {
            value = String(this.spinbuttonref.props.min);
          } else {
            value = String(+value - this.spinbuttonref.props.step) ;
          }
          this.setState( (prev_state:any) => ({
            property_values: {
              ...(prev_state.property_values),
              value: parseInt(value)
            }
          }));
          this.parent_on_change();
          return value;
        }}
        precision={0}
        step={this.state.property_values.step || 1}
        min={this.state.property_values.min || 0}
        max={this.state.property_values.max || 999999999}
        label={this.state.property_values.display_name}
        labelPosition={Position.top}
        componentRef= {(input) => {this.spinbuttonref=input;}}
      />
    );
  }
}
