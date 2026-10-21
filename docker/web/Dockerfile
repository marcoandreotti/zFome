FROM node:22.22-alpine AS build
WORKDIR /app
COPY web/ZFome.Web/package*.json ./
RUN npm ci --ignore-scripts
COPY web/ZFome.Web/ ./
RUN npm run postinstall
ARG VITE_API_BASE_URL=http://localhost:8080
ARG VITE_REALTIME_BASE_URL
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV VITE_REALTIME_BASE_URL=$VITE_REALTIME_BASE_URL
RUN npm run build

FROM nginx:1.29-alpine
COPY docker/web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/spa /usr/share/nginx/html
EXPOSE 80
