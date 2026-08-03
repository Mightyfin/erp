/**
 * Digital employee file (HRM-006). Self-contained mock data + async reader.
 *
 * The product rule this data encodes: a document's CLASSIFICATION decides who
 * may see it, independently of who it is about. HR seeing a contract does not
 * imply HR seeing an occupational-health outcome.
 */
const delay = (ms = 420) => new Promise((r) => setTimeout(r, ms));

export type Classification = "General" | "Confidential" | "Restricted";

export interface EmployeeDocument {
  id: string;
  employeeId: string;
  name: string;
  category: string;
  classification: Classification;
  /** Plain-language statement of who can open it. */
  visibleTo: string;
  version: number;
  supersedes?: string;
  issued: string;
  /** Set where the document stops being valid and needs renewing. */
  expires?: string;
  signature?: "Not required" | "Awaiting signature" | "Signed";
  signedOn?: string;
  retention: string;
  legalHold?: string;
  /** Access history is itself part of the record for sensitive documents. */
  lastAccess?: { by: string; at: string };
  sizeKb: number;
}

export interface DocumentTemplate {
  id: string;
  name: string;
  category: string;
  mergeFields: string[];
  requiresSignature: boolean;
  lastUpdated: string;
}

export const employeeDocuments: EmployeeDocument[] = [
  {
    id: "DOC-1001-01",
    employeeId: "w-1001",
    name: "Employment contract — permanent",
    category: "Contract",
    classification: "Confidential",
    visibleTo: "The employee, their HR administrator and Payroll",
    version: 2,
    supersedes: "DOC-1001-01 v1 (2019 original)",
    issued: "2024-01-15",
    signature: "Signed",
    signedOn: "2024-01-18",
    retention: "7 years after employment ends",
    lastAccess: { by: "Thandiwe Banda (HR operations)", at: "2026-07-22" },
    sizeKb: 412,
  },
  {
    id: "DOC-1001-02",
    employeeId: "w-1001",
    name: "Grade change letter — G7 to G8",
    category: "Employment change",
    classification: "Confidential",
    visibleTo: "The employee, their line manager and HR",
    version: 1,
    issued: "2026-07-14",
    signature: "Awaiting signature",
    retention: "7 years after employment ends",
    sizeKb: 96,
  },
  {
    id: "DOC-1001-03",
    employeeId: "w-1001",
    name: "Code of conduct acknowledgement 2026",
    category: "Policy acknowledgement",
    classification: "General",
    visibleTo: "The employee and HR",
    version: 1,
    issued: "2026-01-08",
    expires: "2027-01-08",
    signature: "Signed",
    signedOn: "2026-01-09",
    retention: "3 years",
    sizeKb: 48,
  },
  {
    id: "DOC-1004-01",
    employeeId: "w-1004",
    name: "Fixed-term contract — expires 31 Aug 2026",
    category: "Contract",
    classification: "Confidential",
    visibleTo: "The employee, their HR administrator and Payroll",
    version: 1,
    issued: "2024-02-01",
    expires: "2026-08-31",
    signature: "Signed",
    signedOn: "2024-01-29",
    retention: "7 years after employment ends",
    sizeKb: 388,
  },
  {
    id: "DOC-1004-02",
    employeeId: "w-1004",
    name: "Welding certificate EN ISO 9606-1",
    category: "Qualification",
    classification: "General",
    visibleTo: "The employee, their line manager and HR",
    version: 1,
    issued: "2023-09-30",
    expires: "2026-09-30",
    retention: "Duration of employment plus 3 years",
    sizeKb: 220,
  },
  {
    id: "DOC-1008-01",
    employeeId: "w-1008",
    name: "Occupational health outcome — fitness to work",
    category: "Occupational health",
    classification: "Restricted",
    visibleTo: "Occupational health and the employee only. Not visible to HR administrators or the line manager.",
    version: 1,
    issued: "2026-06-02",
    retention: "Duration of employment plus 3 years",
    lastAccess: { by: "Occupational health service", at: "2026-06-02" },
    sizeKb: 64,
  },
  {
    id: "DOC-1008-02",
    employeeId: "w-1008",
    name: "Counterbalance forklift licence",
    category: "Licence",
    classification: "General",
    visibleTo: "The employee, their line manager and HR",
    version: 1,
    issued: "2023-08-14",
    expires: "2026-08-14",
    retention: "Duration of employment plus 3 years",
    sizeKb: 156,
  },
  {
    id: "DOC-1006-01",
    employeeId: "w-1006",
    name: "Contractor engagement agreement",
    category: "Contract",
    classification: "Confidential",
    visibleTo: "The contractor, HR and Procurement",
    version: 1,
    issued: "2025-11-03",
    signature: "Signed",
    signedOn: "2025-11-01",
    retention: "7 years after the engagement ends",
    legalHold:
      "Retained beyond the normal schedule while a related procurement query is open. Cannot be deleted until the hold is lifted.",
    sizeKb: 274,
  },
];

export const templates: DocumentTemplate[] = [
  {
    id: "TPL-CONTRACT-PERM",
    name: "Permanent employment contract",
    category: "Contract",
    mergeFields: ["Full name", "Job title", "Grade", "Start date", "Salary", "Legal entity", "Notice period"],
    requiresSignature: true,
    lastUpdated: "2026-04-02",
  },
  {
    id: "TPL-CHANGE-GRADE",
    name: "Grade or salary change letter",
    category: "Employment change",
    mergeFields: ["Full name", "Current grade", "New grade", "Effective date", "New salary"],
    requiresSignature: true,
    lastUpdated: "2026-02-11",
  },
  {
    id: "TPL-POLICY-ACK",
    name: "Policy acknowledgement",
    category: "Policy acknowledgement",
    mergeFields: ["Full name", "Policy name", "Policy version", "Acknowledgement date"],
    requiresSignature: false,
    lastUpdated: "2026-01-05",
  },
  {
    id: "TPL-SEPARATION",
    name: "Service certificate",
    category: "Separation",
    mergeFields: ["Full name", "Job title", "Start date", "End date", "Period of service"],
    requiresSignature: false,
    lastUpdated: "2025-11-20",
  },
];

export const documentsApi = {
  all: async () => {
    await delay();
    return employeeDocuments;
  },
  templates: async () => {
    await delay(300);
    return templates;
  },
};
