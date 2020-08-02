import React from 'react';
import {Stack, Text, Nav, DefaultButton, PrimaryButton, ScrollablePane, INavLink, Spinner, SpinnerSize, Dialog, DialogType, DialogFooter, getTheme} from 'office-ui-fabric-react';
import {GeneralSettings} from './GeneralSettings';
import {ModuleSettings} from './ModuleSettings';
import '../css/layout.css';
import {setup_powertoys_icons} from '../setup_icons';

// Register fabric UI icons and powertoys logos as icons.
setup_powertoys_icons();

export class App extends React.Component <any, any> {
  settings_screen_ref:any;
  constructor(props: any) {
    super(props);
    this.settings_screen_ref = null;
    this.state = {
      data_changed : false,
      saving : false,
      selected_menu : 'general',
      target_selected_menu : '',
      show_save_discard_dialog: false,
      user_trying_to_exit: false,
      settings: {}
    }
  }

  public componentDidMount() {
    this.send_message_to_application(JSON.stringify({'refresh':true}));
  }

  public send_message_to_application(msg: string) {
    (window as any).output_from_webview(msg);
  }

  public receive_config_msg(config: any):void {
    let current_selected_menu = this.state.selected_menu;
    if(!config.hasOwnProperty('powertoys') || !config.powertoys.hasOwnProperty(current_selected_menu)) {
      current_selected_menu='general';
    }
    this.setState({
      settings: config,
      selected_menu: current_selected_menu,
      data_changed: false,
      saving: false,
      show_save_discard_dialog: false
    });
    if(this.settings_screen_ref) {
      this.settings_screen_ref.forceUpdate();
    }
  }

  public receive_exit_request(): void {
    // Settings application wants to close.
    // Prompt the user if there are possible unsaved changes.
    if(this.state.data_changed) {
      this.show_save_exit_dialog();
    } else {
      // Application can exit.
      this.send_message_to_application('exit');
    }
  }

  private show_save_exit_dialog = () : void => {
    this.setState({
      show_save_discard_dialog: true,
      user_trying_to_exit: true
    });
  }

  private on_setting_change = (): void => {
    this.setState({data_changed: true});
  }

  private show_save_discard_dialog = (target_selected_menu: string): void => {
    this.setState({
      target_selected_menu: target_selected_menu,
      show_save_discard_dialog: true,
      user_trying_to_exit: false
    });
  }

  private save_clicked = (): void => {
    // output_from_webview should be declared in index.html
    this.setState({saving : true});
    (window as any).output_from_webview(JSON.stringify(this.settings_screen_ref.get_data()));
  };

  private close_save_discard_dialog = (): void => {
    if (this.state.user_trying_to_exit) {
      this.send_message_to_application('cancel-exit');
    }
    this.setState({ show_save_discard_dialog: false });
  };
  private save_save_discard_dialog = (): void => {
    if (this.state.user_trying_to_exit) {
      this.send_message_to_application('cancel-exit');
    }
    this.setState({ show_save_discard_dialog: false });
    this.save_clicked();
  };
  private discard_save_discard_dialog = (): void => {
    if (this.state.user_trying_to_exit) {
      this.send_message_to_application('exit');
    }
    this.setState({
      show_save_discard_dialog: false,
      selected_menu: this.state.target_selected_menu,
      data_changed: false,
      saving: false
    });
  };

  public render(): JSX.Element {
    const powertoys_dict = this.state.settings.powertoys;
    let powertoys_links = [];
    for(let powertoy_key in powertoys_dict) {
      if(powertoys_dict.hasOwnProperty(powertoy_key)) {
        powertoys_links.push({
          name: powertoys_dict[powertoy_key].name,
          key: powertoy_key,
          url:'',
          icon: powertoys_dict[powertoy_key].icon_key || 'CircleRing'
        });
      }
    }
    const theme = getTheme()
    return (
      <div className='body' style={{stroke: theme.palette.black}}>
        <div className='sidebar' style={{backgroundColor: theme.palette.neutralLighter, color: theme.palette.black}}>
          <Nav
            selectedKey= {this.state.selected_menu}
            onLinkClick = {
              (ev?: React.MouseEvent<HTMLElement,MouseEvent>, item?: INavLink) => {
                let item_menu_key: string|null = ((item && item.key)||null);
                if(item_menu_key && item_menu_key!=this.state.selected_menu) {
                  if(this.state.data_changed) {
                    // There are data changes. Don't change screen until the user confirms.
                    ev&&ev.preventDefault();
                    this.show_save_discard_dialog(item_menu_key);
                  } else {
                    this.setState({selected_menu : item_menu_key});
                  }
                }
              }
            }
            styles = {{
              navItems: { margin : '0'},
              compositeLink: {
                backgroundColor : theme.palette.neutralLighter,
                color: theme.palette.neutralPrimary,
                selectors: {
                  '&.is-selected button' : {
                    backgroundColor: theme.palette.neutralLight,
                    color: theme.palette.neutralPrimaryAlt,
                    fontWeight: 'bold'
                  },
                  '&:hover button.ms-Nav-link' : {
                    backgroundColor: theme.palette.neutralLight,
                    color: theme.palette.neutralPrimary
                  },
                  'i.ms-Button-icon' : {
                    color: theme.palette.neutralPrimary,
                    fontWeight: 'normal',
                    paddingLeft: '5px',
                    paddingRight: '5px'
                  },
                  '.ms-Button-icon > svg' : {
                    paddingTop: '2px'
                  },
                  '&:hover i.ms-Button-icon' : {
                    color: theme.palette.neutralPrimary,
                  },
                  '&:active i.ms-Button-icon' : {
                    color: theme.palette.neutralPrimary,
                  },
                },
              },
            }}
            groups = {[
              {
                links: [
                  { name: 'General Settings', key:'general', url:'', icon: 'Settings' }
                ].
                concat(powertoys_links)
              }
            ]}
          />
        </div>
        <div className='editorzone' style={{backgroundColor: theme.palette.white, color: theme.palette.black}}>
          <div className='editorhead'>
            <div className='editortitle'>
              <Text
                variant='xxLarge'
                styles= {{ root: { display:'block', whiteSpace:'no-wrap', overflow:'hidden', textOverflow:'ellipsis' }}}
              >
                { this.state.selected_menu!='general' ?
                  powertoys_dict[this.state.selected_menu].name + " Settings" :
                  "PowerToys General Settings"
                }
              </Text>
            </div>
            <div className='editorheadbuttons'>
              <Stack horizontal={true} tokens={{childrenGap:16}}>
                <PrimaryButton
                  styles={{
                    root: {
                      minWidth: '100px'
                    }
                  }}
                  disabled = { (!this.state.data_changed) || this.state.saving}
                  text= {this.state.saving?'Saving':'Save'}
                  onClick={this.save_clicked}
                  >
                    {this.state.saving ? <Spinner size={SpinnerSize.small}/> : <span/>}
                  </PrimaryButton>
              </Stack>
            </div>
          </div>
          <div className='editorbody'>
            <ScrollablePane
            styles= {{
              contentContainer: {
                paddingTop: '16px',
                paddingLeft: '16px',
                paddingRight: '16px'
                // padding bottom will be applied by an empty span in the contents, for edge compatibility.
              }
            }}
            >
            {
              (() => {
                if(this.state.selected_menu === 'general' && this.state.settings.hasOwnProperty('general')) {
                  return <GeneralSettings
                    key="general"
                    settings_key="general"
                    settings={this.state.settings}
                    on_change={this.on_setting_change}
                    ref={(input:any) => {this.settings_screen_ref = input;}}
                  />
                } else if( this.state.settings.hasOwnProperty('powertoys') && this.state.selected_menu in this.state.settings.powertoys) {
                  return <ModuleSettings
                    key={this.state.selected_menu}
                    settings_key={this.state.selected_menu}
                    powertoy={this.state.settings.powertoys[this.state.selected_menu]}
                    on_change={this.on_setting_change}
                    ref={(input:any) => {this.settings_screen_ref = input;}}
                    />
                }
              })()
            }
            </ScrollablePane>
          </div>
        </div>
        <Dialog
          hidden={!this.state.show_save_discard_dialog}
          onDismiss={this.close_save_discard_dialog}
          dialogContentProps={{
            type: DialogType.normal,
            title: 'Changes not saved',
            subText: this.state.user_trying_to_exit ?
              'Would you like to save your changes or exit the settings?' :
              'Would you like to save or discard your changes?'
          }}
          modalProps={{
            isBlocking: true,
            styles: { main: { maxWidth: 450 } }
          }}
        >
          <DialogFooter
            styles={{
              actionsRight: {
                textAlign:'center'
                }
              }}
            >
            <PrimaryButton onClick={this.save_save_discard_dialog} text="Save" />
            <PrimaryButton
              onClick={this.discard_save_discard_dialog}
              text={this.state.user_trying_to_exit ? "Exit" : "Discard"}
              />
            <DefaultButton onClick={this.close_save_discard_dialog} text="Cancel" />
          </DialogFooter>
        </Dialog>
      </div>
    );
  }
};
