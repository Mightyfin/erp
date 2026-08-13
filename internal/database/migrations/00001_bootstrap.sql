-- +goose Up

-- One schema per module, per docs/00-architecture-position.md: each module owns its schema and
-- never reads another module's tables. Creating the namespaces now, empty, so the boundary exists
-- before any module's tables land in it.
CREATE SCHEMA IF NOT EXISTS hrm;
CREATE SCHEMA IF NOT EXISTS finance;
CREATE SCHEMA IF NOT EXISTS accounting;
CREATE SCHEMA IF NOT EXISTS procurement;
CREATE SCHEMA IF NOT EXISTS inventory;

CREATE TABLE IF NOT EXISTS public.erp_bootstrap (
    id SERIAL PRIMARY KEY,
    note TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO public.erp_bootstrap (note) VALUES ('erp-api schema bootstrap applied');

-- +goose Down
DROP TABLE IF EXISTS public.erp_bootstrap;
DROP SCHEMA IF EXISTS inventory;
DROP SCHEMA IF EXISTS procurement;
DROP SCHEMA IF EXISTS accounting;
DROP SCHEMA IF EXISTS finance;
DROP SCHEMA IF EXISTS hrm;
