// Types mirror the C# records in Erp.Api (Kpis/KpiService.cs, Imports/ImportEndpoints.cs).

export type Dataset = 'Articles' | 'Suppliers' | 'StockMovements' | 'SalesOrders' | 'PurchaseOrders';

export interface MonthPoint { month: string; issues: number; receipts: number }
export interface GroupPoint { group: string; stockValue: number; cogs365: number; turnover: number | null; slowMovers: number }
export interface WeekPoint { bucket: string; value: number; lines: number; overdue: boolean }
export interface SupplierRow {
  number: string; name: string; city: string | null; received: number; onTime: number;
  onTimeRate: number | null; avgDelayLate: number | null; open: number; openOverdue: number;
}
export interface Overview {
  today: string; stockValue: number; cogs365: number; turnover: number | null;
  slowMoverCount: number; slowMoverValue: number; backlogValue: number; backlogLines: number;
  overdueValue: number; overdueLines: number; onTimeRate: number | null;
  openPurchaseLines: number; overduePurchaseLines: number;
  trend: MonthPoint[]; groups: GroupPoint[]; backlogByWeek: WeekPoint[]; suppliers: SupplierRow[];
}

export interface StockRow {
  number: string; name: string; group: string; unit: string; unitCost: number; supplierNumber: string | null;
  onHand: number; value: number; issued365: number; turnover: number | null; daysOfSupply: number | null;
  lastIssue: string | null; lastMovement: string | null; slowMover: boolean;
}
export interface BacklogRow {
  orderNumber: string; lineNo: number; customer: string; articleNumber: string; articleName: string;
  quantity: number; unitPrice: number; value: number; orderDate: string; dueDate: string; daysOverdue: number;
}
export interface PurchaseRow {
  orderNumber: string; lineNo: number; supplierNumber: string; supplierName: string; articleNumber: string; articleName: string;
  quantity: number; orderDate: string; promisedDate: string; receivedDate: string | null; delayDays: number | null; state: string;
}
export interface MovementRow { id: number; articleNumber: string; date: string; quantity: number; type: string; reference: string | null }
export interface Page<T> { total: number; page: number; size: number; rows: T[] }

export interface FieldDto { key: string; label: string; type: string; required: boolean }
export interface SchemaDto { dataset: Dataset; label: string; description: string; fields: FieldDto[] }
export interface Suggestion { fieldKey: string; column: number | null; confidence: number }
export interface Analyzed {
  uploadId: string; fileName: string; format: string; rowCount: number; headers: string[]; sample: string[][]; suggestions: Suggestion[];
}
export interface RowError { row: number; field: string; value: string; message: string }
export interface Checked {
  totalRows: number; validRows: number; invalidRows: number; missingFields: string[];
  numberStyles: Record<string, string>; errors: RowError[]; errorSummary: { message: string; count: number }[];
  preview: Record<string, string>[];
}
export interface Committed { batchId: string; inserted: number; updated: number; rejected: number }
export interface Batch {
  id: string; dataset: Dataset; datasetLabel: string; fileName: string; createdAt: string;
  rowsTotal: number; inserted: number; updated: number; rejected: number; status: string; canRollback: boolean;
}
export interface Sample { file: string; dataset: Dataset; datasetLabel: string; title: string; description: string }
export interface WorkspaceInfo { created: boolean; createdAt: string; expiresAfterIdleHours: number; today: string }

export class ApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

async function handle<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const problem = await res.json().catch(() => null);
    throw new ApiError(res.status, problem?.detail ?? problem?.title ?? `Fehler ${res.status}`);
  }
  return res.status === 204 ? (undefined as T) : res.json();
}

export const get = <T,>(path: string) => fetch(path, { credentials: 'same-origin' }).then(handle<T>);

export const post = <T,>(path: string, body?: unknown) =>
  fetch(path, {
    method: 'POST',
    credentials: 'same-origin',
    headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  }).then(handle<T>);

export function analyze(file: File, dataset: Dataset) {
  const form = new FormData();
  form.append('file', file);
  form.append('dataset', dataset);
  return fetch('/api/imports/analyze', { method: 'POST', body: form, credentials: 'same-origin' }).then(handle<Analyzed>);
}

/** Query string from a filter object, skipping empty values. */
export function qs(params: Record<string, string | number | boolean | undefined | null>) {
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== '') p.set(k, String(v));
  const s = p.toString();
  return s ? `?${s}` : '';
}
