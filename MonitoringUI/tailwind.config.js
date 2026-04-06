/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        healthy:   { DEFAULT: '#22c55e', light: '#dcfce7', text: '#15803d' },
        degraded:  { DEFAULT: '#f59e0b', light: '#fef3c7', text: '#b45309' },
        unhealthy: { DEFAULT: '#ef4444', light: '#fee2e2', text: '#b91c1c' },
        unknown:   { DEFAULT: '#94a3b8', light: '#f1f5f9', text: '#64748b' },
        sap:       { DEFAULT: '#0070f2', light: '#e0f0ff', text: '#0059c0' },
      },
      animation: {
        'pulse-slow': 'pulse 3s cubic-bezier(0.4,0,0.6,1) infinite',
      },
    },
  },
  plugins: [],
}
