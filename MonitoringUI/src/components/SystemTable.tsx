import { CheckCircle2, AlertTriangle, XCircle, HelpCircle, ChevronDown, ChevronRight } from 'lucide-react'
import { useState } from 'react'
import { clsx } from 'clsx'
import type { HealthResult, HealthStatus } from '../types/monitoring'

interface SystemTableProps {
  results:    HealthResult[]
  title:      string
  icon?:      React.ReactNode
  accentColor?: string
}

const statusCfg: Record<HealthStatus, { icon: React.ReactNode; badge: string; row: string }> = {
  Healthy:   { icon: <CheckCircle2 className="w-4 h-4" />, badge: 'bg-green-500/20 text-green-400 ring-green-500/30',  row: 'hover:bg-green-500/5' },
  Degraded:  { icon: <AlertTriangle className="w-4 h-4" />, badge: 'bg-yellow-500/20 text-yellow-400 ring-yellow-500/30', row: 'hover:bg-yellow-500/5' },
  Unhealthy: { icon: <XCircle className="w-4 h-4" />,       badge: 'bg-red-500/20 text-red-400 ring-red-500/30',       row: 'hover:bg-red-500/5' },
  Unknown:   { icon: <HelpCircle className="w-4 h-4" />,    badge: 'bg-gray-700/50 text-gray-400 ring-gray-600/30',    row: 'hover:bg-gray-700/20' },
}

export function SystemTable({ results, title, icon, accentColor = 'border-blue-500' }: SystemTableProps) {
  const [expandedRow, setExpandedRow] = useState<string | null>(null)

  // Sort: Unhealthy → Degraded → Unknown → Healthy
  const order: HealthStatus[] = ['Unhealthy', 'Degraded', 'Unknown', 'Healthy']
  const sorted = [...results].sort(
    (a, b) => order.indexOf(a.status) - order.indexOf(b.status)
  )

  if (results.length === 0) return null

  return (
    <div className={`rounded-xl overflow-hidden ring-1 ring-gray-800 border-t-2 ${accentColor}`}>
      {/* Table header */}
      <div className="px-5 py-3 bg-gray-900 flex items-center gap-2">
        {icon && <span className="text-gray-400">{icon}</span>}
        <h2 className="font-semibold text-gray-200 text-sm">{title}</h2>
        <span className="ml-auto text-xs text-gray-500">{results.length} system(s)</span>
      </div>

      <table className="w-full text-sm">
        <thead>
          <tr className="bg-gray-800/60 text-gray-400 text-xs uppercase tracking-wider">
            <th className="px-5 py-2.5 text-left w-10"></th>
            <th className="px-5 py-2.5 text-left">System</th>
            <th className="px-5 py-2.5 text-left hidden md:table-cell">Category</th>
            <th className="px-5 py-2.5 text-left">Status</th>
            <th className="px-5 py-2.5 text-left hidden lg:table-cell">Message</th>
            <th className="px-5 py-2.5 text-right hidden sm:table-cell">Resp (ms)</th>
            <th className="px-5 py-2.5 text-right hidden xl:table-cell">Checked</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-800/50">
          {sorted.map((r) => {
            const s = statusCfg[r.status]
            const isExpanded = expandedRow === r.systemName
            const hasMetrics = Object.keys(r.metrics).length > 0 || r.errorDetails

            return (
              <>
                <tr
                  key={r.systemName}
                  className={clsx('transition-colors', s.row, hasMetrics && 'cursor-pointer')}
                  onClick={() => hasMetrics && setExpandedRow(isExpanded ? null : r.systemName)}
                >
                  {/* Expand toggle */}
                  <td className="px-4 py-3 text-gray-600">
                    {hasMetrics
                      ? (isExpanded ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />)
                      : null}
                  </td>

                  {/* System name */}
                  <td className="px-5 py-3 font-medium text-gray-200 whitespace-nowrap">
                    {r.systemName}
                  </td>

                  {/* Category */}
                  <td className="px-5 py-3 text-gray-400 hidden md:table-cell whitespace-nowrap">
                    {r.category}
                  </td>

                  {/* Status badge */}
                  <td className="px-5 py-3">
                    <span className={clsx(
                      'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium ring-1',
                      s.badge
                    )}>
                      {s.icon}
                      {r.status}
                    </span>
                  </td>

                  {/* Message */}
                  <td className="px-5 py-3 text-gray-400 hidden lg:table-cell max-w-xs truncate">
                    {r.message}
                  </td>

                  {/* Response time */}
                  <td className="px-5 py-3 text-right tabular-nums hidden sm:table-cell">
                    <span className={clsx(
                      'text-sm',
                      r.responseMs > 5000 ? 'text-red-400' :
                      r.responseMs > 2000 ? 'text-yellow-400' : 'text-gray-400'
                    )}>
                      {r.responseMs > 0 ? r.responseMs : '—'}
                    </span>
                  </td>

                  {/* Checked at */}
                  <td className="px-5 py-3 text-right text-gray-500 text-xs whitespace-nowrap hidden xl:table-cell">
                    {new Date(r.checkedAt).toLocaleTimeString('en-GB', { hour12: false })}
                  </td>
                </tr>

                {/* Expanded metrics / error row */}
                {isExpanded && (
                  <tr key={`${r.systemName}-detail`} className="bg-gray-900/80">
                    <td colSpan={7} className="px-10 py-4">
                      {Object.keys(r.metrics).length > 0 && (
                        <div className="flex flex-wrap gap-3 mb-3">
                          {Object.entries(r.metrics).map(([k, v]) => (
                            <div key={k} className="bg-gray-800 rounded-lg px-3 py-2 text-xs">
                              <span className="text-gray-500">{k}: </span>
                              <span className="text-gray-200 font-mono font-medium">
                                {typeof v === 'number' ? v.toLocaleString() : v}
                              </span>
                            </div>
                          ))}
                        </div>
                      )}
                      {r.errorDetails && (
                        <pre className="text-xs text-red-400 bg-red-900/20 rounded-lg p-3 overflow-x-auto whitespace-pre-wrap">
                          {r.errorDetails}
                        </pre>
                      )}
                    </td>
                  </tr>
                )}
              </>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
