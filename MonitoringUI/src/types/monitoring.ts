export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy' | 'Unknown'
export type AlertSeverity = 'Info' | 'Warning' | 'Critical'

export interface HealthResult {
  systemName:   string
  category:     string
  status:       HealthStatus
  message:      string
  checkedAt:    string          // ISO-8601
  responseMs:   number
  metrics:      Record<string, number | string>
  errorDetails: string | null
}

export interface HealthSummary {
  healthy:   number
  degraded:  number
  unhealthy: number
  unknown:   number
  total:     number
  asOf:      string
}

export interface AlertRecord {
  id:             string
  systemName:     string
  category:       string
  severity:       AlertSeverity
  currentStatus:  HealthStatus
  previousStatus: HealthStatus
  message:        string
  triggeredAt:    string
  errorDetails:   string | null
}
