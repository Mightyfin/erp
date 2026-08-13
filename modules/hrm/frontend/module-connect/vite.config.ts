// @lovable.dev/vite-tanstack-config already includes the following — do NOT add them manually
// or the app will break with duplicate plugins:
//   - TanStack devtools (dev-only, first), tanstackStart, viteReact, tailwindcss, tsConfigPaths,
//     nitro (build-only using cloudflare as a default target), VITE_* env injection, @ path alias,
//     React/TanStack dedupe, error logger plugins, and sandbox detection (port/host/strictPort).
// You can pass additional config via defineConfig({ vite: { ... }, etc... }) if needed.
import { defineConfig } from "@lovable.dev/vite-tanstack-config";

export default defineConfig({
  nitro: {
    // The bundled config defaults to the `cloudflare` preset, which only runs on
    // Cloudflare Workers. This TanStack Start app is SSR-only (no plain index.html
    // is emitted), so the production host runs the Node server behind nginx.
    preset: "node-server",
  },
  vite: {
    server: {
      allowedHosts: [
        "5173-i7smg96dlpxvsipskzckw.us3.manus.computer",
        "5173-i7smg96dlpxvsipskzckw-cb95a9a2.us3.manus.computer",
      ],
    },
  },
  tanstackStart: {
    // Redirect TanStack Start's bundled server entry to src/server.ts (our SSR error wrapper).
    // nitro/vite builds from this
    server: { entry: "server" },
  },
});
