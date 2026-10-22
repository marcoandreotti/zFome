import { defineStore } from 'pinia'
import { ref } from 'vue'
import { platformClient, type AddUnitInput, type PlatformOwner, type PlatformTenant, type ProvisionTenantInput, type ProvisioningResult } from './client'

export const usePlatformStore = defineStore('platform', () => {
  const tenants = ref<PlatformTenant[]>([])
  const total = ref(0)
  const owners = ref<PlatformOwner[]>([])
  const busy = ref(false)
  const lastIntentKey = ref('')
  async function load(search = '') { busy.value = true; try { const result = await platformClient.search(search); tenants.value = result.items; total.value = result.totalCount } finally { busy.value = false } }
  async function provision(input: Omit<ProvisionTenantInput, 'intentKey'>): Promise<ProvisioningResult> {
    lastIntentKey.value ||= crypto.randomUUID()
    const result = await platformClient.provision({ ...input, intentKey: lastIntentKey.value })
    lastIntentKey.value = ''; await load(); return result
  }
  async function addUnit(tenantId: string, input: Omit<AddUnitInput, 'intentKey'>): Promise<ProvisioningResult> {
    lastIntentKey.value ||= crypto.randomUUID()
    const result = await platformClient.addUnit(tenantId, { ...input, intentKey: lastIntentKey.value })
    lastIntentKey.value = ''; await load(); return result
  }
  async function loadOwners(establishmentId: string) { owners.value = await platformClient.owners(establishmentId) }
  async function recover() { if (!lastIntentKey.value) return undefined; return await platformClient.recover(lastIntentKey.value) }
  function resetIntent() { lastIntentKey.value = '' }
  return { tenants, owners, total, busy, lastIntentKey, load, loadOwners, provision, addUnit, recover, resetIntent }
})
