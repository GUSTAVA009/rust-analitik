'use strict';

const http = require('http');
const https = require('https');
const crypto = require('crypto');
const { URL } = require('url');

const PORT = Number(process.env.PORT || 10000);
const BOT_TOKEN = process.env.TELEGRAM_BOT_TOKEN || '';
const RELAY_KEY = process.env.HAPYCH_RELAY_KEY || '';
const MAX_BODY = 25 * 1024 * 1024;

const ALLOWED = new Set([
  'getMe',
  'getWebhookInfo',
  'getMyCommands',
  'setMyCommands',
  'getUpdates',
  'sendMessage',
  'sendPhoto',
  'sendMediaGroup',
  'getChat',
  'getChatMember'
]);

function sendJson(res, status, body) {
  const data = Buffer.from(JSON.stringify(body));
  res.writeHead(status, {
    'content-type': 'application/json; charset=utf-8',
    'content-length': data.length,
    'cache-control': 'no-store'
  });
  res.end(data);
}

function safeEqual(a, b) {
  const aa = Buffer.from(String(a || ''));
  const bb = Buffer.from(String(b || ''));
  return aa.length === bb.length && aa.length > 0 && crypto.timingSafeEqual(aa, bb);
}

function readBody(req, done) {
  const chunks = [];
  let total = 0;
  let finished = false;

  function finish(err, body) {
    if (finished) return;
    finished = true;
    done(err, body);
  }

  req.on('data', chunk => {
    total += chunk.length;
    if (total > MAX_BODY) {
      finish(new Error('body_too_large'));
      req.destroy();
      return;
    }
    chunks.push(chunk);
  });

  req.on('end', () => finish(null, Buffer.concat(chunks)));
  req.on('error', err => finish(err));
}

http.createServer((req, res) => {
  const url = new URL(req.url, 'http://relay.local');

  if (req.method === 'GET' && url.pathname === '/health') {
    return sendJson(res, 200, {
      ok: true,
      service: 'hapych-telegram-relay'
    });
  }

  const match = url.pathname.match(/^\/telegram\/([A-Za-z][A-Za-z0-9]+)$/);
  if (!match) return sendJson(res, 404, { ok: false, error: 'not_found' });

  if (!BOT_TOKEN || !RELAY_KEY) {
    return sendJson(res, 503, { ok: false, error: 'relay_not_configured' });
  }

  if (!safeEqual(req.headers['x-hapych-relay-key'], RELAY_KEY)) {
    return sendJson(res, 401, { ok: false, error: 'unauthorized' });
  }

  const method = match[1];
  if (!ALLOWED.has(method)) {
    return sendJson(res, 403, { ok: false, error: 'method_not_allowed' });
  }

  if (req.method !== 'GET' && req.method !== 'POST') {
    return sendJson(res, 405, { ok: false, error: 'method_not_allowed' });
  }

  readBody(req, (err, body) => {
    if (err) {
      return sendJson(res, err.message === 'body_too_large' ? 413 : 400, {
        ok: false,
        error: err.message === 'body_too_large' ? 'body_too_large' : 'body_read_failed'
      });
    }

    const headers = {
      'user-agent': 'HAPYCH-Render-Relay/1.0',
      'accept': 'application/json'
    };

    if (req.headers['content-type']) {
      headers['content-type'] = req.headers['content-type'];
    }
    if (body.length) {
      headers['content-length'] = body.length;
    }

    const upstream = https.request({
      hostname: 'api.telegram.org',
      port: 443,
      method: req.method,
      path: `/bot${BOT_TOKEN}/${method}${url.search || ''}`,
      headers,
      timeout: method === 'getUpdates' ? 45000 : 20000
    }, upstreamRes => {
      const responseHeaders = {
        'content-type': upstreamRes.headers['content-type'] || 'application/json; charset=utf-8',
        'cache-control': 'no-store'
      };

      if (upstreamRes.headers['retry-after']) {
        responseHeaders['retry-after'] = upstreamRes.headers['retry-after'];
      }

      res.writeHead(upstreamRes.statusCode || 502, responseHeaders);
      upstreamRes.pipe(res);
    });

    upstream.on('timeout', () => {
      upstream.destroy(new Error('upstream_timeout'));
    });

    upstream.on('error', () => {
      if (!res.headersSent) {
        sendJson(res, 502, { ok: false, error: 'telegram_upstream_failed' });
      } else {
        res.destroy();
      }
    });

    if (body.length) upstream.write(body);
    upstream.end();
  });
}).listen(PORT, '0.0.0.0', () => {
  console.log(`HAPYCH Telegram relay listening on ${PORT}`);
});
