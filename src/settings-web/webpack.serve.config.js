const { webpackMerge, htmlOverlay, basicWebpackServeConfig, stylesOverlay, tsOverlay } = require('just-scripts');

// Overrides the Just file overlay so that SVGs can be used as a React Component.
powertoys_fileOverlay = {
  module: {
    rules: [
      {
        test: /\.(png|jpg|gif)$/,
        use: ['file-loader']
      },
      {
        test: /\.svg$/,
        use: ['@svgr/webpack']
      }
    ]
  }
};

module.exports = webpackMerge(basicWebpackServeConfig, htmlOverlay, stylesOverlay, tsOverlay, powertoys_fileOverlay);
