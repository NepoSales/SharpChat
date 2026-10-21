import { useContext } from 'react'
import { ActivityContext } from './ActivityContext'

export function useActivity() {
  const context = useContext(ActivityContext)
  if (!context) throw new Error('useActivity must be used inside ActivityProvider')
  return context
}
