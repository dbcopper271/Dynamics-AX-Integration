import type { AlertRecord, HealthResult, HealthSummary } from '../types/monitoring'

const BASE = import.meta.env.VITE_API_URL ?? ''

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`)
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  return res.json() as Promise<T>
}

export const monitoringApi = {
  getAll:     () => get<HealthResult[]>('/api/health'),
  getSummary: () => get<HealthSummary>('/api/health/summary'),
  getAlerts:  (limit = 50) => get<AlertRecord[]>(`/api/alerts?limit=${limit}`),
}

// ─── Mock data (used when API is unavailable / VITE_USE_MOCK=true) ───────────

const MOCK_RESULTS: HealthResult[] = [
  // SAP
  { systemName: 'SAP-PRD',  category: 'SAP',              status: 'Healthy',   message: 'SAP system healthy. Login: 142ms, SID: PRD', checkedAt: new Date().toISOString(), responseMs: 142, metrics: { LoginResponseMs: 142, FreeWorkProcessPercent: 74.2, ShortDumpCount: 1, TransportQueueDepth: 3 }, errorDetails: null },
  { systemName: 'SAP-QAS',  category: 'SAP',              status: 'Degraded',  message: 'Warning: 18.5% DIA WPs free (15/81)', checkedAt: new Date().toISOString(), responseMs: 980, metrics: { LoginResponseMs: 980, FreeWorkProcessPercent: 18.5, ShortDumpCount: 0, TransportQueueDepth: 7 }, errorDetails: null },
  // D365
  { systemName: 'D365-PROD', category: 'Dynamics AX / D365', status: 'Healthy', message: 'D365 healthy. 2 endpoint(s) OK. Avg: 312ms', checkedAt: new Date().toISOString(), responseMs: 312, metrics: { 'Probe_SystemParameters_Ms': 298, 'Probe_LegalEntities_Ms': 326 }, errorDetails: null },
  // Databases
  { systemName: 'SQL-AX-PROD', category: 'Database (SqlServer)', status: 'Healthy', message: 'Database healthy. Probe query: 8ms', checkedAt: new Date().toISOString(), responseMs: 8, metrics: { QueryResponseMs: 8 }, errorDetails: null },
  // Web APIs
  { systemName: 'D365-HealthEndpoint', category: 'Web API / Service', status: 'Healthy',   message: 'HTTP 200 OK in 224ms',  checkedAt: new Date().toISOString(), responseMs: 224, metrics: { HttpStatusCode: 200, ResponseMs: 224 }, errorDetails: null },
  { systemName: 'SAP-Fiori-Launchpad', category: 'Web API / Service', status: 'Degraded',  message: 'Response 4820ms exceeds warning threshold 4000ms', checkedAt: new Date().toISOString(), responseMs: 4820, metrics: { HttpStatusCode: 200, ResponseMs: 4820 }, errorDetails: null },
  { systemName: 'Internal-API-Gateway', category: 'Web API / Service', status: 'Unhealthy', message: 'Request failed: Connection refused', checkedAt: new Date().toISOString(), responseMs: 0, metrics: {}, errorDetails: 'System.Net.Http.HttpRequestException: Connection refused' },
  // Windows Services
  { systemName: 'AX-Batch-Service', category: 'Windows Service', status: 'Healthy', message: "Service 'DynamicsAxBatch' is Running.", checkedAt: new Date().toISOString(), responseMs: 2, metrics: { ServiceStatus: 'Running' }, errorDetails: null },
  // File System
  { systemName: 'AX-FileShare', category: 'File System', status: 'Degraded', message: 'Warning: 1124 MB free (threshold: 2048 MB)', checkedAt: new Date().toISOString(), responseMs: 5, metrics: { FreeSpaceMB: 1124, TotalSpaceMB: 51200 }, errorDetails: null },
]

const MOCK_SUMMARY: HealthSummary = {
  healthy:   5,
  degraded:  3,
  unhealthy: 1,
  unknown:   0,
  total:     9,
  asOf:      new Date().toISOString(),
}

const MOCK_ALERTS: AlertRecord[] = [
  { id: '1', systemName: 'Internal-API-Gateway', category: 'Web API / Service', severity: 'Critical', currentStatus: 'Unhealthy', previousStatus: 'Healthy',  message: 'Request failed: Connection refused', triggeredAt: new Date(Date.now() - 3 * 60000).toISOString(), errorDetails: null },
  { id: '2', systemName: 'SAP-Fiori-Launchpad',  category: 'Web API / Service', severity: 'Warning',  currentStatus: 'Degraded',  previousStatus: 'Healthy',  message: 'Response 4820ms exceeds warning threshold 4000ms', triggeredAt: new Date(Date.now() - 7 * 60000).toISOString(), errorDetails: null },
  { id: '3', systemName: 'AX-FileShare',          category: 'File System',       severity: 'Warning',  currentStatus: 'Degraded',  previousStatus: 'Healthy',  message: 'Warning: 1124 MB free (threshold: 2048 MB)', triggeredAt: new Date(Date.now() - 12 * 60000).toISOString(), errorDetails: null },
  { id: '4', systemName: 'SAP-QAS',               category: 'SAP',               severity: 'Warning',  currentStatus: 'Degraded',  previousStatus: 'Healthy',  message: 'Warning: 18.5% DIA WPs free (15/81)', triggeredAt: new Date(Date.now() - 22 * 60000).toISOString(), errorDetails: null },
  { id: '5', systemName: 'SQL-AX-PROD',           category: 'Database (SqlServer)', severity: 'Info',  currentStatus: 'Healthy',   previousStatus: 'Degraded', message: 'Database healthy. Probe query: 8ms', triggeredAt: new Date(Date.now() - 45 * 60000).toISOString(), errorDetails: null },
]

export const mockApi = {
  getAll:     () => Promise.resolve([...MOCK_RESULTS]),
  getSummary: () => Promise.resolve({ ...MOCK_SUMMARY }),
  getAlerts:  () => Promise.resolve([...MOCK_ALERTS]),
}

export const useMock = import.meta.env.VITE_USE_MOCK === 'true' || import.meta.env.DEV
export const api = useMock ? mockApi : monitoringApi
