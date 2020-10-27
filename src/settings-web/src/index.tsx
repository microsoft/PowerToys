import React from 'react';
import ReactDOM from 'react-dom';
import { App } from './components/App';
import { loadTheme, mergeStyles } from 'office-ui-fabric-react';

if ((window as any).start_with_dark_theme) {
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
}
// Inject some global styles
mergeStyles({
  selectors: {
    ':global(body), :global(html), :global(#app)': {
      margin: 0,
      padding: 0,
      height: '100vh'
    }
  }
});

const root = document.getElementById('app');
if (root && root.hasChildNodes()) {
  ReactDOM.hydrate(<App
    ref={(app_component) => {(window as any).react_app_component=app_component;}} // in order to call the app from outside react.
    />,
    root);
} else {
  ReactDOM.render(<App
    ref={(app_component) => {(window as any).react_app_component=app_component;}} // in order to call the app from outside react.
    />,
    root);
}
