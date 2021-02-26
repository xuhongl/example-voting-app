// Mapping the container environment variables defined in the pod/deployment definition file to JavaScript variables here.
// In your code you need to import this file, like f.e.: const keys = require('./keys');
// After that you can use the variables like this: var redisHost = keys.redisHost;
module.exports = {
  option_a: process.env.ENV_VAR_OPTION_A || 'Cats',
  option_b: process.env.ENV_VAR_OPTION_B || 'Dogs',
  appPort: process.env.ENV_VAR_RESULT_APP_PORT || 8080,
  pgHost: process.env.ENV_VAR_POSTGRES_HOST || 'db',
  pgPort: process.env.ENV_VAR_POSTGRES_PORT || '5432',
  pgDatabase: process.env.ENV_VAR_POSTGRES_DATABASE || 'postgres',
  pgUser: process.env.ENV_VAR_POSTGRES_USER || 'postgres_user',
  pgPassword: process.env.ENV_VAR_POSTGRES_PASSWORD || 'postgres_password'
};
