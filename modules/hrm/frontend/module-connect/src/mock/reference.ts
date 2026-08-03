/**
 * The option lists forms offer, derived from whatever configures them.
 *
 * A form must never hold its own copy of a list that exists elsewhere. When it
 * does, the copy goes stale silently: a dropdown of grades G1–G7 against data
 * containing G9 renders an employee's grade as blank, and nobody notices until
 * someone saves over it.
 */
import { grades } from "./configuration";
import { orgUnitConfigs } from "./adminconfig";
import { entities } from "./data";

const unique = (xs: string[]) => [...new Set(xs)];

/** Every grade the pay ranges define, plus the marker for roles without one. */
export const gradeOptions = [...grades.map((g) => g.grade), "N/A"];

/** Departments come from the org structure, not from a list typed into a form. */
export const departmentOptions = unique(orgUnitConfigs.map((u) => u.name)).sort();

/** Branches come from the entities that own them. */
export const branchOptions = unique(entities.flatMap((e) => e.branches));

export const workLocationOptions = unique([
  "Lusaka, Lusaka Province",
  "Ndola, Copperbelt Province",
  "Kitwe, Copperbelt Province",
  "Chingola, Copperbelt Province",
  "Solwezi, North-Western Province",
  "Livingstone, Southern Province",
]);
