import React from 'react';
import { Stack, Text, DefaultButton, Label, Link} from 'office-ui-fabric-react';
import {BoolToggleSettingsControl} from './BoolToggleSettingsControl'
import { Separator } from 'office-ui-fabric-react/lib/Separator';

export class GeneralSettings extends React.Component <any, any> {
  references: any = {};
  startup_reference: any;
  parent_on_change: Function;
  constructor(props: any) {
    super(props);
    this.references={};
    this.startup_reference=null;
    this.parent_on_change = props.on_change;
    this.state = {
      settings_key: props.settings_key,
      settings: props.settings,
    }
  }
  shouldComponentUpdate(nextProps:any, nextState:any)
  {
    // This component and its children manage their state.
    // React only to state changes when forceUpdate is called by the App component.
    return false;
  }
  componentWillReceiveProps(props: any) {
    this.setState({ settings: props.settings })
  }

  public get_data(): any {
    let enabled : any = {};
    Object.keys(this.references).forEach(key => {
      enabled[key]=this.references[key].get_value().value;
    });
    let result : any = {};
    result[this.state.settings_key]= {
      startup: this.startup_reference.get_value().value,
      enabled: enabled
    };
    return result;
  }

  public render(): JSX.Element {
    let power_toys_enabled = this.state.settings.general.enabled;
    return (
      <Stack tokens={{childrenGap:20}}>
        <Text variant='xLarge'>Available PowerToys</Text>
        { Object.keys(power_toys_enabled).map(
          (key) => {
            let enabled_value=power_toys_enabled[key];
            return <Stack key={key}>
              <Stack horizontal tokens={{childrenGap:5}}>
                <Label>{key}</Label>
                {(
                  this.state.settings.powertoys &&
                  this.state.settings.powertoys.hasOwnProperty(key) &&
                  this.state.settings.powertoys[key].hasOwnProperty('overview_link'))
                  ?
                  <Link
                    styles = {{
                      root: {
                        alignSelf:'center'
                      }
                    }}
                    href={this.state.settings.powertoys[key].overview_link}
                    target='_blank'
                  >(Overview)</Link>
                  :
                  null
                }
                {(
                  this.state.settings.powertoys &&
                  this.state.settings.powertoys.hasOwnProperty(key) &&
                  this.state.settings.powertoys[key].hasOwnProperty('video_link'))
                  ?
                  <Link
                    styles = {{
                      root: {
                        alignSelf:'center'
                      }
                    }}
                    href={this.state.settings.powertoys[key].video_link}
                    target='_blank'
                  >(Video)</Link>
                  :
                  null
                }
              </Stack>
              {(
                this.state.settings.powertoys &&
                this.state.settings.powertoys.hasOwnProperty(key) &&
                this.state.settings.powertoys[key].hasOwnProperty('description'))
                ?
                <Text
                  styles = {{
                    root: {
                      paddingBottom: '5px'
                    }
                  }}
                >{this.state.settings.powertoys[key].description}</Text>
                :
                null
              }
              <BoolToggleSettingsControl
                setting={{value: enabled_value}}
                on_change={this.parent_on_change}
                ref={(input) => {this.references[key]=input;}}
              />
            </Stack>;
          })
        }
        <Separator />
        <Text variant='xLarge'>General</Text>
        <BoolToggleSettingsControl
          setting={{display_name: 'Start at login', value: this.state.settings.general.startup}}
          on_change={this.parent_on_change}
          ref={(input) => {this.startup_reference=input;}}
          />
        <Stack>
        <Label>Version 0.11.0</Label>
          <DefaultButton
            styles={{
                root: {
                  backgroundColor: "#FFFFFF",
                  alignSelf: "start"
                }
            }}
            href='https://github.com/microsoft/PowerToys/releases'
            target='_blank'
          >Check for updates</DefaultButton>
        </Stack>
        {/* An empty span to always give 30px padding in Edge. */}
        <span/>
      </Stack>
    )
  }
}
