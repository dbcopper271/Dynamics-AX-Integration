import {
  RadialBarChart, RadialBar, ResponsiveContainer, Tooltip,
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Cell,
} from 'recharts'
import type { HealthResult, HealthStatus } from '../types/monitoring'

// ── Donut summary chart ───────────────────────────────────────────────────────
interface DonutChartProps { results: HealthResult[] }

const STATUS_COLORS: Record<HealthStatus, string> = {
  Healthy:   '#22c55e',
  Degraded:  '#f59e0b',
  Unhealthy: '#ef4444',
  Unknown:   '#64748b',
}

export function SummaryDonut({ results }: DonutChartProps) {
  const counts = results.reduce(
    (acc, r) => { acc[r.status] = (acc[r.status] ?? 0) + 1; return acc },
    {} as Record<string, number>
  )

  const data = (Object.entries(counts) as [HealthStatus, number][]).map(([name, value]) => ({
    name, value, fill: STATUS_COLORS[name],
  }))

  return (
    <div className="rounded-xl ring-1 ring-gray-800 bg-gray-900/40 p-5">
      <h3 className="text-sm font-semibold text-gray-300 mb-4">Status Overview</h3>
      <div className="h-52">
        <ResponsiveContainer width="100%" height="100%">
          <RadialBarChart
            cx="50%" cy="50%"
            innerRadius="40%" outerRadius="85%"
            data={data}
            startAngle={90} endAngle={-270}
          >
            <RadialBar dataKey="value" cornerRadius={4} />
            <Tooltip
              contentStyle={{ backgroundColor: '#1f2937', border: '1px solid #374151', borderRadius: 8, fontSize: 12 }}
              itemStyle={{ color: '#e5e7eb' }}
            />
          </RadialBarChart>
        </ResponsiveContainer>
      </div>
      <div className="flex flex-wrap justify-center gap-3 mt-1">
        {data.map(d => (
          <div key={d.name} className="flex items-center gap-1.5 text-xs text-gray-400">
            <span className="w-2.5 h-2.5 rounded-full" style={{ background: d.fill }} />
            {d.name} ({d.value})
          </div>
        ))}
      </div>
    </div>
  )
}

// ── Response time bar chart ───────────────────────────────────────────────────
interface ResponseChartProps { results: HealthResult[] }

export function ResponseTimeChart({ results }: ResponseChartProps) {
  const data = results
    .filter(r => r.responseMs > 0)
    .sort((a, b) => b.responseMs - a.responseMs)
    .slice(0, 10)
    .map(r => ({
      name:   r.systemName.length > 14 ? r.systemName.slice(0, 13) + '…' : r.systemName,
      ms:     r.responseMs,
      status: r.status,
    }))

  if (data.length === 0) return null

  return (
    <div className="rounded-xl ring-1 ring-gray-800 bg-gray-900/40 p-5">
      <h3 className="text-sm font-semibold text-gray-300 mb-4">Response Times (ms)</h3>
      <div className="h-52">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data} layout="vertical" margin={{ left: 0, right: 20, top: 0, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#374151" horizontal={false} />
            <XAxis type="number" tick={{ fill: '#9ca3af', fontSize: 11 }} axisLine={false} tickLine={false} />
            <YAxis type="category" dataKey="name" width={110} tick={{ fill: '#9ca3af', fontSize: 11 }} axisLine={false} tickLine={false} />
            <Tooltip
              contentStyle={{ backgroundColor: '#1f2937', border: '1px solid #374151', borderRadius: 8, fontSize: 12 }}
              itemStyle={{ color: '#e5e7eb' }}
              formatter={(v: number) => [`${v} ms`, 'Response time']}
            />
            <Bar dataKey="ms" radius={[0, 4, 4, 0]}>
              {data.map((d, i) => (
                <Cell key={i} fill={STATUS_COLORS[d.status as HealthStatus] ?? '#3b82f6'} />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  )
}
