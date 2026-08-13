package httpserver

import (
	"context"
	"net/http"
	"strings"

	"github.com/Mightyfin/erp/internal/auth"
)

type contextKey string

const principalKey contextKey = "principal"

// authenticate leaves /health/* open (needed for compose/orchestrator health checks before any
// caller has a token) and requires a bearer token on everything else, unless auth is disabled -
// which config.Load() only allows when ERP_ENVIRONMENT=local, regardless of where it's deployed.
func authenticate(verifier auth.Verifier, disabled bool, next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasPrefix(r.URL.Path, "/health/") {
			next.ServeHTTP(w, r)
			return
		}
		if disabled {
			p := auth.Principal{Subject: "local-developer", TenantID: "local-tenant", Environment: "local", Scopes: map[string]struct{}{}, Roles: map[string]struct{}{}}
			next.ServeHTTP(w, r.WithContext(context.WithValue(r.Context(), principalKey, p)))
			return
		}
		scheme, token, ok := strings.Cut(strings.TrimSpace(r.Header.Get("Authorization")), " ")
		if !ok || !strings.EqualFold(scheme, "Bearer") || strings.TrimSpace(token) == "" {
			writeJSON(w, http.StatusUnauthorized, map[string]string{"error": "unauthorized"})
			return
		}
		p, err := verifier.Verify(r.Context(), strings.TrimSpace(token))
		if err != nil {
			writeJSON(w, http.StatusUnauthorized, map[string]string{"error": "unauthorized"})
			return
		}
		next.ServeHTTP(w, r.WithContext(context.WithValue(r.Context(), principalKey, p)))
	})
}
