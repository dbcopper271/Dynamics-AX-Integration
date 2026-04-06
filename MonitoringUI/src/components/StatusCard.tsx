import { CheckCircle2, AlertTriangle, XCircle, HelpCircle } from 'lucide-react'
import type { HealthStatus } from '../types/monitoring'
import { clsx } from 'clsx'

interface StatusCardProps {
  title:  string
  value:  number | string
  status?: HealthStatus
  sub?:   string
  large?: boolean
}

const cfg: Record<HealthStatus, { icon: React.ReactNode; ring: string; bg: string; text: string }> = {
  Healthy:   { icon: <CheckCircle2 />, ring: 'ring-green-500/30',  bg: 'bg-green-500/10',  text: 'text-green-400'  },
  Degraded:  { icon: <AlertTriangle />, ring: 'ring-yellow-500/30', bg: 'bg-yellow-500/10', text: 'text-yellow-400' },
  Unhealthy: { icon: <XCircle />,       ring: 'ring-red-500/30',    bg: 'bg-red-500/10',    text: 'text-red-400'    },
  Unknown:   { icon: <HelpCircle />,    ring: 'ring-gray-500/30',   bg: 'bg-gray-700/30',   text: 'text-gray-400'   },
}

export function StatusCard({ title, value, status, sub, large }: StatusCardProps) {
  const c = status ? cfg[status] : cfg.Unknown
  return (
    <div className={clsx(
      'rounded-xl p-5 ring-1 transition-all duration-300',
      c.ring, c.bg,
    )}>
      <div className="flex items-start justify-between gap-2">
        <div>
          <p className="text-sm text-gray-400 font-medium">{title}</p>
          <p className={clsx('font-bold', large ? 'text-4xl mt-1' : 'text-2xl mt-0.5', c.text)}>
            {value}
          </p>
          {sub && <p className="text-xs text-gray-500 mt-1">{sub}</p>}
        </div>
        <div className={clsx('w-8 h-8 shrink-0 mt-0.5', c.text)}>
          {c.icon}
        </div>
      </div>
    </div>
  )
}

// Simple numeric card (no status color)
export function MetricCard({ title, value, unit, color = 'text-blue-400' }:
  { title: string; value: number | string; unit?: string; color?: string }) {
  return (
    <div className="rounded-xl p-5 ring-1 ring-gray-700/50 bg-gray-800/40">
      <p className="text-sm text-gray-400 font-medium">{title}</p>
      <p className={clsx('text-2xl font-bold mt-0.5', color)}>
        {value}
        {unit && <span className="text-sm font-normal text-gray-500 ml-1">{unit}</span>}
      </p>
    </div>
  )
}
