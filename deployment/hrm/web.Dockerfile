# syntax=docker/dockerfile:1
# Mightyfin ERP — HRM web frontend (TanStack Start SSR)
# Follows host conventions: image tag :local, built from the cloned repo.
#
# M50.16d: TanStack Start's rolldown build emits the server as a plain Web
# fetch handler with no listen wrapper (server/server.js alongside its chunk
# assets). The removed Lovable vite wrapper used to inject one; we now ship a
# tiny project-local entry (server-entry.js) that starts a Node HTTP server
# on PORT and delegates every request to the generated fetch handler.

FROM node:22-alpine AS build
WORKDIR /app
COPY modules/hrm/frontend/module-connect/package.json modules/hrm/frontend/module-connect/pnpm-lock.yaml ./
RUN corepack enable && corepack prepare pnpm@latest --activate && pnpm install --frozen-lockfile --prefer-offline
COPY modules/hrm/frontend/module-connect/ ./
# Production build: API base = /api (all client routes already carry the
# /hrm/... prefix, so resolved paths are /api/hrm/..., proxied by nginx on the
# host), tenant defaulted to the ZML legal entity.
ENV VITE_HRM_API_BASE=/api \
    VITE_USE_REAL_API=true \
    VITE_HRM_TENANT_ID=019ffa8b-0fb0-71e6-849a-f76e5a28e0b5
# The TanStack build writes to .output: client/ (static files + assets) and
# server/ (server.js fetch handler + chunk assets).
RUN pnpm build

FROM node:22-alpine AS runtime
ENV NODE_ENV=production \
    PORT=3000
WORKDIR /app
# M50.16e: Rolldown keeps `react` / `@tanstack/*` as external specifiers in
# the server chunks, so node_modules must exist at runtime to resolve them.
COPY modules/hrm/frontend/module-connect/package.json modules/hrm/frontend/module-connect/pnpm-lock.yaml ./
RUN corepack enable && corepack prepare pnpm@latest --activate && \
    pnpm install --frozen-lockfile --prefer-offline --prod 2>/dev/null || \
    pnpm install --frozen-lockfile --prefer-offline
# M50.16d/16e: .output holds the production build. The SSR fetch handler lives
# in /app/server/server.js and imports its chunks from ./assets, so it is
# copied as a whole directory. Static site files (client assets, favicon, etc.)
# must sit at /app/public for nitro's static file serving. server-entry.js is
# resolved next to server/, so it lands in /app itself.
COPY --from=build /app/.output/server /app/server
COPY --from=build /app/.output/client /app/public
COPY modules/hrm/frontend/module-connect/server-entry.js /app/server-entry.js
# M50.16e: the Lovable vite wrapper's dedupe + import-protection config was
# removed in M50.16, so server chunks no longer need the dev node_modules;
# production deps copied above cover the external react/TanStack specifiers.
# Drop all devDependencies artifacts to shrink the image.
RUN rm -rf /usr/local/share/.cache /usr/local/share/.local
EXPOSE 3000
CMD ["node", "server-entry.js"]
