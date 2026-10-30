# Build context: folder MyHelpDesk
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

# VITE_* di-embed saat build. Contoh:
#   docker build --build-arg VITE_API_URL=https://helpdesk.example.com/api -t myhelpdesk .
ARG VITE_API_URL
ARG VITE_IOT_API_URL

COPY . .

# Hanya tulis variabel yang diisi. String kosong tidak memicu fallback `??` di kode,
# jadi variabel kosong harus benar-benar tidak ada.
RUN : > .env.production \
    && if [ -n "$VITE_API_URL" ]; then echo "VITE_API_URL=$VITE_API_URL" >> .env.production; fi \
    && if [ -n "$VITE_IOT_API_URL" ]; then echo "VITE_IOT_API_URL=$VITE_IOT_API_URL" >> .env.production; fi \
    && npm run build

FROM nginx:alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
