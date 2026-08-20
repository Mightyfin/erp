// M50.16d: slim production entry. TanStack Start's rolldown build emits the
// server as a plain Web fetch handler (server/server.js) with no listen
// wrapper, so we wrap it in a minimal Node HTTP server here.
import { createServer } from "node:http";
const handler = await import("./server/server.js").then((m) => m.default);
const port = Number(process.env.PORT ?? 3000);
createServer((req, res) => {
  const url = new URL(req.url ?? "/", `http://${req.headers.host ?? "localhost"}`);
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
}).listen(port, () => {
  console.log(`hrm-web listening on :${port}`);
});
