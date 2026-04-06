import { Header } from './components/Header'
import { Dashboard } from './components/Dashboard'
import { useMonitoring } from './hooks/useMonitoring'

export default function App() {
  const { results, summary, alerts, lastRefresh, loading, error, refresh } = useMonitoring(30_000)

  return (
    <div className="min-h-screen bg-gray-950 flex flex-col">
      <Header
        summary={summary}
        lastRefresh={lastRefresh}
        loading={loading}
        onRefresh={refresh}
      />
      <main className="flex-1 overflow-auto">
        <Dashboard
          results={results}
          summary={summary}
          alerts={alerts}
          loading={loading}
          error={error}
        />
      </main>
      <footer className="text-center text-xs text-gray-600 py-3 border-t border-gray-800">
        IT System Monitoring Dashboard · Auto-refresh every 30s · {new Date().getFullYear()}
      </footer>
    </div>
  )
}
