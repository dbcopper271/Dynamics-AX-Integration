import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from '../api/monitoringApi'
import type { AlertRecord, HealthResult, HealthSummary } from '../types/monitoring'

interface MonitoringState {
  results:     HealthResult[]
  summary:     HealthSummary | null
  alerts:      AlertRecord[]
  lastRefresh: Date | null
  loading:     boolean
  error:       string | null
}

export function useMonitoring(refreshIntervalMs = 30_000) {
  const [state, setState] = useState<MonitoringState>({
    results: [], summary: null, alerts: [], lastRefresh: null, loading: true, error: null,
  })
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const refresh = useCallback(async () => {
    try {
      const [results, summary, alerts] = await Promise.all([
        api.getAll(),
        api.getSummary(),
        api.getAlerts(50),
      ])
      setState({ results, summary, alerts, lastRefresh: new Date(), loading: false, error: null })
    } catch (err) {
      setState(s => ({ ...s, loading: false, error: String(err) }))
    }
  }, [])

  useEffect(() => {
    refresh()
    timerRef.current = setInterval(refresh, refreshIntervalMs)
    return () => { if (timerRef.current) clearInterval(timerRef.current) }
  }, [refresh, refreshIntervalMs])

  return { ...state, refresh }
}
