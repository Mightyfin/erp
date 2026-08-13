# syntax=docker/dockerfile:1
# Mightyfin ERP — HRM web frontend (.output of TanStack Start, node-server preset)
FROM node:22-alpine AS build
WORKDIR /app
COPY modules/hrm/frontend/module-connect/package.json modules/hrm/frontend/module-connect/pnpm-lock.yaml ./
RUN corepack enable && corepack prepare pnpm@latest --activate && pnpm install --frozen-lockfile --prefer-offline
COPY modules/hrm/frontend/module-connect/ ./
# Production build: SPA base URL = /api/hrm (proxied by nginx on the host).
ENV VITE_HRM_API_BASE=/api/hrm \
    VITE_USE_REAL_API=true \
    VITE_HRM_TENANT_ID=019ffa8b-0fb0-71e6-849a-f76e5a28e0b5
RUN pnpm build

FROM node:22-alpine AS runtime
ENV NODE_ENV=production \
    PORT=3000
WORKDIR /app
COPY --from=build /app/.output ./
EXPOSE 3000
CMD ["node", "server/index.mjs"]
