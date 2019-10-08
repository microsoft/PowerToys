import React from 'react';
import {Stack, Text, Link} from 'office-ui-fabric-react';
import {BoolToggleSettingsControl} from './BoolToggleSettingsControl';
import {StringTextSettingsControl} from './StringTextSettingsControl';
import {IntSpinnerSettingsControl} from './IntSpinnerSettingsControl';
import {ColorPickerSettingsControl} from './ColorPickerSettingsControl';
import {CustomActionSettingsControl} from './CustomActionSettingsControl';
import {HotkeySettingsControl} from './HotkeySettingsControl';
import {ChoiceGroupSettingsControl} from './ChoiceGroupSettingsControl';
import {DropdownSettingsControl} from './DropdownSettingsControl';

export class ModuleSettings extends React.Component <any, any> {
  references: any;
  parent_on_change: Function;

  constructor(props: any) {
    super(props);
    this.references={};
    this.parent_on_change = props.on_change;
    this.state = {
      settings_key: props.settings_key,
      powertoy: props.powertoy,
    }
  }

  shouldComponentUpdate(nextProps:any, nextState:any) {
    // This component and its children manage their state.
    // React only to state changes when forceUpdate is called by the App component.
    return false;
  }

  componentWillReceiveProps(props: any) {
    this.setState({ powertoy: props.powertoy })
  }

  public get_data(): any {
    let properties : any = {};
    Object.keys(this.references).forEach(key => {
      properties[key]= this.references[key].get_value();
    });
    let result : any = {};
    result[this.state.settings_key] = {
      name: this.state.powertoy.name,
      properties:properties
    };
    return {powertoys: result};
  }

  private call_custom_action(action_name: any, action_values: any) {
    let result = {action: {
      [this.state.settings_key]: {
        action_name: action_name,
        value: action_values.value
      }
    }};
    (window as any).output_from_webview(JSON.stringify(result));
  }

  public render(): JSX.Element {
    let power_toys_properties = this.state.powertoy.properties;
    return (
      <Stack tokens={{childrenGap:20}}>
        <Stack>
          <Text variant='large'>{this.state.powertoy.description}</Text>
          {
            this.state.powertoy.hasOwnProperty('overview_link')
            ?
            <Stack horizontal tokens={{childrenGap:5}}>
              <Link
                styles = {{
                  root: {
                    alignSelf:'center'
                  }
                }}
                href={this.state.powertoy.overview_link}
                target='_blank'
              >Module overview</Link>
            </Stack>
            :
            null
          }
          {
            this.state.powertoy.hasOwnProperty('video_link')
            ?
            <Stack horizontal tokens={{childrenGap:5}}>
              <Link
                styles = {{
                  root: {
                    alignSelf:'center'
                  }
                }}
                href={this.state.powertoy.video_link} target='_blank'>Video demo</Link>
            </Stack>
            :
            null
          }
        </Stack>
        {
          Object.keys(power_toys_properties).
          sort(function(a, b) {
            return ( // Order powertoys settings
              (power_toys_properties[a].order || 0) -
              (power_toys_properties[b].order || 0)
            )
          }).
          map((key) => {
            switch(power_toys_properties[key].editor_type) {
              case 'bool_toggle':
                return <BoolToggleSettingsControl
                  setting={power_toys_properties[key]}
                  key={key}
                  on_change={this.parent_on_change}
                  ref={(input) => {this.references[key]=input;}}
                  />;
              case 'string_text':
                return <StringTextSettingsControl
                  setting = {power_toys_properties[key]}
                  key={key}
                  on_change={this.parent_on_change}
                  ref={(input) => {this.references[key]=input;}}
                  />;
              case 'int_spinner':
                return <IntSpinnerSettingsControl
                  setting = {power_toys_properties[key]}
                  key={key}
                  on_change={this.parent_on_change}
                  ref={(input) => {this.references[key]=input;}}
                  />;
              case 'color_picker':
                return <ColorPickerSettingsControl
                  setting = {power_toys_properties[key]}
                  key={key}
                  on_change={this.parent_on_change}
                  ref={(input) => {this.references[key]=input;}}
                  />;
              case 'custom_action':
                return <CustomActionSettingsControl
                  setting={power_toys_properties[key]}
                  action_name={key}
                  action_callback={(action_name: any, action_values:any) => {this.call_custom_action(action_name, action_values);} }
                  key={key}
                  ref={(input) => {this.references[key]=input;}}
                  />;
              case 'hotkey':
                return <HotkeySettingsControl
                  setting = {power_toys_properties[key]}
                  key={key}
                  on_change={this.parent_on_change}
                  ref={(input) => {this.references[key]=input;}}
                />;
              case 'choice_group':
                return <ChoiceGroupSettingsControl
                  setting = {power_toys_properties[key]}
                  key={key}
                  on_change={this.parent_on_change}
                  ref={(input) => {this.references[key]=input;}}
                />;
              case 'dropdown':
                return <DropdownSettingsControl
                  setting = {power_toys_properties[key]}
                  key={key}
                  on_change={this.parent_on_change}
                  ref={(input) => {this.references[key]=input;}}
                />;
              default:
                return null;
            }
          })
        }
        {/* An empty span to always give 30px padding in Edge. */}
        <span/>
      </Stack>
    )
  }
}
