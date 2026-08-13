package auth

import (
	"context"
	"errors"
)

var ErrInvalidToken = errors.New("invalid access token")

// Tenant here is the employer whose data is being accessed - MightyFin is the vendor, per
// docs/00-architecture-position.md; ERP is built to keep that swappable from the start.
type Principal struct {
	Subject, TenantID, Environment string
	Scopes                         map[string]struct{}
	Roles                          map[string]struct{}
}

func (p Principal) HasScope(scope string) bool { _, ok := p.Scopes[scope]; return ok }
func (p Principal) HasRole(role string) bool   { _, ok := p.Roles[role]; return ok }

type Verifier interface {
	Verify(context.Context, string) (Principal, error)
}
