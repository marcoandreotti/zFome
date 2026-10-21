import { createServer } from 'node:http'
import { readFile } from 'node:fs/promises'
import { extname, resolve, sep } from 'node:path'

const root = resolve('dist/spa')
let status = 'Confirmed'

const ticket = () => ({
  id: 'order-42',
  number: 42,
  serviceType: 'Table',
  status,
  customerName: 'Maria de Souza',
  tableCode: 'A1',
  confirmedAt: new Date(Date.now() - 18 * 60_000).toISOString(),
  preparationStartedAt:
    status === 'Preparing'
      ? new Date(Date.now() - 5 * 60_000).toISOString()
      : null,
  action: status === 'Confirmed' ? 'StartPreparation' : 'MarkReady',
  items: [
    {
      id: 'item-1',
      productName: 'Hambúrguer da casa',
      variationName: 'Duplo',
      quantity: 2,
      notes: 'Sem cebola; ponto da carne bem passado',
      additionals: [
        { name: 'Molho especial', quantity: 1 },
        { name: 'Bacon', quantity: 2 }
      ]
    }
  ]
})

const server = createServer(async (request, response) => {
  const url = new URL(request.url, 'http://localhost')
  if (url.pathname === '/api/auth/context') {
    response.setHeader('Content-Type', 'application/json')
    response.end(JSON.stringify({
      passwordChangeRequired: false,
      isPlatformUser: false,
      capabilities: ['order-read', 'order-kitchen'],
      establishments: [{ id: 'unit', name: 'Unidade Centro' }]
    }))
    return
  }
  if (url.pathname.startsWith('/api/') && url.pathname.endsWith('/kitchen')) {
    response.setHeader('Content-Type', 'application/json')
    response.end(JSON.stringify(status === 'Ready' ? [] : [ticket()]))
    return
  }
  if (request.method === 'POST' && url.pathname.startsWith('/api/') && url.pathname.endsWith('/prepare')) {
    status = 'Preparing'
    response.writeHead(204).end()
    return
  }
  if (request.method === 'POST' && url.pathname.startsWith('/api/') && url.pathname.endsWith('/ready')) {
    status = 'Ready'
    response.writeHead(204).end()
    return
  }
  if (url.pathname.startsWith('/hubs/')) {
    response.writeHead(503).end()
    return
  }

  const requested = resolve(root, '.' + decodeURIComponent(url.pathname))
  if (requested !== root && !requested.startsWith(root + sep)) {
    response.writeHead(403).end()
    return
  }
  const file = extname(requested) ? requested : resolve(root, 'index.html')
  try {
    response.setHeader('Content-Type', {
      '.js': 'text/javascript',
      '.css': 'text/css',
      '.html': 'text/html',
      '.svg': 'image/svg+xml'
    }[extname(file)] ?? 'application/octet-stream')
    response.end(await readFile(file))
  } catch {
    response.writeHead(404).end()
  }
})

server.listen(4174, '127.0.0.1', () => {
  console.log('Kitchen browser fixture: http://127.0.0.1:4174/operations/kitchen')
})
