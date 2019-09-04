import React from 'react';
import {BaseSettingsControl} from './BaseSettingsControl';
import { ColorPicker, Stack, Label, getColorFromString, IColor } from 'office-ui-fabric-react';

export class ColorPickerSettingsControl extends BaseSettingsControl {
  colorpickerref:any = null;

  constructor(props:any) {
    super(props);
    this.colorpickerref = null;
    this.state={
      property_values: props.setting
    }
  }

  componentWillReceiveProps(props: any) {
    this.setState({ property_values: props.setting })
  }

  public get_value() : any {
    return {value: this.colorpickerref.color.str};
  }

  public render(): JSX.Element {
    let current_color : IColor | undefined = getColorFromString(this.state.property_values.value);
    return (
      <Stack>
        <Label>{this.state.property_values.display_name}</Label>
        <ColorPicker
          styles= {{
            panel: {padding:0}
          }}
          color={current_color===undefined?"#000000":current_color}
          componentRef= {(input) => {this.colorpickerref=input;}}
          alphaSliderHidden = {true}
          onChange = { () => {
              this.parent_on_change();
            }
          }
        />
      </Stack>
    );
  }

}