#!/bin/bash
# M5: Seed Zambian statutory payroll data + default monthly pay group + Aug 2026 period.
# Idempotent (inserts only when not already present).
set -e
export PGPASSWORD=NMNikwUmBtJ7ufSuUvxRQl4Y5zSBqDxO
PSQL="psql -h 127.0.0.1 -p 15432 -U erp -d erp"
T='019ffa8b-0fb0-71e6-849a-f76e5a28e0b5'
BY='seed-m5'

$PSQL -v t="'$T'" -v by="'$BY'" <<'SQL'
-- Salary components
INSERT INTO hrm.salary_components (id, code, name, component_type, calculation_basis, basis_component_code, rate, fixed_amount, ceiling, is_taxable, is_statutory, priority, version, is_active, effective_from, tenant_id, created_at, created_by, is_archived)
SELECT gen_random_uuid(), v.code, v.name, v.ct, v.cbasis, v.basis, v.rate, v.fixed, v.ceiling, v.taxable, v.statutory, v.priority, 1, true, '2026-01-01', '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5', now(), 'seed-m5', false
FROM (VALUES
  ('basic', 'Basic Salary', 'earning', 'fixed', NULL, NULL::numeric, NULL::numeric, NULL::numeric, true, false, 10),
  ('housing-allowance', 'Housing Allowance', 'earning', 'fixed', NULL, NULL::numeric, NULL::numeric, NULL::numeric, true, false, 20),
  ('transport-allowance', 'Transport Allowance', 'earning', 'fixed', NULL, NULL::numeric, NULL::numeric, NULL::numeric, true, false, 30),
  ('napsa-ee', 'NAPSA Employee (5%)', 'deduction', 'percent-of', 'basic', NULL, NULL, NULL, false, true, 90),
  ('napsa-er', 'NAPSA Employer (5%)', 'employer-contribution', 'percent-of', 'basic', NULL, NULL, NULL, false, true, 91),
  ('nhima-ee', 'NHIMA Employee (1%)', 'deduction', 'percent-of', 'basic', NULL, NULL, NULL, false, true, 92),
  ('nhima-er', 'NHIMA Employer (1%)', 'employer-contribution', 'percent-of', 'basic', NULL, NULL, NULL, false, true, 93),
  ('paye', 'ZRA PAYE (2026 bands)', 'tax', 'slab', 'gross', NULL, NULL, NULL, true, true, 100)
) AS v(code, name, ct, cbasis, basis, rate, fixed, ceiling, taxable, statutory, priority)
-- tuple row count must match the aliases above
WHERE NOT EXISTS (SELECT 1 FROM hrm.salary_components WHERE code = v.code AND tenant_id = '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5');

-- ZRA PAYE 2026 monthly slabs (verified from zra.org.zm/paye-calculator and PwC)
INSERT INTO hrm.tax_slabs (id, tax_year, min_amount, max_amount, rate, sequence, version, is_active, effective_from, tenant_id, created_at, created_by, is_archived)
SELECT gen_random_uuid(), v.year, v.min, v.max, v.rate, v.seq, 1, true, '2026-01-01', '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5', now(), 'seed-m5', false
FROM (VALUES
  ('2026', 0, 5100, 0, 10),
  ('2026', 5100, 7100, 20, 20),
  ('2026', 7100, 9200, 30, 30),
  ('2026', 9200, NULL, 37, 40)
) AS v(year, min, max, rate, seq)
WHERE NOT EXISTS (SELECT 1 FROM hrm.tax_slabs WHERE tax_year = v.year AND sequence = v.seq AND tenant_id = '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5');

-- Contribution rules
INSERT INTO hrm.contribution_rules (id, code, name, payer, rate, ceiling, floor, tied_component_code, version, is_active, effective_from, tenant_id, created_at, created_by, is_archived)
SELECT gen_random_uuid(), v.code, v.name, v.payer, v.rate, v.ceiling, NULL, v.tied, 1, true, '2026-01-01', '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5', now(), 'seed-m5', false
FROM (VALUES
  ('napsa-ee', 'NAPSA Employee Contribution 2026', 'employee', 5, 1861.80, 'basic'),
  ('napsa-er', 'NAPSA Employer Contribution 2026', 'employer', 5, 1861.80, 'basic'),
  ('nhima-ee', 'NHIMA Employee Contribution 2026', 'employee', 1, NULL, 'basic'),
  ('nhima-er', 'NHIMA Employer Contribution 2026', 'employer', 1, NULL, 'basic')
) AS v(code, name, payer, rate, ceiling, tied)
WHERE NOT EXISTS (SELECT 1 FROM hrm.contribution_rules WHERE code = v.code AND tenant_id = '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5');

-- Default monthly pay group (ZMW)
INSERT INTO hrm.pay_groups (id, code, name, frequency, currency, calendar_day_of_month, input_cutoff_days_before_payday, is_default, tenant_id, created_at, created_by, is_archived)
SELECT gen_random_uuid(), 'MONTHLY-ZMW', 'Monthly ZMW', 'monthly', 'ZMW', 28, 3, true, '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5', now(), 'seed-m5', false
WHERE NOT EXISTS (SELECT 1 FROM hrm.pay_groups WHERE code = 'MONTHLY-ZMW' AND tenant_id = '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5');

-- Aug 2026 pay period for the default group
INSERT INTO hrm.pay_periods (id, pay_group_id, period_label, start_date, end_date, cutoff_date, pay_date, status, is_current, tenant_id, created_at, created_by, is_archived)
SELECT gen_random_uuid(), pg.id, '2026-08', '2026-08-01', '2026-08-31', '2026-08-25', '2026-08-28', 'open', true, '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5', now(), 'seed-m5', false
FROM hrm.pay_groups pg
WHERE pg.code = 'MONTHLY-ZMW' AND pg.tenant_id = '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5'
  AND NOT EXISTS (SELECT 1 FROM hrm.pay_periods WHERE pay_group_id = pg.id AND period_label = '2026-08');

-- Salary structure holding the standard components
INSERT INTO hrm.salary_structures (id, code, name, version, is_active, tenant_id, created_at, created_by, is_archived)
SELECT gen_random_uuid(), 'ZMW-STANDARD', 'Zambia Standard Earnings & Statutory', 1, true, '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5', now(), 'seed-m5', false
WHERE NOT EXISTS (SELECT 1 FROM hrm.salary_structures WHERE code = 'ZMW-STANDARD' AND tenant_id = '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5');

-- Map structure to components (structure items)
INSERT INTO hrm.salary_structure_items (id, structure_id, component_id, default_amount, is_optional, "order", tenant_id, created_at, created_by, is_archived)
SELECT gen_random_uuid(), s.id, c.id, 0, NOT (c.component_type = 'earning'), c.priority, '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5', now(), 'seed-m5', false
FROM hrm.salary_structures s, hrm.salary_components c
WHERE s.code = 'ZMW-STANDARD' AND s.tenant_id = '019ffa8b-0fb0-71e6-849a-f76e5a28e0b5'
  AND NOT EXISTS (SELECT 1 FROM hrm.salary_structure_items si WHERE si.structure_id = s.id AND si.component_id = c.id);
SQL
echo "=== seed complete ==="
$PSQL -c "select code, component_type from hrm.salary_components where tenant_id='$T';" \
      -c "select tax_year, sequence, rate, max_amount from hrm.tax_slabs where tenant_id='$T';" \
      -c "select code, payer, rate, ceiling from hrm.contribution_rules where tenant_id='$T';" \
      -c "select id, code from hrm.pay_groups where tenant_id='$T';" \
      -c "select period_label, status from hrm.pay_periods;" \
      -c "select count(*) as structure_items from hrm.salary_structure_items;"
