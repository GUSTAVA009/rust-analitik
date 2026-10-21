'use strict';

/**
 * HAPYCH Mobile Preview (read-only diagnostic mirror)
 *
 * This is NOT an authentication gateway. It never forwards cookies,
 * Authorization headers, client bodies, POST requests or credentials.
 * It proxies only anonymous GET / and fixed public /static/* assets.
 * Disable and replace with a properly audited origin once route works.
 */

const http = require('node:http');

const PORT = Number(process.env.PORT || 10000);
const ORIGIN = 'https://my.hapychrust.ru';
const MAX_BYTES = 4 * 1024 * 1024;
const MAX_REQUESTS_PER_MINUTE = 100;
const TIMEOUT_MS = 9500;
const RELEASE = 'preview-20261021-r1';

const counts = new Map();
const banner =
  '<div id="hapych-mobile-preview-banner" role="status" ' +
  'style="position:relative;z-index:2147483647;background:#3b240d;color:#fff4e3;' +
  'border-bottom:2px solid #e59b44;text-align:center;padding:14px 10px;' +
  'font:700 14px/1.5 system-ui,sans-serif">' +
  'HAPYCH — тест мобильного доступа. Только просмотр. ' +
  'Вход, оплата и получение ключей на этом адресе отключены.' +
  '</div>';

function deny(res, status, message) {
  res.writeHead(status, {
    'Content-Type': 'text/plain; charset=utf-8',
    'Cache-Control': 'no-store',
    'X-Content-Type-Options': 'nosniff',
    'Referrer-Policy': 'no-referrer'
  });
  res.end(message);
}

function isAllowedPath(path) {
  if (path === '/') return true;
  if (!path.startsWith('/static/')) return false;
  if (path.includes('..') || path.includes('//')) return false;
  return /^\/static\/[a-zA-Z0-9_./-]+\.(?:css|js|mjs|png|jpg|jpeg|svg|webp|woff2?|ttf|ico|gif|map|json|webmanifest)$/.test(path);
}

function publicSecurityHeaders() {
  return {
    'X-Content-Type-Options': 'nosniff',
    'Referrer-Policy': 'no-referrer',
    'X-Frame-Options': 'DENY',
    'Content-Security-Policy':
      "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; " +
      "script-src 'self'; font-src 'self'; connect-src 'self'; " +
      "form-action 'none'; base-uri 'none'; frame-ancestors 'none'",
    'Cache-Control': 'no-store',
    'Cross-Origin-Resource-Policy': 'same-origin',
    'X-Hapych-Preview': RELEASE
  };
}

async function loadPublic(pathAndQuery, req, res) {
  const controller = new AbortController();
  const t = setTimeout(() => controller.abort(), TIMEOUT_MS);
  try {
    const upstream = await fetch(ORIGIN + pathAndQuery, {
      method: 'GET',
      redirect: 'manual',
      signal: controller.signal,
      headers: {
        'Accept': req.headers.accept || 'text/html,application/xhtml+xml,*/*',
        'User-Agent': 'HAPYCH-Public-Mobile-Preview/1.0',
      }
    });

    if (upstream.status < 200 || upstream.status >= 300) {
      deny(res, 502, 'Источник временно недоступен. HTTP ' + upstream.status);
      return;
    }

    const contentType = upstream.headers.get('content-type') || 'application/octet-stream';
    const declaredSize = Number(upstream.headers.get('content-length') || 0);
    if (declaredSize > MAX_BYTES) {
      deny(res, 413, 'Публичный ресурс слишком большой для просмотра.');
      return;
    }

    const array = await upstream.arrayBuffer();
    if (array.byteLength > MAX_BYTES) {
      deny(res, 413, 'Публичный ресурс слишком большой для просмотра.');
      return;
    }

    let body = Buffer.from(array);
    if (pathAndQuery.startsWith('/?') || pathAndQuery === '/') {
      if (!contentType.toLowerCase().includes('text/html')) {
        deny(res, 502, 'Источник вернул неожиданный формат страницы.');
        return;
      }
      const html = body.toString('utf8');
      if (!/<body(?:\s|>)/i.test(html)) {
        deny(res, 502, 'Не удалось распознать страницу источника.');
        return;
      }
      body = Buffer.from(html.replace(/<body([^>]*)>/i, function (full, attrs) {
        return '<body' + attrs + '>' + banner;
      }));
    }

    const headers = publicSecurityHeaders();
    headers['Content-Type'] = contentType;
    headers['Content-Length'] = String(body.length);
    // Deliberately NEVER forward any Set-Cookie, Location, CORS or auth header.
    res.writeHead(200, headers);
    res.end(body);
  } catch (error) {
    deny(res, 502, 'Не получилось загрузить публичный ресурс HAPYCH. ' +
      (error.name === 'AbortError' ? 'Таймаут источника.' : 'Ошибка сети.'));
  } finally {
    clearTimeout(t);
  }
}

const server = http.createServer((req, res) => {
  if (req.url === '/healthz') {
    const body = JSON.stringify({ ok: true, release: RELEASE, mode: 'public-read-only' });
    res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8',
      'Content-Length': String(Buffer.byteLength(body)), 'Cache-Control': 'no-store' });
    res.end(body);
    return;
  }
  if (req.method !== 'GET') {
    deny(res, 405, 'Этот тестовый адрес разрешает только просмотр. Авторизация и оплата отключены.');
    return;
  }

  const ip = String(req.socket.remoteAddress || 'unknown');
  const key = Math.floor(Date.now() / 60000) + ':' + ip;
  const count = (counts.get(key) || 0) + 1;
  counts.set(key, count);
  if (count > MAX_REQUESTS_PER_MINUTE) {
    deny(res, 429, 'Слишком много проверок. Попробуйте через минуту.');
    return;
  }
  if (counts.size > 5000) {
    const prefix = Math.floor(Date.now() / 60000) - 1;
    for (const k of counts.keys()) {
      if (Number(k.split(':', 1)[0]) < prefix) counts.delete(k);
    }
  }

  let url;
  try { url = new URL(req.url, 'https://preview.invalid'); }
  catch { deny(res, 400, 'Некорректный адрес.'); return; }

  if (url.search.length > 2048 || !isAllowedPath(url.pathname)) {
    deny(res, 404, 'Тестовый режим: доступна только публичная главная страница и её оформление.');
    return;
  }

  loadPublic(url.pathname + url.search, req, res);
});

server.requestTimeout = 15000;
server.headersTimeout = 16000;
server.listen(PORT, '0.0.0.0', () => {
  process.stdout.write('HAPYCH mobile public preview active on port ' + PORT + '\n');
});
