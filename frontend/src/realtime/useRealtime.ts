import { useContext } from 'react'
import { RealtimeContext } from './RealtimeContext'

export function useRealtime() {
  const context = useContext(RealtimeContext)
  if (!context) throw new Error('useRealtime must be used inside RealtimeProvider')
  return context
}
