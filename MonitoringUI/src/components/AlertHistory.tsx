import { Bell, CheckCircle2, AlertTriangle, XCircle, TrendingDown, TrendingUp } from 'lucide-react'
import { clsx } from 'clsx'
import type { AlertRecord, AlertSeverity } from '../types/monitoring'

const severityCfg: Record<AlertSeverity, { icon: React.ReactNode; bar: string; text: string; bg: string }> = {
  Critical: { icon: <XCircle className="w-4 h-4" />,       bar: 'bg-red-500',    text: 'text-red-400',    bg: 'bg-red-500/10' },
  Warning:  { icon: <AlertTriangle className="w-4 h-4" />, bar: 'bg-yellow-500', text: 'text-yellow-400', bg: 'bg-yellow-500/10' },
  Info:     { icon: <CheckCircle2 className="w-4 h-4" />,  bar: 'bg-green-500',  text: 'text-green-400',  bg: 'bg-green-500/10' },
}

interface AlertHistoryProps { alerts: AlertRecord[] }

export function AlertHistory({ alerts }: AlertHistoryProps) {
  return (
    <div className="rounded-xl ring-1 ring-gray-800 overflow-hidden border-t-2 border-orange-500">
      <div className="px-5 py-3 bg-gray-900 flex items-center gap-2">
        <Bell className="w-4 h-4 text-orange-400" />
        <h2 className="font-semibold text-gray-200 text-sm">Alert History</h2>
        {alerts.length > 0 && (
          <span className="ml-auto bg-orange-500/20 text-orange-400 ring-1 ring-orange-500/30
                           text-xs font-medium px-2 py-0.5 rounded-full">
            {alerts.length}
          </span>
        )}
      </div>

      {alerts.length === 0 ? (
        <div className="py-10 text-center text-gray-500 text-sm">
          <CheckCircle2 className="w-8 h-8 mx-auto mb-2 text-green-500/50" />
          No alerts in the last period
        </div>
      ) : (
        <ul className="divide-y divide-gray-800/50 max-h-96 overflow-y-auto">
          {alerts.map((a) => {
            const s = severityCfg[a.severity]
            const isRecovery = a.severity === 'Info'
            const ago = formatAgo(new Date(a.triggeredAt))
            return (
              <li key={a.id} className={clsx('flex gap-0 transition-colors hover:bg-gray-800/30', s.bg, 'hover:opacity-90')}>
                {/* Color bar */}
                <div className={clsx('w-1 shrink-0', s.bar)} />

                <div className="flex-1 px-4 py-3">
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex items-center gap-2">
                      <span className={clsx('shrink-0', s.text)}>{s.icon}</span>
                      <span className="font-medium text-gray-200 text-sm">{a.systemName}</span>
                      <span className="text-xs text-gray-500">{a.category}</span>
                    </div>
                    <span className="text-xs text-gray-500 shrink-0">{ago}</span>
                  </div>
                  <p className="text-xs text-gray-400 mt-1 ml-6">
                    {isRecovery
                      ? <span className="inline-flex items-center gap-1"><TrendingUp className="w-3 h-3 text-green-400" /> Recovery: {a.message}</span>
                      : <span className="inline-flex items-center gap-1"><TrendingDown className="w-3 h-3 text-red-400" /> {a.previousStatus} → {a.currentStatus}: {a.message}</span>
                    }
                  </p>
                </div>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}

function formatAgo(date: Date): string {
  const diff = Math.floor((Date.now() - date.getTime()) / 1000)
  if (diff < 60) return `${diff}s ago`
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`
  return `${Math.floor(diff / 3600)}h ago`
}
