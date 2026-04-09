FROM node:20-alpine AS build
WORKDIR /app

COPY frontend/package.json frontend/package-lock.json ./frontend/
WORKDIR /app/frontend
RUN npm ci

WORKDIR /app
COPY frontend ./frontend
RUN npm run build --prefix frontend

FROM nginx:1.29-alpine
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY docker/web-entrypoint.sh /docker-entrypoint.d/99-config.sh
COPY --from=build /app/frontend/dist /usr/share/nginx/html
RUN chmod +x /docker-entrypoint.d/99-config.sh
EXPOSE 8080
