// M50.16i: slim production entry. TanStack Start's rolldown build emits the
// server as a plain Web fetch handler (server/server.js) with no listen
// wrapper and NO working static-file middleware, so we wrap it in a minimal
// Node HTTP server here. Static assets under /assets/ and root-level site
// files (favicon, logo, robots) are served directly from the client build
// directory (`./public` if present, else `./client`); everything else is
// delegated to the SSR fetch handler.
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const handler = await import("./server/server.js").then((m) => m.default);
const port = Number(process.env.PORT ?? 3000);

// Resolve the directory of THIS file (not the cwd, which may differ).
const entryDir = dirname(fileURLToPath(import.meta.url));
// Client assets live next to this entry. Build/layout variants: the Dockerfile
// copies them to ./public, while a raw rolldown build keeps them at ./client.
const candidateDirs = [resolve(entryDir, "public"), resolve(entryDir, "client")];
const staticDir = candidateDirs.find((d) => existsSync(join(d, "assets"))) ?? candidateDirs[0];

const MIME = {
  ".js": "text/javascript; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".webp": "image/webp",
  ".ico": "image/x-icon",
  ".txt": "text/plain; charset=utf-8",
  ".woff": "font/woff",
  ".woff2": "font/woff2",
};

function mimeTypeOf(path) {
  const ext = path.slice(path.lastIndexOf(".")).toLowerCase();
  return MIME[ext] ?? "application/octet-stream";
}

function isSafeRelative(reqPath) {
  const joined = join(staticDir, reqPath.slice(1));
  const full = resolve(joined);
  return full.startsWith(staticDir + "/") || full === staticDir;
}

async function tryStatic(pathname) {
  // Only serve: (a) anything under /assets/ or (b) a root-level filename
  // consisting of safe characters (favicon.png, robots.txt, ...).
  const isAsset = pathname.startsWith("/assets/");
  const isRootFile = !isAsset && /^[A-Za-z0-9._-]+$/.test(pathname.slice(1));
  if (!isAsset && !isRootFile) return null;
  if (!isSafeRelative(pathname)) return null;
  const file = join(staticDir, decodeURIComponent(pathname).slice(1));
  if (!existsSync(file)) return null;
  const data = await readFile(file);
  return { data, type: mimeTypeOf(file) };
}

createServer((req, res) => {
  const url = new URL(req.url ?? "/", `http://${req.headers.host ?? "localhost"}`);

  if (req.method === "GET" || req.method === "HEAD") {
    tryStatic(url.pathname)
      .then((staticRes) => {
        if (staticRes) {
          res.writeHead(200, {
            "Content-Type": staticRes.type,
            "Content-Length": staticRes.data.length,
            "Cache-Control": "public, max-age=31536000, immutable",
          });
          if (req.method === "HEAD") return res.end();
          res.end(staticRes.data);
        } else {
          void forwardToHandler(req, res, url);
        }
      })
      .catch((err) => {
        console.error("static read error", err);
        void forwardToHandler(req, res, url);
      });
  } else {
    void forwardToHandler(req, res, url);
  }
}).listen(port, () => {
  console.log(`hrm-web listening on :${port}`);
});

function forwardToHandler(req, res, url) {
  const headers = new Headers();
  for (const [k, v] of Object.entries(req.headers)) {
    if (Array.isArray(v)) for (const h of v) headers.append(k, h);
    else if (v) headers.set(k, String(v));
  }
  const reqBody =
    req.method !== "GET" && req.method !== "HEAD"
      ? new ReadableStream({
          start(controller) {
            req.on("data", (chunk) => controller.enqueue(chunk));
            req.on("end", () => controller.close());
            req.on("error", (err) => controller.error(err));
          },
        })
      : null;
  handler
    .fetch(new Request(url, { method: req.method, headers, body: reqBody }))
    .then(async (response) => {
      res.writeHead(response.status, Object.fromEntries(response.headers));
      if (response.body) {
        const reader = response.body.getReader();
        for (;;) {
          const { done, value } = await reader.read();
          if (done) break;
          res.write(value);
        }
      }
      res.end();
    })
    .catch((err) => {
      console.error(err);
      if (!res.headersSent) res.writeHead(500);
      res.end("Internal Server Error");
    });
}
