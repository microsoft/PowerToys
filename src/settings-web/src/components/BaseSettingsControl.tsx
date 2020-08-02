import React from 'react';

export class BaseSettingsControl extends React.Component <any, any> {
  parent_on_change: Function;
  constructor(props:any) {
    super(props);
    this.parent_on_change=props.on_change;
  }
  public get_value():any {
    return null;
  }
}
