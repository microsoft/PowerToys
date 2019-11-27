import React from 'react';
import { Stack, Text, PrimaryButton, Label, Link, loadTheme } from 'office-ui-fabric-react';
import { BoolToggleSettingsControl } from './BoolToggleSettingsControl'
import { ChoiceGroupSettingsControl } from './ChoiceGroupSettingsControl'
import { Separator } from 'office-ui-fabric-react/lib/Separator';
import { CustomActionSettingsControl } from './CustomActionSettingsControl';

export class GeneralSettings extends React.Component <any, any> {
  references: any = {};
  startup_reference: any;
  elevated_reference: any;
  restart_reference: any;
  theme_reference: any;
  parent_on_change: Function;
  constructor(props: any) {
    super(props);
    this.references={};
    this.startup_reference=null;
    this.elevated_reference=null;
    this.restart_reference=null;
    this.parent_on_change = props.on_change;
    this.state = {
      settings_key: props.settings_key,
      settings: props.settings,
    }
  }

  shouldComponentUpdate(nextProps:any, nextState:any) {
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
      run_elevated: this.elevated_reference.get_value().value,
      theme: this.theme_reference.get_value().value,
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
        <BoolToggleSettingsControl
          setting={{display_name: 'Run PowerToys with elevated privileges', value: this.state.settings.general.run_elevated}}
          on_change={this.parent_on_change}
          ref={(input) => {this.elevated_reference=input;}}
        />
        <CustomActionSettingsControl
          setting={{
            display_name: '',
            value: this.state.settings.general.is_elevated ? 
                   'PowerToys are currently running with elevated privileges.' :
                   'PowerToys are currently running without elevated privileges.',
            button_text: this.state.settings.general.is_elevated ? 
                          'Restart without elevated privileges' :
                          'Restart with elevated privileges'
          }}
          action_name={'restart_elevation'}
          action_callback={(action_name: any, value:any) => {
            (window as any).output_from_webview(JSON.stringify({
              action: {
                general: {
                  action_name,
                  value
                }
              }
            }));
          }}
          ref={(input) => {this.restart_reference=input;}}
        />
        <ChoiceGroupSettingsControl
          setting={{display_name: 'Chose Settings color',
                    value: this.state.settings.general.theme,
                    options: [
                      { key: 'system', text: 'System default app mode'},
                      { key: 'light', text: 'Light' },
                      { key: 'dark', text: 'Dark' }
                    ]}}
          on_change={() => {
            const dark_mode = this.theme_reference.get_value().value === 'dark' ||
                             (this.theme_reference.get_value().value === 'system' && this.state.settings.general.system_theme === 'dark');
            if (dark_mode) {
              loadTheme({
                palette: {
                  themePrimary: '#0088e4',
                  themeLighterAlt: '#000509',
                  themeLighter: '#001624',
                  themeLight: '#002944',
                  themeTertiary: '#005288',
                  themeSecondary: '#0078c8',
                  themeDarkAlt: '#1793e6',
                  themeDark: '#38a3ea',
                  themeDarker: '#69baef',
                  neutralLighterAlt: '#0b0b0b',
                  neutralLighter: '#151515',
                  neutralLight: '#252525',
                  neutralQuaternaryAlt: '#2f2f2f',
                  neutralQuaternary: '#373737',
                  neutralTertiaryAlt: '#595959',
                  neutralTertiary: '#eaeaea',
                  neutralSecondary: '#eeeeee',
                  neutralPrimaryAlt: '#f1f1f1',
                  neutralPrimary: '#e0e0e0',
                  neutralDark: '#f8f8f8',
                  black: '#fbfbfb',
                  white: '#000000',
                }
              });
            } else {
              loadTheme({
                palette: {
                  themePrimary: '#0078d4',
                  themeLighterAlt: '#f3f9fd',
                  themeLighter: '#d0e7f8',
                  themeLight: '#a9d3f2',
                  themeTertiary: '#5ca9e5',
                  themeSecondary: '#1a86d9',
                  themeDarkAlt: '#006cbe',
                  themeDark: '#005ba1',
                  themeDarker: '#004377',
                  neutralLighterAlt: '#f8f8f8',
                  neutralLighter: '#f4f4f4',
                  neutralLight: '#eaeaea',
                  neutralQuaternaryAlt: '#dadada',
                  neutralQuaternary: '#d0d0d0',
                  neutralTertiaryAlt: '#c8c8c8',
                  neutralTertiary: '#bab8b7',
                  neutralSecondary: '#a3a2a0',
                  neutralPrimaryAlt: '#8d8b8a',
                  neutralPrimary: '#323130',
                  neutralDark: '#605e5d',
                  black: '#494847',
                  white: '#ffffff',
                }
              });
            }
            this.parent_on_change();
          }}
          ref={(input) => {this.theme_reference=input;}}
        />
        <Stack>
        <Label>Version {this.state.settings.general.powertoys_version}</Label>
          <PrimaryButton
            styles={{
                root: {
                  alignSelf: "start"
                }
            }}
            href='https://github.com/microsoft/PowerToys/releases'
            target='_blank'
          >Check for updates</PrimaryButton>
        </Stack>
        {/* An empty span to always give 30px padding in Edge. */}
        <span/>
      </Stack>
    )
  }
}
