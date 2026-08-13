using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mightyfin.Erp.Hrm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hrm");

            migrationBuilder.CreateTable(
                name: "approval_delegations",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegate_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: true),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_delegations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_entries",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    before_json = table.Column<string>(type: "text", nullable: true),
                    after_json = table.Column<string>(type: "text", nullable: true),
                    actor_subject_id = table.Column<string>(type: "text", nullable: false),
                    correlation_id = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "capability_configs",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_key = table.Column<string>(type: "text", nullable: false),
                    tier = table.Column<string>(type: "text", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_capability_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contribution_rules",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    payer = table.Column<string>(type: "text", nullable: false),
                    rate = table.Column<decimal>(type: "numeric", nullable: false),
                    ceiling = table.Column<decimal>(type: "numeric", nullable: true),
                    floor = table.Column<decimal>(type: "numeric", nullable: true),
                    tied_component_code = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contribution_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leave_types",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    default_days_per_year = table.Column<int>(type: "integer", nullable: false),
                    max_consecutive_days = table.Column<decimal>(type: "numeric", nullable: false),
                    requires_evidence = table.Column<bool>(type: "boolean", nullable: false),
                    min_notice_days = table.Column<int>(type: "integer", nullable: false),
                    allows_partial_days = table.Column<bool>(type: "boolean", nullable: false),
                    carry_forward_days = table.Column<int>(type: "integer", nullable: false),
                    carry_forward_expiry_months = table.Column<int>(type: "integer", nullable: false),
                    allow_negative = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legal_entities",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    registered_name = table.Column<string>(type: "text", nullable: false),
                    trading_name = table.Column<string>(type: "text", nullable: true),
                    pacra_number = table.Column<string>(type: "text", nullable: true),
                    tpin = table.Column<string>(type: "text", nullable: true),
                    napsa_employer_ref = table.Column<string>(type: "text", nullable: true),
                    nhima_employer_ref = table.Column<string>(type: "text", nullable: true),
                    wcfcb_employer_ref = table.Column<string>(type: "text", nullable: true),
                    currency = table.Column<string>(type: "text", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_entities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pay_groups",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    frequency = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    calendar_day_of_month = table.Column<int>(type: "integer", nullable: false),
                    input_cutoff_days_before_payday = table.Column<int>(type: "integer", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pay_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "protected_disclosures",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_reference = table.Column<string>(type: "text", nullable: false),
                    access_code = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    triage_notes = table.Column<string>(type: "text", nullable: true),
                    assigned_to_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outcome = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_protected_disclosures", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "salary_components",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    component_type = table.Column<string>(type: "text", nullable: false),
                    calculation_basis = table.Column<string>(type: "text", nullable: false),
                    basis_component_code = table.Column<string>(type: "text", nullable: true),
                    rate = table.Column<decimal>(type: "numeric", nullable: true),
                    fixed_amount = table.Column<decimal>(type: "numeric", nullable: true),
                    ceiling = table.Column<decimal>(type: "numeric", nullable: true),
                    is_taxable = table.Column<bool>(type: "boolean", nullable: false),
                    is_statutory = table.Column<bool>(type: "boolean", nullable: false),
                    gl_account_ref = table.Column<string>(type: "text", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_salary_components", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "salary_structures",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_salary_structures", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_slabs",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_year = table.Column<string>(type: "text", nullable: false),
                    min_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    max_amount = table.Column<decimal>(type: "numeric", nullable: true),
                    rate = table.Column<decimal>(type: "numeric", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_slabs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_calendars",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    standard_weekly_hours = table.Column<int>(type: "integer", nullable: false),
                    weekend_days = table.Column<string>(type: "text", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_calendars", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_requests",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_type = table.Column<string>(type: "text", nullable: false),
                    subject_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    return_note = table.Column<string>(type: "text", nullable: true),
                    current_approver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    escalated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_units",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unit_type = table.Column<string>(type: "text", nullable: true),
                    cost_centre_ref = table.Column<string>(type: "text", nullable: true),
                    manager_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_units", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_units_legal_entities_legal_entity_id",
                        column: x => x.legal_entity_id,
                        principalSchema: "hrm",
                        principalTable: "legal_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_org_units_org_units_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "hrm",
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pay_periods",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pay_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_label = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    cutoff_date = table.Column<DateOnly>(type: "date", nullable: false),
                    pay_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pay_periods", x => x.id);
                    table.ForeignKey(
                        name: "FK_pay_periods_pay_groups_pay_group_id",
                        column: x => x.pay_group_id,
                        principalSchema: "hrm",
                        principalTable: "pay_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "salary_structure_items",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    structure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_amount = table.Column<decimal>(type: "numeric", nullable: true),
                    is_optional = table.Column<bool>(type: "boolean", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_salary_structure_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_salary_structure_items_salary_components_component_id",
                        column: x => x.component_id,
                        principalSchema: "hrm",
                        principalTable: "salary_components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_salary_structure_items_salary_structures_structure_id",
                        column: x => x.structure_id,
                        principalSchema: "hrm",
                        principalTable: "salary_structures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "public_holidays",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    calendar_id = table.Column<Guid>(type: "uuid", nullable: false),
                    holiday_date = table.Column<DateOnly>(type: "date", nullable: false),
                    observed_on = table.Column<string>(type: "text", nullable: true),
                    is_recurring = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_holidays", x => x.id);
                    table.ForeignKey(
                        name: "FK_public_holidays_work_calendars_calendar_id",
                        column: x => x.calendar_id,
                        principalSchema: "hrm",
                        principalTable: "work_calendars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_locations",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address_line = table.Column<string>(type: "text", nullable: true),
                    province = table.Column<string>(type: "text", nullable: true),
                    district = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true),
                    default_calendar_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_locations", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_locations_legal_entities_legal_entity_id",
                        column: x => x.legal_entity_id,
                        principalSchema: "hrm",
                        principalTable: "legal_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_locations_work_calendars_default_calendar_id",
                        column: x => x.default_calendar_id,
                        principalSchema: "hrm",
                        principalTable: "work_calendars",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "workflow_decisions",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    delegated_to_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_decisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_decisions_workflow_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "hrm",
                        principalTable: "workflow_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vacancies",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_title = table.Column<string>(type: "text", nullable: false),
                    grade = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacancies", x => x.id);
                    table.ForeignKey(
                        name: "FK_vacancies_org_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalSchema: "hrm",
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payroll_runs",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pay_period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pay_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    employee_count = table.Column<int>(type: "integer", nullable: false),
                    total_gross = table.Column<decimal>(type: "numeric", nullable: false),
                    total_deductions = table.Column<decimal>(type: "numeric", nullable: false),
                    total_net = table.Column<decimal>(type: "numeric", nullable: false),
                    total_employer_cost = table.Column<decimal>(type: "numeric", nullable: false),
                    is_reversal = table.Column<bool>(type: "boolean", nullable: false),
                    reverses_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    calc_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    exception_count = table.Column<int>(type: "integer", nullable: false),
                    calc_version = table.Column<string>(type: "text", nullable: true),
                    approval_note = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_runs_pay_periods_pay_period_id",
                        column: x => x.pay_period_id,
                        principalSchema: "hrm",
                        principalTable: "pay_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workers",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_no = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    middle_name = table.Column<string>(type: "text", nullable: true),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    preferred_name = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    photo_url = table.Column<string>(type: "text", nullable: true),
                    nrc = table.Column<string>(type: "text", nullable: true),
                    passport_no = table.Column<string>(type: "text", nullable: true),
                    tpin = table.Column<string>(type: "text", nullable: true),
                    napsa_number = table.Column<string>(type: "text", nullable: true),
                    nhima_number = table.Column<string>(type: "text", nullable: true),
                    nationality = table.Column<string>(type: "text", nullable: true),
                    date_of_birth = table.Column<string>(type: "text", nullable: true),
                    subject_id = table.Column<string>(type: "text", nullable: true),
                    worker_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    manager_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grade = table.Column<string>(type: "text", nullable: true),
                    job_title = table.Column<string>(type: "text", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workers", x => x.id);
                    table.ForeignKey(
                        name: "FK_workers_org_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalSchema: "hrm",
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_workers_work_locations_location_id",
                        column: x => x.location_id,
                        principalSchema: "hrm",
                        principalTable: "work_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "candidates",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vacancy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    stage = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidates", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidates_vacancies_vacancy_id",
                        column: x => x.vacancy_id,
                        principalSchema: "hrm",
                        principalTable: "vacancies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manager_id = table.Column<Guid>(type: "uuid", nullable: true),
                    job_title = table.Column<string>(type: "text", nullable: true),
                    grade = table.Column<string>(type: "text", nullable: true),
                    position_no = table.Column<string>(type: "text", nullable: true),
                    contract_type = table.Column<string>(type: "text", nullable: false),
                    work_pattern = table.Column<string>(type: "text", nullable: false),
                    probation_months = table.Column<int>(type: "integer", nullable: false),
                    notice_days = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_assignments_legal_entities_legal_entity_id",
                        column: x => x.legal_entity_id,
                        principalSchema: "hrm",
                        principalTable: "legal_entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assignments_org_units_org_unit_id",
                        column: x => x.org_unit_id,
                        principalSchema: "hrm",
                        principalTable: "org_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assignments_work_locations_location_id",
                        column: x => x.location_id,
                        principalSchema: "hrm",
                        principalTable: "work_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_assignments_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attendance_corrections",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    issue_type = table.Column<string>(type: "text", nullable: false),
                    proposed_clock_in = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    proposed_clock_out = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    proposed_status = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_corrections", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendance_corrections_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attendance_records",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    clock_in = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    clock_out = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    derived_status = table.Column<string>(type: "text", nullable: false),
                    total_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendance_records_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "emergency_contacts",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emergency_contacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_emergency_contacts_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hr_letters",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    letter_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    addressee = table.Column<string>(type: "text", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    verification_code = table.Column<string>(type: "text", nullable: true),
                    document_url = table.Column<string>(type: "text", nullable: true),
                    template_body = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_letters", x => x.id);
                    table.ForeignKey(
                        name: "FK_hr_letters_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hr_requests",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    confidentiality = table.Column<string>(type: "text", nullable: false),
                    service_target_days = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_hr_requests_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "leave_balance_ledger",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_type_code = table.Column<string>(type: "text", nullable: false),
                    days = table.Column<decimal>(type: "numeric", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_type = table.Column<string>(type: "text", nullable: false),
                    for_date = table.Column<DateOnly>(type: "date", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_balance_ledger", x => x.id);
                    table.ForeignKey(
                        name: "FK_leave_balance_ledger_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "leave_requests",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    leave_type_code = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_partial_day = table.Column<bool>(type: "boolean", nullable: false),
                    start_time = table.Column<string>(type: "text", nullable: true),
                    end_time = table.Column<string>(type: "text", nullable: true),
                    requested_days = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    return_note = table.Column<string>(type: "text", nullable: true),
                    evidence_attached = table.Column<bool>(type: "boolean", nullable: false),
                    balance_reserved = table.Column<bool>(type: "boolean", nullable: false),
                    created_for_period = table.Column<DateOnly>(type: "date", nullable: false),
                    crosses_cutoff = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_leave_requests_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "movements",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    from_org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_job_title = table.Column<string>(type: "text", nullable: true),
                    from_grade = table.Column<string>(type: "text", nullable: true),
                    to_org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_job_title = table.Column<string>(type: "text", nullable: true),
                    to_grade = table.Column<string>(type: "text", nullable: true),
                    to_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_manager_id = table.Column<Guid>(type: "uuid", nullable: true),
                    salary_change = table.Column<decimal>(type: "numeric", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_movements_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payroll_run_lines",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gross_pay = table.Column<decimal>(type: "numeric", nullable: false),
                    total_deductions = table.Column<decimal>(type: "numeric", nullable: false),
                    net_pay = table.Column<decimal>(type: "numeric", nullable: false),
                    employer_cost = table.Column<decimal>(type: "numeric", nullable: false),
                    has_exception = table.Column<bool>(type: "boolean", nullable: false),
                    exception_reason = table.Column<string>(type: "text", nullable: true),
                    component_count = table.Column<int>(type: "integer", nullable: false),
                    rule_version_snapshot = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_run_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_run_lines_payroll_runs_run_id",
                        column: x => x.run_id,
                        principalSchema: "hrm",
                        principalTable: "payroll_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payroll_run_lines_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "relations_cases",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    case_type = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    classification = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_relations_cases", x => x.id);
                    table.ForeignKey(
                        name: "FK_relations_cases_workers_subject_worker_id",
                        column: x => x.subject_worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "worker_bank_details",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_name = table.Column<string>(type: "text", nullable: false),
                    branch_code = table.Column<string>(type: "text", nullable: false),
                    account_number = table.Column<string>(type: "text", nullable: false),
                    account_name = table.Column<string>(type: "text", nullable: false),
                    payment_method = table.Column<string>(type: "text", nullable: false),
                    mobile_money_number = table.Column<string>(type: "text", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worker_bank_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_worker_bank_details_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "worker_documents",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_path = table.Column<string>(type: "text", nullable: false),
                    classification = table.Column<string>(type: "text", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_latest = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worker_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_worker_documents_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "worker_payroll_profiles",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    structure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pay_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worker_payroll_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_worker_payroll_profiles_pay_groups_pay_group_id",
                        column: x => x.pay_group_id,
                        principalSchema: "hrm",
                        principalTable: "pay_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_worker_payroll_profiles_salary_structures_structure_id",
                        column: x => x.structure_id,
                        principalSchema: "hrm",
                        principalTable: "salary_structures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_worker_payroll_profiles_workers_worker_id",
                        column: x => x.worker_id,
                        principalSchema: "hrm",
                        principalTable: "workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "offers",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_salary = table.Column<decimal>(type: "numeric", nullable: false),
                    contract_type = table.Column<string>(type: "text", nullable: false),
                    probation_months = table.Column<int>(type: "integer", nullable: false),
                    notice_days = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offers", x => x.id);
                    table.ForeignKey(
                        name: "FK_offers_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "hrm",
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hr_request_messages",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    is_internal_note = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_request_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_hr_request_messages_hr_requests_request_id",
                        column: x => x.request_id,
                        principalSchema: "hrm",
                        principalTable: "hr_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payroll_line_components",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_code = table.Column<string>(type: "text", nullable: false),
                    component_name = table.Column<string>(type: "text", nullable: false),
                    component_type = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false),
                    rule_version_id = table.Column<int>(type: "integer", nullable: false),
                    is_statutory = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_line_components", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_line_components_payroll_run_lines_run_line_id",
                        column: x => x.run_line_id,
                        principalSchema: "hrm",
                        principalTable: "payroll_run_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payslips",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payslip_no = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gross_pay = table.Column<decimal>(type: "numeric", nullable: false),
                    total_deductions = table.Column<decimal>(type: "numeric", nullable: false),
                    net_pay = table.Column<decimal>(type: "numeric", nullable: false),
                    ytd_gross = table.Column<string>(type: "text", nullable: true),
                    ytd_tax = table.Column<string>(type: "text", nullable: true),
                    ytd_net = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    document_url = table.Column<string>(type: "text", nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payslips", x => x.id);
                    table.ForeignKey(
                        name: "FK_payslips_payroll_run_lines_run_line_id",
                        column: x => x.run_line_id,
                        principalSchema: "hrm",
                        principalTable: "payroll_run_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "worker_component_values",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    component_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worker_component_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_worker_component_values_salary_components_component_id",
                        column: x => x.component_id,
                        principalSchema: "hrm",
                        principalTable: "salary_components",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_worker_component_values_worker_payroll_profiles_profile_id",
                        column: x => x.profile_id,
                        principalSchema: "hrm",
                        principalTable: "worker_payroll_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payslip_access_logs",
                schema: "hrm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payslip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accessed_by = table.Column<string>(type: "text", nullable: false),
                    access_reason = table.Column<string>(type: "text", nullable: false),
                    accessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payslip_access_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_payslip_access_logs_payslips_payslip_id",
                        column: x => x.payslip_id,
                        principalSchema: "hrm",
                        principalTable: "payslips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_legal_entity_id",
                schema: "hrm",
                table: "assignments",
                column: "legal_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_location_id",
                schema: "hrm",
                table: "assignments",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_org_unit_id",
                schema: "hrm",
                table: "assignments",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_assignments_worker_id",
                schema: "hrm",
                table: "assignments",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_corrections_worker_id",
                schema: "hrm",
                table: "attendance_corrections",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_tenant_id_worker_id_work_date",
                schema: "hrm",
                table: "attendance_records",
                columns: new[] { "tenant_id", "worker_id", "work_date" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_records_worker_id",
                schema: "hrm",
                table: "attendance_records",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidates_vacancy_id",
                schema: "hrm",
                table: "candidates",
                column: "vacancy_id");

            migrationBuilder.CreateIndex(
                name: "IX_emergency_contacts_worker_id",
                schema: "hrm",
                table: "emergency_contacts",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_hr_letters_worker_id",
                schema: "hrm",
                table: "hr_letters",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_hr_request_messages_request_id",
                schema: "hrm",
                table: "hr_request_messages",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_hr_requests_worker_id",
                schema: "hrm",
                table: "hr_requests",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_balance_ledger_worker_id",
                schema: "hrm",
                table: "leave_balance_ledger",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_worker_id",
                schema: "hrm",
                table: "leave_requests",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_types_tenant_id_code",
                schema: "hrm",
                table: "leave_types",
                columns: new[] { "tenant_id", "code" });

            migrationBuilder.CreateIndex(
                name: "IX_movements_worker_id",
                schema: "hrm",
                table: "movements",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_offers_candidate_id",
                schema: "hrm",
                table: "offers",
                column: "candidate_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_units_legal_entity_id",
                schema: "hrm",
                table: "org_units",
                column: "legal_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_units_parent_id",
                schema: "hrm",
                table: "org_units",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_pay_periods_pay_group_id",
                schema: "hrm",
                table: "pay_periods",
                column: "pay_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_pay_periods_tenant_id_pay_group_id_period_label",
                schema: "hrm",
                table: "pay_periods",
                columns: new[] { "tenant_id", "pay_group_id", "period_label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payroll_line_components_run_line_id",
                schema: "hrm",
                table: "payroll_line_components",
                column: "run_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_run_lines_run_id",
                schema: "hrm",
                table: "payroll_run_lines",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_run_lines_worker_id",
                schema: "hrm",
                table: "payroll_run_lines",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_runs_pay_period_id",
                schema: "hrm",
                table: "payroll_runs",
                column: "pay_period_id");

            migrationBuilder.CreateIndex(
                name: "IX_payslip_access_logs_payslip_id",
                schema: "hrm",
                table: "payslip_access_logs",
                column: "payslip_id");

            migrationBuilder.CreateIndex(
                name: "IX_payslips_payslip_no",
                schema: "hrm",
                table: "payslips",
                column: "payslip_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payslips_run_line_id",
                schema: "hrm",
                table: "payslips",
                column: "run_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_protected_disclosures_case_reference",
                schema: "hrm",
                table: "protected_disclosures",
                column: "case_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_public_holidays_calendar_id",
                schema: "hrm",
                table: "public_holidays",
                column: "calendar_id");

            migrationBuilder.CreateIndex(
                name: "IX_relations_cases_subject_worker_id",
                schema: "hrm",
                table: "relations_cases",
                column: "subject_worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_salary_components_tenant_id_code",
                schema: "hrm",
                table: "salary_components",
                columns: new[] { "tenant_id", "code" });

            migrationBuilder.CreateIndex(
                name: "IX_salary_structure_items_component_id",
                schema: "hrm",
                table: "salary_structure_items",
                column: "component_id");

            migrationBuilder.CreateIndex(
                name: "IX_salary_structure_items_structure_id",
                schema: "hrm",
                table: "salary_structure_items",
                column: "structure_id");

            migrationBuilder.CreateIndex(
                name: "IX_vacancies_org_unit_id",
                schema: "hrm",
                table: "vacancies",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_locations_default_calendar_id",
                schema: "hrm",
                table: "work_locations",
                column: "default_calendar_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_locations_legal_entity_id",
                schema: "hrm",
                table: "work_locations",
                column: "legal_entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_worker_bank_details_worker_id",
                schema: "hrm",
                table: "worker_bank_details",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_worker_component_values_component_id",
                schema: "hrm",
                table: "worker_component_values",
                column: "component_id");

            migrationBuilder.CreateIndex(
                name: "IX_worker_component_values_profile_id",
                schema: "hrm",
                table: "worker_component_values",
                column: "profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_worker_documents_worker_id",
                schema: "hrm",
                table: "worker_documents",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_worker_payroll_profiles_pay_group_id",
                schema: "hrm",
                table: "worker_payroll_profiles",
                column: "pay_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_worker_payroll_profiles_structure_id",
                schema: "hrm",
                table: "worker_payroll_profiles",
                column: "structure_id");

            migrationBuilder.CreateIndex(
                name: "IX_worker_payroll_profiles_worker_id",
                schema: "hrm",
                table: "worker_payroll_profiles",
                column: "worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_workers_employee_no",
                schema: "hrm",
                table: "workers",
                column: "employee_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workers_location_id",
                schema: "hrm",
                table: "workers",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "IX_workers_org_unit_id",
                schema: "hrm",
                table: "workers",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_decisions_request_id",
                schema: "hrm",
                table: "workflow_decisions",
                column: "request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_delegations",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "assignments",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "attendance_corrections",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "attendance_records",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "capability_configs",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "contribution_rules",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "emergency_contacts",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "hr_letters",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "hr_request_messages",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "leave_balance_ledger",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "leave_requests",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "leave_types",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "movements",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "offers",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "payroll_line_components",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "payslip_access_logs",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "protected_disclosures",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "public_holidays",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "relations_cases",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "salary_structure_items",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "tax_slabs",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "worker_bank_details",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "worker_component_values",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "worker_documents",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "workflow_decisions",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "hr_requests",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "candidates",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "payslips",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "salary_components",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "worker_payroll_profiles",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "workflow_requests",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "vacancies",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "payroll_run_lines",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "salary_structures",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "payroll_runs",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "workers",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "pay_periods",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "org_units",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "work_locations",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "pay_groups",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "legal_entities",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "work_calendars",
                schema: "hrm");
        }
    }
}
