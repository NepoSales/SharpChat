import type { ConversationRole } from '../api/conversations'

const rank: Record<ConversationRole, number> = { Owner: 3, Admin: 2, Member: 1 }

export function canManageMembers(role: ConversationRole) {
  return role === 'Owner' || role === 'Admin'
}

export function canRemove(actor: ConversationRole, target: ConversationRole) {
  return rank[actor] > rank[target]
}

export function canChangeRoles(role: ConversationRole) {
  return role === 'Owner'
}
