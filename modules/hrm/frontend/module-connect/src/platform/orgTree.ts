/**
 * Entity-rooted org hierarchy helpers.
 *
 * Backend shape (GET /api/hrm/admin/org-units/entity-tree): roots are legal
 * entities (unitType === "entity"), each with nested org units (departments,
 * sections, teams) under `children`. Flat list variant lives at /org-units.
 */

import { getSession } from "@/platform/oidc";

export interface OrgTreeNode {
  id: string;
  code: string;
  name: string;
  unitType: string | null; // "entity" | "division" | "department" | "section" | "team" | null
  status: string;
  managerId: string | null;
  managerName: string | null;
  effectiveFrom: string;
  effectiveTo: string | null;
  children: OrgTreeNode[];
}

/** Demo-mode fallback tree so demo screens still show an entity › branch hierarchy. */
export const demoEntityTree: OrgTreeNode[] = [
  {
    id: "entity:ent-zm1",
    code: "ZM1",
    name: "Demo Logistics Zambia Ltd",
    unitType: "entity",
    status: "active",
    managerId: null,
    managerName: null,
    effectiveFrom: "2026-01-01",
    effectiveTo: null,
    children: [
      {
        id: "unit:lusaka-hq",
        code: "LUS",
        name: "Lusaka HQ",
        unitType: "department",
        status: "active",
        managerId: null,
        managerName: null,
        effectiveFrom: "2026-01-01",
        effectiveTo: null,
        children: [
          {
            id: "unit:lusaka-hr",
            code: "HR",
            name: "Human Resources",
            unitType: "team",
            status: "active",
            managerId: null,
            managerName: null,
            effectiveFrom: "2026-01-01",
            effectiveTo: null,
            children: [],
          },
          {
            id: "unit:lusaka-finance",
            code: "FIN",
            name: "Finance",
            unitType: "team",
            status: "active",
            managerId: null,
            managerName: null,
            effectiveFrom: "2026-01-01",
            effectiveTo: null,
            children: [],
          },
        ],
      },
      {
        id: "unit:ndola-plant",
        code: "NDL",
        name: "Ndola Plant",
        unitType: "department",
        status: "active",
        managerId: null,
        managerName: null,
        effectiveFrom: "2026-01-01",
        effectiveTo: null,
        children: [],
      },
      {
        id: "unit:kitwe-depot",
        code: "KTW",
        name: "Kitwe Depot",
        unitType: "department",
        status: "active",
        managerId: null,
        managerName: null,
        effectiveFrom: "2026-01-01",
        effectiveTo: null,
        children: [],
      },
    ],
  },
  {
    id: "entity:ent-zm2",
    code: "ZM2",
    name: "Demo Copperbelt Services Ltd",
    unitType: "entity",
    status: "active",
    managerId: null,
    managerName: null,
    effectiveFrom: "2026-01-01",
    effectiveTo: null,
    children: [
      {
        id: "unit:cb-hq",
        code: "CB",
        name: "Copperbelt HQ",
        unitType: "department",
        status: "active",
        managerId: null,
        managerName: null,
        effectiveFrom: "2026-01-01",
        effectiveTo: null,
        children: [],
      },
    ],
  },
];

const USE_REAL = import.meta.env.VITE_USE_REAL_API === "true";

function authHeaders(): Record<string, string> {
  const accessToken = typeof localStorage !== "undefined" ? getSession()?.accessToken : null;
  return accessToken
    ? { accept: "application/json", Authorization: `Bearer ${accessToken}` }
    : { accept: "application/json" };
}

export interface EntityUnit {
  entityId: string;
  entityName: string;
  unitId: string;
  unitCode: string;
  unitName: string;
  unitType: string | null;
  path: string[]; // [entityName, ..., unitName]
}

/** Full entity tree (roots are entities) from the entity-tree endpoint. */
export async function fetchEntityTree(): Promise<OrgTreeNode[]> {
  if (!USE_REAL) return demoEntityTree;
  const res = await fetch("/api/hrm/admin/org-units/entity-tree", {
    credentials: "include",
    headers: authHeaders(),
  });
  if (!res.ok) return [];
  return (await res.json()) as OrgTreeNode[];
}

/** Flat unit list (includes legalEntityId/name) from the org-units endpoint. */
export async function fetchOrgUnits(): Promise<Record<string, unknown>[]> {
  if (!USE_REAL) return flattenEntityTree(demoEntityTree, false).map((e) => ({
    id: e.unitId, code: e.unitCode, name: e.unitName, unitType: e.unitType,
    legalEntityId: e.entityId, legalEntityName: e.entityName, parentId: null,
    status: "active", effectiveFrom: "2026-01-01", effectiveTo: null,
  }));
  const res = await fetch("/api/hrm/admin/org-units", {
    credentials: "include",
    headers: authHeaders(),
  });
  if (!res.ok) return [];
  return (await res.json()) as Record<string, unknown>[];
}

/** Flatten the entity-rooted tree into one entry per unit with its ancestry path. */
export function flattenEntityTree(
  roots: OrgTreeNode[],
  includeEntities: boolean = true,
): EntityUnit[] {
  const out: EntityUnit[] = [];
  const walk = (node: OrgTreeNode, path: string[], entityId: string) => {
    if (node.unitType === "entity") {
      if (includeEntities) {
        out.push({
          entityId: node.id,
          entityName: node.name,
          unitId: node.id,
          unitCode: node.code,
          unitName: node.name,
          unitType: node.unitType,
          path: [node.name],
        });
      }
      for (const child of node.children) walk(child, [node.name], node.id);
      return;
    }
    const entry: EntityUnit = {
      entityId,
      entityName: path[0] ?? "Unknown entity",
      unitId: node.id,
      unitCode: node.code,
      unitName: node.name,
      unitType: node.unitType,
      path: [...path, node.name],
    };
    out.push(entry);
    for (const child of node.children) walk(child, entry.path, entityId);
  };
  for (const root of roots) walk(root, [], root.id);
  return out;
}

/**
 * Indented tree options for a flat select: "Entity › Department › Team".
 * Returns { label, value } where value is the unit id (entity roots use
 * entity: prefix so entity-level selection never collides with unit ids).
 */
export function treeToSelectOptions(roots: OrgTreeNode[], depth = 0): { label: string; value: string }[] {
  const out: { label: string; value: string }[] = [];
  for (const node of roots) {
    const indent = depth === 0 ? "" : "— ".repeat(Math.min(depth, 3));
    const suffix = node.unitType === "entity" ? " (entity)" : "";
    const value = node.unitType === "entity" ? `entity:${node.id}` : node.id;
    out.push({ label: `${indent}${node.name}${suffix}`, value });
    out.push(...treeToSelectOptions(node.children, depth + 1));
  }
  return out;
}

/** Human-readable path label for a tree entry: "Entity › Dept › Team". */
export function treePathLabel(path: string[]): string {
  return path.join(" › ");
}

/** Build a unit-name lookup (id → "Entity › Unit") from the flat org-units list. */
export function unitPathMap(units: Record<string, unknown>[]): Record<string, string> {
  const byId = new Map<string, Record<string, unknown>>();
  for (const u of units) byId.set(String(u.id ?? ""), u);
  const out: Record<string, string> = {};
  for (const u of units) {
    const chain: string[] = [String(u.name ?? "")];
    let cur = u as Record<string, unknown>;
    while (cur.parentId) {
      const parent = byId.get(String(cur.parentId));
      if (!parent) break;
      chain.unshift(String(parent.name ?? ""));
      cur = parent;
    }
    out[String(u.id ?? "")] = chain.join(" › ");
  }
  return out;
}
