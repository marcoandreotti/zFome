import type { SessionContext } from '../modules/session/store'
import type { Router } from 'vue-router'

interface NavigationSession {
  context: SessionContext | null
  hydrate: () => Promise<void>
  clear: () => void
}

export function installAccessGuard(router: Router, session: NavigationSession) {
  router.beforeEach(async (to) => {
    if (!to.meta.requiresSession) return true
    try {
      if (!session.context) await session.hydrate()
    } catch {
      session.clear()
      return '/login'
    }
    return (
      accessDestination(
        session.context,
        to.path,
        to.meta.capability as string | undefined,
        to.meta.platformOnly as boolean | undefined
      ) ?? true
    )
  })
}

/** Guards melhoram a navegação; a autorização definitiva continua no servidor. */
export function sessionLandingPath(context: SessionContext | null): string {
  if (!context) return '/login'
  if (context.passwordChangeRequired) return '/change-password'
  if (context.isPlatformUser) return '/platform'
  if (context.capabilities.includes('order-read')) return '/operations'
  if (context.capabilities.includes('management')) return '/administration'
  if (context.capabilities.includes('customer-operations'))
    return '/administration/customers'
  return '/access-denied'
}

export function accessDestination(
  context: SessionContext | null,
  path: string,
  capability?: string,
  platformOnly?: boolean
): string | undefined {
  if (!context) return '/login'
  if (context.passwordChangeRequired && path !== '/change-password')
    return '/change-password'
  if (!context.passwordChangeRequired && path === '/change-password')
    return sessionLandingPath(context)
  if (platformOnly && !context.isPlatformUser) return '/access-denied'
  if (capability && !context.capabilities.includes(capability))
    return '/access-denied'
}
