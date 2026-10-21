import { Logo } from './components/brand/Logo'

export default function App() {
  return (
    <main style={{ display: 'grid', placeItems: 'center', height: '100%' }}>
      <Logo size="var(--text-xl)" />
    </main>
  )
}
