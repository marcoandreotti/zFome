import { createRequire } from 'node:module'
import { createServer } from 'node:http'
import { readFile, mkdir } from 'node:fs/promises'
import { resolve, extname, sep } from 'node:path'
import assert from 'node:assert/strict'

// Execute após npm run build; usa Playwright disponível no ambiente, sem alterar dependências.
const require = createRequire(import.meta.url)
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright')
const root = resolve('dist/spa'),
  output = resolve(
    process.env.BROWSER_CHECK_OUTPUT ||
      '../../TestResults/administration-browser'
  )
await mkdir(output, { recursive: true })
const server = createServer(async (request, response) => {
  const requested = resolve(
    root,
    '.' + decodeURIComponent(new URL(request.url, 'http://localhost').pathname)
  )
  if (requested !== root && !requested.startsWith(root + sep)) {
    response.writeHead(403).end()
    return
  }
  const file = extname(requested) ? requested : resolve(root, 'index.html')
  try {
    response.setHeader(
      'Content-Type',
      {
        '.js': 'text/javascript',
        '.css': 'text/css',
        '.html': 'text/html',
        '.svg': 'image/svg+xml'
      }[extname(file)] || 'application/octet-stream'
    )
    response.end(await readFile(file))
  } catch {
    response.writeHead(404).end()
  }
})
await new Promise((done) => server.listen(0, '127.0.0.1', done))
const origin = `http://127.0.0.1:${server.address().port}`
const browser = await chromium.launch({
  headless: true,
  ...(process.env.BROWSER_EXECUTABLE
    ? { executablePath: process.env.BROWSER_EXECUTABLE }
    : {})
})
function contrast(foreground, background) {
  const luminance = (color) => {
    const channels = color
      .match(/[\d.]+/g)
      .slice(0, 3)
      .map(Number)
      .map((value) => {
        const channel = value / 255
        return channel <= 0.04045
          ? channel / 12.92
          : ((channel + 0.055) / 1.055) ** 2.4
      })
    return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722
  }
  const values = [luminance(foreground), luminance(background)].sort(
    (a, b) => b - a
  )
  return (values[0] + 0.05) / (values[1] + 0.05)
}
async function checkContrast(locator) {
  const colors = await locator.evaluate((node) => {
    const style = getComputedStyle(node)
    return [style.color, style.backgroundColor]
  })
  assert(
    contrast(...colors) >= 4.5,
    `Contraste insuficiente: ${colors.join(' / ')}`
  )
}
const failures = []
const ownerCapabilities = [
  'management',
  'administration',
  'ownership',
  'customer-operations',
  'promotion-management',
  'payment-management'
]
const row = {
  id: 'item',
  name: 'Complemento artesanal',
  price: 2,
  isActive: true,
  order: 0
}
const group = {
  id: 'group',
  name: 'Complementos',
  minimumSelection: 0,
  maximumSelection: 2,
  isActive: true,
  order: 0,
  items: [row]
}
const product = {
  id: 'product',
  name: 'Pizza da casa',
  code: 'PIZZA',
  description: null,
  basePrice: 45,
  isActive: true,
  isFeatured: false,
  allowsNotes: true,
  images: [],
  variations: [],
  additionalGroups: [group]
}
const customer = {
  id: 'customer',
  name: 'Maria de Souza',
  phone: '11999999999',
  email: 'maria@example.test',
  addresses: [
    {
      id: 'address',
      label: 'Casa',
      street: 'Rua das Flores',
      number: '10',
      complement: null,
      neighborhood: 'Centro',
      city: 'São Paulo',
      state: 'SP',
      postalCode: '01001000',
      isPrimary: true
    }
  ]
}
async function fixtures(page, capabilities = ownerCapabilities) {
  page.on('pageerror', (failure) => failures.push(failure.message))
  await page.route('**/api/**', async (route) => {
    const url = new URL(route.request().url()),
      path = url.pathname
    let body
    if (path === '/api/auth/context')
      body = {
        passwordChangeRequired: false,
        isPlatformUser: false,
        capabilities,
        establishments: [
          { id: 'unit', name: 'Unidade Centro' },
          { id: 'second', name: 'Unidade Norte' }
        ]
      }
    else if (path.endsWith('/catalog/'))
      body = {
        establishmentId: 'unit',
        establishmentName: 'Unidade Centro',
        slug: 'centro',
        categories: [
          {
            id: 'category',
            name: path.includes('/second/') ? 'Categoria Norte' : 'Pizzas',
            description: null,
            parentId: null,
            order: 0,
            imageUrl: null,
            isActive: true,
            products: [product]
          }
        ]
      }
    else if (path.endsWith('/additionals'))
      body = {
        total: 21,
        items: [
          {
            ...row,
            name:
              url.searchParams.get('page') === '2'
                ? 'Adicional da segunda página'
                : row.name
          }
        ]
      }
    else if (path.endsWith('/additional-groups'))
      body = { total: 1, items: [group] }
    else if (path.endsWith('/customers')) body = { total: 1, items: [customer] }
    else if (path.endsWith('/coupons'))
      body = {
        total: 1,
        items: [
          {
            id: 'coupon',
            code: 'BOASVINDAS',
            description: null,
            discountType: 'Percentage',
            value: 10,
            minimumOrder: 20,
            startsAt: '2026-09-03T12:00:00Z',
            endsAt: '2026-12-31T12:00:00Z',
            maximumUses: 100,
            usedCount: 3,
            isActive: true
          }
        ]
      }
    else if (path.endsWith('/payment-methods'))
      body = {
        total: 1,
        items: [
          {
            id: 'cash',
            code: 'CASH',
            name: 'Dinheiro',
            isOnline: false,
            allowsChange: true,
            isActive: true
          }
        ]
      }
    else if (path.endsWith('/users'))
      body = {
        totalCount: 1,
        items: [
          {
            id: 'owner',
            name: 'Ana Proprietária',
            email: 'ana@example.test',
            isActive: true,
            roles: [1],
            establishmentIds: ['unit'],
            isCurrentUser: false
          }
        ]
      }
    if (route.request().method() !== 'GET') {
      await route.fulfill({
        status: 409,
        contentType: 'application/problem+json',
        body: JSON.stringify({
          status: 409,
          detail: 'Conflito de teste: dados preservados.'
        })
      })
      return
    }
    if (!body) throw new Error(`Fixture ausente: ${path}`)
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(body)
    })
  })
}
try {
  for (const width of [1440, 768]) {
    const page = await browser.newPage({ viewport: { width, height: 1024 } })
    await fixtures(page)
    for (const [path, title, create] of [
      ['catalog', 'Catálogo', 'Cadastrar'],
      ['customers', 'Clientes', 'Cadastrar cliente'],
      ['coupons', 'Cupons', 'Cadastrar cupom'],
      ['payment-methods', 'Formas de pagamento', 'Cadastrar forma'],
      ['users', 'Usuários', 'Novo usuário']
    ]) {
      await page.goto(`${origin}/administration/${path}`)
      await page.getByRole('heading', { name: title, exact: true }).waitFor()
      await page
        .getByRole('button', { name: /Editar|Gerenciar/ })
        .first()
        .waitFor()
      await page.screenshot({
        path: resolve(output, `${path}-${width}.png`),
        fullPage: true
      })
      assert(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth
        ),
        `${path}: overflow ${width}: ${JSON.stringify(
          await page.evaluate(() =>
            [...document.querySelectorAll('body *')]
              .filter(
                (node) =>
                  node.getBoundingClientRect().right > innerWidth &&
                  getComputedStyle(node).position !== 'fixed'
              )
              .slice(0, 10)
              .map((node) => ({
                tag: node.tagName,
                class: node.className,
                right: node.getBoundingClientRect().right
              }))
          )
        )}`
      )
      if (create) {
        const button = page.getByRole('button', { name: create, exact: true })
        await checkContrast(button)
        await button.focus()
        await page.keyboard.press('Enter')
        const dialog = page.getByRole('dialog')
        await dialog.waitFor()
        assert(
          await dialog.getAttribute('aria-label'),
          `${path}: diálogo sem nome acessível`
        )
        await page.waitForFunction(() =>
          document
            .querySelector('[role="dialog"]')
            ?.contains(document.activeElement)
        )
        await page.keyboard.press('Tab')
        assert(
          await dialog.evaluate((node) =>
            node.contains(document.activeElement)
          ),
          `${path}: foco escapou do diálogo`
        )
        await page.screenshot({
          path: resolve(output, `${path}-form-${width}.png`),
          fullPage: true
        })
        await page.keyboard.press('Escape')
        await dialog.waitFor({ state: 'hidden' })
      }
    }
    await page.goto(`${origin}/administration/catalog`)
    await page.getByRole('tab', { name: 'Produtos', exact: true }).click()
    await page
      .getByRole('button', { name: 'Editar Pizza da casa', exact: true })
      .click()
    await page
      .getByRole('textbox', { name: 'Código', exact: true })
      .fill('DUPLICADO')
    await page.getByRole('button', { name: 'Salvar', exact: true }).click()
    await page.getByRole('button', { name: 'Confirmar', exact: true }).click()
    await page
      .getByRole('alert')
      .filter({ hasText: 'Conflito de teste' })
      .waitFor()
    await page
      .getByRole('dialog', {
        name: 'Confirmar alterações do catálogo',
        exact: true
      })
      .waitFor({ state: 'hidden' })
    await page.getByRole('alert').scrollIntoViewIfNeeded()
    await checkContrast(page.getByRole('alert'))
    assert.equal(
      await page
        .getByRole('textbox', { name: 'Código', exact: true })
        .inputValue(),
      'DUPLICADO'
    )
    await page.screenshot({
      path: resolve(output, `product-conflict-${width}.png`),
      fullPage: true
    })
    await page.close()
  }
  const switching = await browser.newPage({
    viewport: { width: 1440, height: 1024 }
  })
  await fixtures(switching)
  await switching.goto(`${origin}/administration/catalog`)
  await switching.getByRole('cell', { name: 'Pizzas', exact: true }).waitFor()
  await switching.getByRole('combobox', { name: 'Unidade ativa' }).click()
  await switching.getByRole('option', { name: 'Unidade Norte' }).click()
  await switching
    .getByRole('cell', { name: 'Categoria Norte', exact: true })
    .waitFor()
  assert.equal(
    await switching.getByRole('cell', { name: 'Pizzas', exact: true }).count(),
    0
  )
  await switching.getByRole('link', { name: 'Clientes', exact: true }).click()
  await switching
    .getByRole('heading', { name: 'Clientes', exact: true })
    .waitFor()
  await switching.waitForFunction(
    () => document.activeElement?.id === 'admin-content'
  )
  await switching.close()
  const admin = await browser.newPage()
  await fixtures(
    admin,
    ownerCapabilities.filter((capability) => capability !== 'ownership')
  )
  await admin.goto(`${origin}/administration/users`)
  await admin
    .getByRole('button', { name: 'Gerenciar Ana Proprietária' })
    .click()
  assert.equal(
    await admin.getByRole('checkbox', { name: 'Owner', exact: true }).count(),
    0
  )
  assert.equal(
    await admin.getByRole('button', { name: 'Desativar usuário' }).count(),
    0
  )
  await admin.close()
  const denied = await browser.newPage()
  await fixtures(denied, ['kitchen'])
  await denied.goto(`${origin}/administration/catalog`)
  await denied.waitForURL('**/access-denied')
  await denied.close()
  const attendant = await browser.newPage()
  await fixtures(attendant, ['customer-operations'])
  await attendant.goto(`${origin}/administration/customers`)
  await attendant
    .getByRole('heading', { name: 'Clientes', exact: true })
    .waitFor()
  assert.equal(
    await attendant
      .getByRole('link', { name: 'Usuários', exact: true })
      .count(),
    0
  )
  await attendant.goto(`${origin}/administration/coupons`)
  await attendant.waitForURL('**/access-denied')
  await attendant.close()
  assert.deepEqual(failures, [])
  console.log(
    'PASS: desktop/tablet, teclado/foco, formulários, conflito, acesso por capacidade; screenshots:',
    output
  )
} finally {
  await browser.close()
  await new Promise((done) => server.close(done))
}
