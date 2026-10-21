import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { api } from '../src/http/client'
import { usePlatformStore } from '../src/modules/platform/store'
import { accessDestination, sessionLandingPath } from '../src/router/access'

beforeEach(() => { setActivePinia(createPinia()); vi.restoreAllMocks() })

describe('administração de plataforma', () => {
  it('envia retry com a mesma chave de intenção até obter sucesso', async () => {
    const post = vi.spyOn(api, 'post').mockRejectedValueOnce(new Error('network')).mockResolvedValueOnce({ data: { tenantId: 't', establishmentId: 'e', ownerId: 'o', tenantPublicCode: 'TEN', nextStep: '/administration/onboarding/dados' } })
    vi.spyOn(api, 'get').mockResolvedValue({ data: { items: [], totalCount: 0, page: 1, pageSize: 20 } })
    const store = usePlatformStore(); const input = { tenantName: 'Tenant', tenantPublicCode: 'TEN', establishmentName: 'Unit', establishmentSlug: 'unit', timeZoneId: 'America/Sao_Paulo', ownerName: 'Owner', ownerEmail: 'owner@test.local', temporaryPassword: 'temporary-password' }
    await expect(store.provision(input)).rejects.toThrow('network')
    const key = store.lastIntentKey
    await store.provision(input)
    expect(post.mock.calls[0][1]).toMatchObject({ intentKey: key })
    expect(post.mock.calls[1][1]).toMatchObject({ intentKey: key })
  })
  it('leva plataforma para seu shell e nega usuário tenant', () => {
    const platform = { passwordChangeRequired: false, isPlatformUser: true, capabilities: ['management'], establishments: [] }
    expect(sessionLandingPath(platform)).toBe('/platform')
    expect(accessDestination(platform, '/platform', undefined, true)).toBeUndefined()
    expect(accessDestination({ ...platform, isPlatformUser: false }, '/platform', undefined, true)).toBe('/access-denied')
  })
})
