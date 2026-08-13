export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (url.pathname === "/api" || url.pathname.startsWith("/api/")) {
      const backendUrl = new URL(
        url.pathname + url.search,
        "http://erp-backend",
      );
      return env.ERP_BACKEND.fetch(new Request(backendUrl, request));
    }

    const frontendUrl = new URL(
      url.pathname + url.search,
      "https://erp-git-main-mighty-finances-projects.vercel.app",
    );
    const frontendRequest = new Request(frontendUrl, request);
    frontendRequest.headers.set(
      "x-vercel-protection-bypass",
      env.VERCEL_BYPASS,
    );
    return fetch(frontendRequest);
  },
};
