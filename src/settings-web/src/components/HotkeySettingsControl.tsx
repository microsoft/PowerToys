import React from 'react';
import {BaseSettingsControl} from './BaseSettingsControl';
import { TextField } from 'office-ui-fabric-react';

function makeDisplayValue(value : any) : string {
  if (!value) {
    return '(none)';
  }
  let keyparts = [];
  if (value.win) {
    keyparts.push('Win');
  }
  if (value.ctrl) {
    keyparts.push('Ctrl');
  }
  if (value.alt) {
    keyparts.push('Alt');
  }
  if (value.shift) {
    keyparts.push('Shift');
  }
  keyparts.push(value.key);
  return keyparts.join(' + ');
}

export class HotkeySettingsControl extends BaseSettingsControl {
  textref:any = null; // Keeps a reference to the corresponding TextField in the DOM.

  constructor(props:any) {
    super(props);
    this.textref = null;
    this.state = {
      property_values: props.setting,
      display_value : makeDisplayValue(props.setting.value)
    }
  }

  componentWillReceiveProps(props: any) {
    // Fully controlled component.
    // Reacting to a property change so that the control is redrawn properly.
    this.setState({ property_values: props.setting })
  }

  public get_value() : any {
    // Returns the TextField value.
    return {value: this.state.property_values.value};
  }

  public render(): JSX.Element {
    // Renders a UI Fabric TextField.
    return (
      <TextField
        styles={{ fieldGroup: {
          width: '350px',
          alignSelf: 'start'
        }}}
        onKeyDown = {
          (_event) => {
            _event.preventDefault();
            if (_event.key === 'Meta' ||
                _event.key === 'Control' ||
                _event.key === 'Shift' ||
                _event.key === 'Alt') {
              return;
            }
            let new_value = {
              win : _event.metaKey,
              ctrl : _event.ctrlKey,
              alt : _event.altKey,
              shift : _event.shiftKey,
              key : _event.key,
              code : _event.keyCode,
            };
            if (new_value.key === ' ') {
              new_value.key = 'Space';
            }
            if (new_value.key === '~~') {
              new_value.key = '~';
            }
            if (!new_value.key || new_value.key === 'Unidentified') {
              new_value.key = `(Key ${new_value.code})`;
            }
            if (new_value.key.length === 1) {
              new_value.key = new_value.key.toLocaleUpperCase();
            }
            this.setState( (prev_state:any) => ({
                property_values: {
                  ...(prev_state.property_values),
                  value: new_value
                },
                display_value: makeDisplayValue(new_value)
              })
            );
            
            this.parent_on_change();
          }
        }
        onKeyUp = {() => {}}
        value={this.state.display_value}
        label={this.state.property_values.display_name}
        componentRef= {(input) => {this.textref=input;}}
      />
    );
  }
}
