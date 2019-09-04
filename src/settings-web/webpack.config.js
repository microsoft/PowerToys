const path = require('path')
const { webpackMerge, basicWebpackConfig, stylesOverlay, tsOverlay } = require('just-scripts');

// Overrides the Just file overlay so that SVGs can be used as a React Component.
powertoys_fileOverlay = {
  output: {
    path: path.resolve(__dirname, 'build', 'dist')
  },
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

module.exports = webpackMerge(basicWebpackConfig, stylesOverlay, tsOverlay, powertoys_fileOverlay);
