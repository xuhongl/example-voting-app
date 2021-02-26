FROM node:10-slim

WORKDIR /app

RUN npm install -g nodemon

COPY ./src/result-nodejs/package*.json ./

RUN npm ci \
 && npm cache clean --force \
 && mv /app/node_modules /node_modules

COPY ./src/result-nodejs .

ENV PORT 80

EXPOSE 80

CMD ["node", "server.js"]
