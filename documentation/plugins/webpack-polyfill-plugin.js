module.exports = function () {
  return {
    name: 'webpack-polyfill-plugin',
    configureWebpack(config, isServer, utils) {
      return {
        resolve: {
          fallback: {
            stream: require.resolve('stream-browserify'),
          },
        },
      };
    },
  };
};
