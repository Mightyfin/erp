import { loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import tsConfigPaths from "vite-tsconfig-paths";
import { tanstackStart } from "@tanstack/react-start/plugin/vite";

// Standard Vite config — the Lovable vite-tanstack-config wrapper was removed
// (M50.16). It only bundled the same standard plugins plus dev-sandbox error
// loggers; none of those are needed in production.
export default ({ mode }: { mode: string }) => {
  // Replicate loadEnv with the VITE_ prefix so VITE_* env vars are exposed to
  // import.meta.env (the Lovable config wrapper used to do this).
  const env = loadEnv(mode, process.cwd(), "VITE_");
  const define: Record<string, string> = {};
  for (const [key, value] of Object.entries(env)) {
    define[`import.meta.env.${key}`] = JSON.stringify(value);
  }

  return {
    define,
    plugins: [
      tailwindcss(),
      tanstackStart({
        importProtection: {
          behavior: "error",
          client: {
            files: ["**/server/**"],
            specifiers: ["server-only"],
          },
        },
        // Match the previous Lovable config: route the TanStack Start server
        // entry through src/server.ts (our SSR error wrapper that converts
        // h3-swallowed 500 JSON bodies into a friendly error page).
        server: { entry: "server" },
      }),
      react(),
    ],
    nitro: {
      // The standard TanStack build emits no plain index.html (this app is
      // SSR-only), and production runs the Node server behind nginx.
      preset: "node-server",
    },
    build: {
      outDir: ".output",
      // vite 8 uses Rolldown for production builds. Rolldown's chunk splitting
      // can produce circular chunk references where a chunk's top-level code
      // calls the `__exportAll` helper before the chunk that defines it has
      // finished evaluating (TypeError: __exportAll is not a function → HTTP
      // 500 on every SSR request). `strictExecutionOrder` forces Rolldown to
      // respect the module dependency graph when emitting chunks.
      rolldownOptions: {
        output: { strictExecutionOrder: true },
      },
    },
    server: {
      allowedHosts: [
        "5173-i7smg96dlpxvsipskzckw.us3.manus.computer",
        "5173-i7smg96dlpxvsipskzckw-cb95a9a2.us3.manus.computer",
      ],
    },
  };
};
