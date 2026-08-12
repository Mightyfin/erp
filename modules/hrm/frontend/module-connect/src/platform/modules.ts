export interface ErpModule {
  id: string;
  label: string;
  description: string;
  available: boolean;
  to: string;
}

/**
 * The ERP's module catalogue. HRM is the only module with a real
 * implementation; the rest are named here so the entrance and the in-app
 * module switcher agree on the roadmap instead of drifting apart.
 */
export const modules: ErpModule[] = [
  { id: "hrm", label: "Human resources", description: "People, time, payroll and HR administration.", available: true, to: "/hrm" },
  { id: "finance", label: "Finance", description: "General ledger, payables, receivables, fixed assets.", available: false, to: "/finance" },
  { id: "procurement", label: "Procurement", description: "Purchasing, suppliers and requisitions.", available: false, to: "/procurement" },
  { id: "inventory", label: "Inventory", description: "Stock, warehousing and movements.", available: false, to: "/inventory" },
  { id: "accounting", label: "Accounting", description: "Corporate books, journals and reporting.", available: false, to: "/accounting" },
];
