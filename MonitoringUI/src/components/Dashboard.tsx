import { Server, Database, Globe, HardDrive, Cpu, FolderOpen } from 'lucide-react'
import { clsx } from 'clsx'
import type { HealthResult, HealthSummary } from '../types/monitoring'
import { StatusCard } from './StatusCard'
import { SystemTable } from './SystemTable'
import { AlertHistory } from './AlertHistory'
import { SummaryDonut, ResponseTimeChart } from './MetricChart'
import type { AlertRecord } from '../types/monitoring'

interface DashboardProps {
  results:  HealthResult[]
  summary:  HealthSummary | null
  alerts:   AlertRecord[]
  loading:  boolean
  error:    string | null
}

export function Dashboard({ results, summary, alerts, loading, error }: DashboardProps) {
  if (loading && results.length === 0) return <LoadingSkeleton />
  if (error) return <ErrorBanner message={error} />

  // Group by category prefix
  const sap    = results.filter(r => r.category === 'SAP')
  const d365   = results.filter(r => r.category.startsWith('Dynamics'))
  const db     = results.filter(r => r.category.startsWith('Database'))
  const web    = results.filter(r => r.category.startsWith('Web'))
  const svc    = results.filter(r => r.category === 'Windows Service')
  const fs     = results.filter(r => r.category === 'File System')

  // Overall status for summary cards
  const worstStatus = (items: HealthResult[]) =>
    items.some(r => r.status === 'Unhealthy') ? 'Unhealthy' :
    items.some(r => r.status === 'Degraded')  ? 'Degraded'  :
    items.length === 0 ? 'Unknown' : 'Healthy'

  return (
    <div className="max-w-screen-2xl mx-auto px-4 sm:px-6 py-6 space-y-6">

      {/* ── Top summary cards ── */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-4">
        <StatusCard title="SAP Systems"     value={sap.length}  status={worstStatus(sap)}  sub={`${sap.filter(r=>r.status==='Healthy').length} healthy`} />
        <StatusCard title="Dynamics AX"     value={d365.length} status={worstStatus(d365)} sub={`${d365.filter(r=>r.status==='Healthy').length} healthy`} />
        <StatusCard title="Databases"       value={db.length}   status={worstStatus(db)}   sub={`${db.filter(r=>r.status==='Healthy').length} healthy`} />
        <StatusCard title="Web APIs"        value={web.length}  status={worstStatus(web)}  sub={`${web.filter(r=>r.status==='Healthy').length} healthy`} />
        <StatusCard title="Win Services"    value={svc.length}  status={worstStatus(svc)}  sub={`${svc.filter(r=>r.status==='Healthy').length} healthy`} />
        <StatusCard title="File Systems"    value={fs.length}   status={worstStatus(fs)}   sub={`${fs.filter(r=>r.status==='Healthy').length} healthy`} />
      </div>

      {/* ── Charts row ── */}
      {summary && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <SummaryDonut results={results} />
          <ResponseTimeChart results={results} />
        </div>
      )}

      {/* ── System tables ── */}
      <div className="space-y-4">
        {sap.length > 0 && (
          <SystemTable
            results={sap}
            title="SAP Systems"
            icon={<Cpu className="w-4 h-4" />}
            accentColor="border-blue-500"
          />
        )}
        {d365.length > 0 && (
          <SystemTable
            results={d365}
            title="Dynamics AX / D365"
            icon={<Server className="w-4 h-4" />}
            accentColor="border-purple-500"
          />
        )}
        {db.length > 0 && (
          <SystemTable
            results={db}
            title="Databases"
            icon={<Database className="w-4 h-4" />}
            accentColor="border-cyan-500"
          />
        )}
        {web.length > 0 && (
          <SystemTable
            results={web}
            title="Web APIs & Services"
            icon={<Globe className="w-4 h-4" />}
            accentColor="border-green-500"
          />
        )}
        {svc.length > 0 && (
          <SystemTable
            results={svc}
            title="Windows Services"
            icon={<HardDrive className="w-4 h-4" />}
            accentColor="border-orange-500"
          />
        )}
        {fs.length > 0 && (
          <SystemTable
            results={fs}
            title="File Systems"
            icon={<FolderOpen className="w-4 h-4" />}
            accentColor="border-pink-500"
          />
        )}
      </div>

      {/* ── Alerts panel ── */}
      <AlertHistory alerts={alerts} />
    </div>
  )
}

function LoadingSkeleton() {
  return (
    <div className="max-w-screen-2xl mx-auto px-6 py-6">
      <div className="grid grid-cols-3 lg:grid-cols-6 gap-4 mb-6">
        {[...Array(6)].map((_, i) => (
          <div key={i} className="h-24 rounded-xl bg-gray-800 animate-pulse" />
        ))}
      </div>
      <div className="h-64 rounded-xl bg-gray-800 animate-pulse mb-4" />
      <div className="h-48 rounded-xl bg-gray-800 animate-pulse" />
    </div>
  )
}

function ErrorBanner({ message }: { message: string }) {
  return (
    <div className="max-w-screen-2xl mx-auto px-6 py-8">
      <div className={clsx('rounded-xl ring-1 ring-red-500/30 bg-red-500/10 p-6 text-red-400')}>
        <p className="font-semibold mb-1">Failed to load monitoring data</p>
        <p className="text-sm text-red-300/80">{message}</p>
        <p className="text-xs text-red-400/60 mt-3">
          Make sure the backend is running: <code>dotnet run -- --api</code>
        </p>
      </div>
    </div>
  )
}
