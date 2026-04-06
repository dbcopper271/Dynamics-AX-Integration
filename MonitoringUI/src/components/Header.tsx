import { Activity, RefreshCw } from 'lucide-react'
import type { HealthSummary } from '../types/monitoring'

interface HeaderProps {
  summary:     HealthSummary | null
  lastRefresh: Date | null
  loading:     boolean
  onRefresh:   () => void
}

export function Header({ summary, lastRefresh, loading, onRefresh }: HeaderProps) {
  const time = lastRefresh
    ? lastRefresh.toLocaleTimeString('en-GB', { hour12: false })
    : '—'

  return (
    <header className="bg-gray-900 border-b border-gray-800 px-6 py-4">
      <div className="max-w-screen-2xl mx-auto flex items-center justify-between gap-4">
        {/* Logo & title */}
        <div className="flex items-center gap-3">
          <div className="p-2 rounded-lg bg-blue-600">
            <Activity className="w-5 h-5 text-white" />
          </div>
          <div>
            <h1 className="text-lg font-bold text-white leading-tight">IT System Monitor</h1>
            <p className="text-xs text-gray-400">SAP &amp; Non-SAP Real-time Dashboard</p>
          </div>
        </div>

        {/* Summary pills */}
        {summary && (
          <div className="hidden sm:flex items-center gap-2">
            <Pill count={summary.healthy}   label="Healthy"   color="bg-green-500/20 text-green-400 ring-green-500/30" />
            <Pill count={summary.degraded}  label="Degraded"  color="bg-yellow-500/20 text-yellow-400 ring-yellow-500/30" />
            <Pill count={summary.unhealthy} label="Unhealthy" color="bg-red-500/20 text-red-400 ring-red-500/30" />
          </div>
        )}

        {/* Refresh */}
        <div className="flex items-center gap-3 text-sm text-gray-400">
          <span className="hidden md:block">Updated {time}</span>
          <button
            onClick={onRefresh}
            disabled={loading}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-gray-800
                       hover:bg-gray-700 text-gray-300 hover:text-white transition-colors
                       disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
            Refresh
          </button>
        </div>
      </div>
    </header>
  )
}

function Pill({ count, label, color }: { count: number; label: string; color: string }) {
  return (
    <span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium ring-1 ${color}`}>
      <span className="font-bold text-sm">{count}</span>
      {label}
    </span>
  )
}
