import axios, { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from 'axios'
import { beforeEach, describe, expect, it } from 'vitest'
import { hydrateCartFromCatalog, loadCart, usePublicCart } from '../src/modules/public-ordering/cart'
import { createPublicOrderingClient } from '../src/modules/public-ordering/client'
import { applyPublicTheme, contrast } from '../src/modules/public-ordering/theme'
import { pollingDelay, terminalOrderStatuses } from '../src/modules/public-ordering/tracking'
import { checkoutValidation } from '../src/modules/public-ordering/checkout'

beforeEach(() => localStorage.clear())

describe('carrinho público versionado', () => {
  it('restaura somente a intenção da mesma unidade', () => {
    loadCart('unit-a')
    usePublicCart().add({
      key: 'line-1', productId: 'product', variationId: null, quantity: 2,
      notes: 'sem cebola', additionals: [], productName: 'Pizza',
      variationName: null, displayedUnitPrice: 20
    })
    const persisted = localStorage.getItem('orderhub.public-cart.unit-a')!
    expect(persisted).not.toMatch(/displayedUnitPrice|productName|variationName/)
    loadCart('unit-b')
    expect(usePublicCart().state.items).toHaveLength(0)
    loadCart('unit-a')
    hydrateCartFromCatalog({
      establishmentId: 'ignored', establishmentName: 'Unit', slug: 'unit-a',
      categories: [{
        id: 'category', parentId: null, name: 'Pizzas', description: null,
        order: 0, imageUrl: null, isActive: true,
        products: [{
          id: 'product', code: 'P', name: 'Pizza', description: null,
          basePrice: 20, isFeatured: false, isActive: true, allowsNotes: true,
          images: [], variations: [], additionalGroups: []
        }]
      }]
    })
    expect(usePublicCart().state.items[0]).toMatchObject({
      productId: 'product', productName: 'Pizza', displayedUnitPrice: 20,
      quantity: 2, notes: 'sem cebola'
    })
  })

  it('descarta estado incompatível ou inválido', () => {
    localStorage.setItem('orderhub.public-cart.unit', JSON.stringify({
      version: 0, slug: 'unit', items: [{ productId: 'old' }]
    }))
    loadCart('unit')
    expect(usePublicCart().state.items).toHaveLength(0)
    expect(localStorage.getItem('orderhub.public-cart.unit')).toBeNull()
  })

  it('preserva a intenção e sinaliza item que ficou indisponível', () => {
    localStorage.setItem('orderhub.public-cart.unit', JSON.stringify({
      version: 1, slug: 'unit', items: [{
        key: 'line', productId: 'product', variationId: null,
        quantity: 1, notes: null, additionals: []
      }]
    }))
    loadCart('unit')
    hydrateCartFromCatalog({
      establishmentId: 'ignored', establishmentName: 'Unit', slug: 'unit',
      categories: [{
        id: 'category', parentId: null, name: 'Pizzas', description: null,
        order: 0, imageUrl: null, isActive: true,
        products: [{
          id: 'product', code: 'P', name: 'Pizza', description: null,
          basePrice: 20, isFeatured: false, isActive: true, allowsNotes: true,
          isAvailable: false, unavailabilityReason: 'Sem estoque',
          images: [], variations: [], additionalGroups: []
        }]
      }]
    })
    expect(usePublicCart().state.items[0]?.productName).toBe('Pizza (indisponível)')
    expect(usePublicCart().state.items).toHaveLength(1)
  })
})

describe('cliente HTTP público', () => {
  it('usa somente slug/token e não transporta TenantId', async () => {
    const http = axios.create()
    let request: InternalAxiosRequestConfig | undefined
    http.defaults.adapter = async config => {
      request = config
      return {
        config, status: 200, statusText: 'OK', headers: new AxiosHeaders(),
        data: { establishmentName: 'Unit', slug: 'unit', theme: {}, table: null, paymentMethods: [] }
      }
    }
    await createPublicOrderingClient(http).context('unit', 'opaque-token')
    expect(request?.url).toBe('/api/public/ordering/unit/context')
    expect(request?.params).toEqual({ tableToken: 'opaque-token' })
    expect(JSON.stringify(request)).not.toMatch(/tenantId/i)
  })

  it('converte falha HTTP em ProblemDetails preservando a explicação', async () => {
    const http = axios.create()
    http.defaults.adapter = async config => {
      const response = {
        config, status: 409, statusText: 'Conflict', headers: new AxiosHeaders(),
        data: { title: 'Oferta alterada', detail: 'Recalcule o pedido.' }
      }
      throw new AxiosError('Conflict', 'ERR_BAD_REQUEST', config, undefined, response)
    }
    await expect(createPublicOrderingClient(http).catalog('unit')).rejects.toMatchObject({
      message: 'Recalcule o pedido.'
    })
  })
})

it('mantém contraste mínimo ao aplicar tema público inseguro', () => {
  const publicSurface = document.createElement('div')
  publicSurface.dataset.surface = 'public'
  document.body.append(publicSurface)
  applyPublicTheme({
    establishmentName: 'Unit', slug: 'unit', table: null, paymentMethods: [],
    theme: {
      primaryColor: '#ffffff', secondaryColor: '#ffffff',
      backgroundColor: '#ffffff', textColor: '#ffffff',
      fontFamily: '<script>', logoUrl: null
    }
  })
  const style = publicSurface.style
  expect(contrast(style.getPropertyValue('--oh-brand-primary'), '#ffffff')).toBeGreaterThanOrEqual(4.5)
  expect(contrast(style.getPropertyValue('--oh-text-primary'), '#ffffff')).toBeGreaterThanOrEqual(4.5)
  expect(style.getPropertyValue('--oh-font-family')).toBe('system-ui, sans-serif')
  expect(document.documentElement.style.getPropertyValue('--oh-brand-primary')).toBe('')
  publicSurface.remove()
})

it('isola o tema público ao navegar para outra superfície', () => {
  const publicSurface = document.createElement('div')
  publicSurface.dataset.surface = 'public'
  const administrationSurface = document.createElement('div')
  administrationSurface.dataset.surface = 'administration'
  document.body.append(publicSurface, administrationSurface)

  applyPublicTheme({
    establishmentName: 'Expressiva', slug: 'expressiva', table: null, paymentMethods: [],
    theme: {
      primaryColor: '#7f1d1d', secondaryColor: '#14532d',
      backgroundColor: '#fff7ed', textColor: '#431407',
      fontFamily: 'Georgia, serif', logoUrl: null
    }
  }, publicSurface)

  expect(publicSurface.style.getPropertyValue('--oh-font-family')).toBe('Georgia, serif')
  expect(administrationSurface.style.getPropertyValue('--oh-font-family')).toBe('')
  expect(administrationSurface.style.getPropertyValue('--oh-brand-primary')).toBe('')
  expect(document.documentElement.style.getPropertyValue('--oh-font-family')).toBe('')

  publicSurface.remove()
  administrationSurface.remove()
})

it('aplica backoff limitado e reconhece estados terminais do acompanhamento', () => {
  expect([0, 1, 2, 8].map(pollingDelay)).toEqual([5000, 10000, 20000, 60000])
  expect(terminalOrderStatuses.has('Completed')).toBe(true)
  expect(terminalOrderStatuses.has('Preparing')).toBe(false)
})

it('valida dados necessários para mesa, retirada e entrega', () => {
  const emptyAddress = {
    label: 'Principal', street: '', number: '', complement: null,
    neighborhood: '', city: '', state: '', postalCode: ''
  }
  const validAddress = {
    ...emptyAddress, street: 'Rua A', number: '10', neighborhood: 'Centro',
    city: 'São Paulo', state: 'SP', postalCode: '01001000'
  }
  expect(checkoutValidation('Table', 'pay', { name: '', phone: '' }, emptyAddress)).toBe('')
  expect(checkoutValidation('Pickup', 'pay', { name: '', phone: '' }, emptyAddress)).toContain('nome')
  expect(checkoutValidation('Pickup', 'pay', { name: 'Ana', phone: '1199' }, emptyAddress)).toContain('telefone')
  expect(checkoutValidation('Pickup', 'pay', { name: 'Ana', phone: '11999999999' }, emptyAddress)).toBe('')
  expect(checkoutValidation('Delivery', 'pay', { name: 'Ana', phone: '1199' }, emptyAddress)).toContain('telefone')
  expect(checkoutValidation('Delivery', 'pay', { name: 'Ana', phone: '11999999999' }, emptyAddress)).toContain('endereço')
  expect(checkoutValidation('Delivery', 'pay', { name: 'Ana', phone: '11999999999' }, validAddress)).toBe('')
  expect(checkoutValidation('Table', '', { name: '', phone: '' }, emptyAddress)).toContain('pagamento')
})
