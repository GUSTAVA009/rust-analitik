'use strict';

/**
 * HAPYCH Mobile Access - isolated HTTPS customer portal bridge.
 * The original Flask application continues to own all accounts, sessions,
 * passwords, subscriptions and customer data. This bridge owns no database.
 *
 * Deliberately disallowed during beta: payment creation, account changes,
 * account deletion, mail reset/verification tokens, and admin functionality.
 * Never log paths, query strings, cookies, responses or POST bodies.
 */
const http = require('node:http');
const net = require('node:net');

const PORT = Number(process.env.PORT || 10000);
const UPSTREAM = 'https://my.hapychrust.ru';
const PUBLIC_HOST = 'hapych-mobile-access.onrender.com';
const PUBLIC_BASE = 'https://' + PUBLIC_HOST;
const BUILD = 'mobile-access-20261021-beta1';
const TIMEOUT_MS = 14000;
const MAX_POST_BYTES = 32768;
const MAX_DOWNLOAD_BYTES = 5 * 1024 * 1024;
const MAX_ACTIVE_REQUESTS = 90;

let activeRequests = 0;
const limits = new Map();

function sendText(res, code, body, extra = {}) {
  const buffer = Buffer.from(body, 'utf8');
  res.writeHead(code, {
    'Content-Type': 'text/plain; charset=utf-8',
    'Content-Length': String(buffer.length),
    'Cache-Control': 'private, no-store',
    'X-Content-Type-Options': 'nosniff',
    'Referrer-Policy': 'no-referrer',
    'X-Robots-Tag': 'noindex, nofollow',
    ...extra
  });
  res.end(buffer);
}

function publicIp(req) {
  // Used for abuse throttling only, not a trust or authentication decision.
  const header = String(req.headers['x-forwarded-for'] || '').slice(0, 512);
  const candidates = header.split(',').map(v => v.trim()).filter(Boolean);
  for (let i = candidates.length - 1; i >= 0; i--) {
    if (net.isIP(candidates[i])) return candidates[i];
  }
  return req.socket.remoteAddress || 'anonymous';
}

function underLimit(key, max, windowMs) {
  const now = Date.now();
  const record = limits.get(key);
  const used = (record && record.until > now) ? record.count : 0;
  if (used >= max) return false;
  limits.set(key, { count: used + 1, until: used ? record.until : now + windowMs });
  if (limits.size > 6500) {
    for (const [k, entry] of limits) if (entry.until <= now) limits.delete(k);
  }
  return true;
}

function allowed(method, path) {
  if (method === 'GET' || method === 'HEAD') {
    if ([
      '/', '/help', '/privacy', '/terms', '/login', '/register',
      '/dashboard', '/connect', '/account', '/checkout',
      '/my/orders', '/forgot-password'
    ].includes(path)) return true;

    if (/^\/static\/[a-zA-Z0-9_./-]+\.(css|js|mjs|png|jpg|jpeg|webp|gif|svg|ico|woff|woff2|ttf)$/.test(path) &&
        !path.includes('..') && !path.includes('//')) return true;
    if (/^\/my\/qr\/[a-zA-Z0-9_-]{1,24}$/.test(path)) return true;
    if (/^\/client\/orders\/[a-zA-Z0-9_-]{1,90}$/.test(path)) return true;
    return false;
  }
  if (method === 'POST') {
    return ['/login', '/register', '/logout'].includes(path);
  }
  return false;
}

function sessionCookie(requestCookie) {
  const raw = String(requestCookie || '');
  if (raw.length > 10000) return '';
  for (const part of raw.split(';')) {
    const cookie = part.trim();
    if (/^hapych_customer_session=[a-zA-Z0-9._=-]{1,6000}$/.test(cookie)) return cookie;
  }
  return '';
}

function cookieFromOrigin(cookie) {
  if (typeof cookie !== 'string') return null;
  if (!/^hapych_customer_session=/i.test(cookie)) return null;
  // Host-only cookies on this onrender.com hostname; no cross-domain cookies.
  return cookie.split(';').filter(p => !/^\s*domain\s*=/i.test(p)).join(';');
}

function locationFromOrigin(raw) {
  if (!raw) return null;
  if (raw.startsWith('/') && !raw.startsWith('//')) return raw;
  try {
    const u = new URL(raw);
    if (u.origin !== UPSTREAM || u.username || u.password) return null;
    return PUBLIC_BASE + u.pathname + u.search + u.hash;
  } catch { return null; }
}

function policy() {
  return {
    'Content-Security-Policy':
      "default-src 'self'; script-src 'self'; style-src 'self'; " +
      "img-src 'self' data:; font-src 'self'; connect-src 'self'; " +
      "form-action 'self'; base-uri 'self'; frame-ancestors 'none'",
    'X-Frame-Options': 'DENY',
    'X-Content-Type-Options': 'nosniff',
    'Referrer-Policy': 'no-referrer',
    'X-Robots-Tag': 'noindex, nofollow',
    'Cache-Control': 'private, no-store',
    'Permissions-Policy': 'camera=(), microphone=(), geolocation=()',
    'X-Hapych-Mobile': BUILD
  };
}

function betaBanner() {
  return '<aside id="hapych-mobile-gateway-status" role="status" style="' +
    'position:relative;z-index:2147483647;padding:12px 15px;' +
    'background:#38260f;color:#fff3e1;border-bottom:2px solid #ff982d;' +
    'font:700 13px/1.5 system-ui, sans-serif;text-align:center">' +
    'HAPYCH — мобильный вход (тест). Авторизация и просмотр подписки доступны. ' +
    'Оплата и изменение аккаунта временно отключены.' +
    '</aside>';
}

async function fetchOrigin(req, res, target, body) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), TIMEOUT_MS);
  activeRequests++;
  try {
    const headers = {
      'Accept': String(req.headers.accept || 'text/html,application/xhtml+xml,*/*').slice(0, 420),
      'Accept-Language': String(req.headers['accept-language'] || 'ru-RU,ru;q=0.9').slice(0, 160),
      'User-Agent': 'Mozilla/5.0 (compatible; HAPYCH-Mobile-Gateway/1.0)',
      'Cache-Control': 'no-store'
    };
    const cookie = sessionCookie(req.headers.cookie);
    if (cookie) headers.Cookie = cookie;
    if (body) {
      headers['Content-Type'] = 'application/x-www-form-urlencoded';
      headers['Content-Length'] = String(body.length);
      headers.Origin = UPSTREAM;
      headers.Referer = UPSTREAM + target.pathname;
    }

    const upstream = await fetch(UPSTREAM + target.pathname + target.search, {
      method: req.method === 'HEAD' ? 'HEAD' : req.method,
      headers,
      body: body && body.length ? body : undefined,
      signal: controller.signal,
      redirect: 'manual',
      cache: 'no-store'
    });

    let status = upstream.status;
    const responseHeaders = policy();
    const cookieValues = upstream.headers.getSetCookie();
    const cleaned = cookieValues.map(cookieFromOrigin).filter(Boolean);
    if (cleaned.length) responseHeaders['Set-Cookie'] = cleaned;

    const loc = upstream.headers.get('location');
    if (loc && status >= 300 && status < 400) {
      const safe = locationFromOrigin(loc);
      if (!safe) {
        sendText(res, 502, 'Перенаправление за пределы личного кабинета отключено.');
        return;
      }
      responseHeaders.Location = safe;
    } else if (status >= 300 && status < 400) {
      sendText(res, 502, 'Некорректное перенаправление исходного сайта.');
      return;
    }

    let contentType = upstream.headers.get('content-type') || 'application/octet-stream';
    if (contentType.length > 240) contentType = 'application/octet-stream';
    responseHeaders['Content-Type'] = contentType;

    const declared = Number(upstream.headers.get('content-length') || 0);
    if (declared > MAX_DOWNLOAD_BYTES) {
      sendText(res, 502, 'Размер ответа источника превышает лимит мобильного шлюза.');
      return;
    }

    const chunks = [];
    let size = 0;
    if (req.method !== 'HEAD' && upstream.body) {
      for await (const part of upstream.body) {
        size += part.length;
        if (size > MAX_DOWNLOAD_BYTES) {
          controller.abort();
          sendText(res, 502, 'Ответ источника превышает лимит мобильного шлюза.');
          return;
        }
        chunks.push(Buffer.from(part));
      }
    }

    let payload = Buffer.concat(chunks);
    if (contentType.includes('text/html') && status >= 200 && status < 300) {
      let html = payload.toString('utf8');
      if (!/<body(?:\s|>)/i.test(html)) {
        sendText(res, 502, 'Источник вернул неполную страницу.');
        return;
      }
      html = html.replace(/<body([^>]*)>/i, (_, attrs) => '<body' + attrs + '>' + betaBanner());
      html = html.replaceAll(UPSTREAM, PUBLIC_BASE);
      payload = Buffer.from(html, 'utf8');
    }

    responseHeaders['Content-Length'] = String(payload.length);
    res.writeHead(status, responseHeaders);
    res.end(req.method === 'HEAD' ? undefined : payload);
  } catch (err) {
    if (!res.headersSent) {
      sendText(res, 502, err && err.name === 'AbortError'
        ? 'Основной HAPYCH не ответил в срок. Попробуйте ещё раз.'
        : 'Ошибка подключения шлюза к основному HAPYCH.');
    } else {
      res.destroy();
    }
  } finally {
    clearTimeout(timer);
    activeRequests--;
  }
}

const server = http.createServer(async (req, res) => {
  if (req.url === '/healthz') {
    const payload = JSON.stringify({ ok: true, service: 'hapych-mobile-access', build: BUILD });
    const buf = Buffer.from(payload);
    res.writeHead(200, {
      'Content-Type': 'application/json; charset=utf-8',
      'Content-Length': String(buf.length),
      'Cache-Control': 'no-store'
    });
    res.end(buf);
    return;
  }

  if (req.headers.host !== PUBLIC_HOST) {
    sendText(res, 421, 'Некорректный адрес шлюза.');
    return;
  }

  if (activeRequests >= MAX_ACTIVE_REQUESTS) {
    sendText(res, 503, 'Мобильный шлюз перегружен. Повторите запрос позднее.');
    return;
  }

  let target;
  try {
    if (!req.url || req.url.length > 2300 || req.url.startsWith('//')) throw new Error('path');
    target = new URL(req.url, PUBLIC_BASE);
    if (target.origin !== PUBLIC_BASE) throw new Error('domain');
  } catch {
    sendText(res, 400, 'Некорректный путь запроса.');
    return;
  }

  if (!allowed(req.method, target.pathname)) {
    sendText(res, req.method === 'POST' ? 405 : 404,
      'Эта операция временно недоступна на мобильном входе. ' +
      'Основной личный кабинет продолжает работать по своему адресу.');
    return;
  }
  if (target.search.length > 1400) {
    sendText(res, 400, 'Слишком длинный запрос.');
    return;
  }

  const ip = publicIp(req);
  if (!underLimit('get:' + ip, 240, 60 * 1000)) {
    sendText(res, 429, 'Слишком много запросов. Попробуйте через минуту.');
    return;
  }
  if (req.method === 'POST' &&
      !underLimit('auth:' + target.pathname + ':' + ip, target.pathname === '/login' ? 5 : 3,
        15 * 60 * 1000)) {
    sendText(res, 429, 'Превышен лимит попыток. Подождите 15 минут.');
    return;
  }

  let body;
  if (req.method === 'POST') {
    const contentType = String(req.headers['content-type'] || '').toLowerCase();
    const length = Number(req.headers['content-length'] || 0);
    if (!contentType.startsWith('application/x-www-form-urlencoded') ||
        length > MAX_POST_BYTES || !Number.isFinite(length)) {
      sendText(res, 415, 'Поддерживается только стандартная форма личного кабинета.');
      return;
    }

    const parts = [];
    let total = 0;
    try {
      for await (const part of req) {
        total += part.length;
        if (total > MAX_POST_BYTES) throw new Error('too large');
        parts.push(Buffer.from(part));
      }
      body = Buffer.concat(parts);
    } catch {
      sendText(res, 413, 'Слишком большой запрос.');
      return;
    }
  }

  await fetchOrigin(req, res, target, body);
});

server.headersTimeout = 16000;
server.requestTimeout = 18000;
server.listen(PORT, '0.0.0.0', () => {
  console.log('HAPYCH mobile gateway started (' + BUILD + ')');
});
