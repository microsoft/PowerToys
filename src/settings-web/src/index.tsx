import React from 'react';
import ReactDOM from 'react-dom';
import { App } from './components/App';
import { mergeStyles } from 'office-ui-fabric-react';

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
