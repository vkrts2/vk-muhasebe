module.exports = function(api) {
  api.cache(true);
  const isTest = process.env.NODE_ENV === 'test' || process.env.JEST_WORKER_ID !== undefined;
  if (isTest) {
    return {
      presets: [
        'babel-preset-jest',
        '@babel/preset-typescript'
      ],
      plugins: [
        '@babel/plugin-transform-modules-commonjs'
      ]
    };
  }
  return {
    presets: ['babel-preset-expo']
  };
};
