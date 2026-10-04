const http = require("http");
const crypto = require("crypto");
const WebSocket = require("ws");

const PORT = Number(process.env.PORT || 10000);
const RCON_HOST = process.env.RCON_HOST || "";
const RCON_PORT = Number(process.env.RCON_PORT || 0);
const RCON_PASSWORD = process.env.RCON_PASSWORD || "";

if (!RCON_HOST || !RCON_PORT || !RCON_PASSWORD) {
  console.error("Missing RCON_HOST, RCON_PORT or RCON_PASSWORD");
  process.exit(1);
}

let lastProbe = {
  ok: false,
  state: "starting",
  checkedAt: null,
  latencyMs: null,
  error: null
};

function safeEqual(a, b) {
  const aa = Buffer.from(String(a));
  const bb = Buffer.from(String(b));
  if (aa.length !== bb.length) return false;
  return crypto.timingSafeEqual(aa, bb);
}

function upstreamUrl() {
  return `ws://${RCON_HOST}:${RCON_PORT}/${encodeURIComponent(RCON_PASSWORD)}`;
}

function newUpstream() {
  return new WebSocket(upstreamUrl(), {
    perMessageDeflate: false,
    handshakeTimeout: 8000,
    headers: {
      "User-Agent": "HAPYCH-RCON-Relay/1.0"
    }
  });
}

function sendJson(ws, obj) {
  if (ws.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify(obj));
  }
}

function probeRcon() {
  const started = Date.now();
  const ws = newUpstream();
  let finished = false;
  const finish = (ok, state, error = null) => {
    if (finished) return;
    finished = true;
    lastProbe = {
      ok,
      state,
      checkedAt: new Date().toISOString(),
      latencyMs: Date.now() - started,
      error
    };
    try { ws.close(); } catch {}
  };

  const timer = setTimeout(() => {
    finish(false, "timeout", "WebRCON did not answer within 10s");
    try { ws.terminate(); } catch {}
  }, 10000);

  ws.on("open", () => {
    const id = 970001;
    ws.send(JSON.stringify({
      Identifier: id,
      Message: "serverinfo",
      Name: "HAPYCH Relay",
      Type: 3
    }));
  });

  ws.on("message", data => {
    const text = data.toString();
    try {
      const msg = JSON.parse(text);
      if (msg && (msg.Identifier === 970001 || String(msg.Message || "").includes("Hostname"))) {
        clearTimeout(timer);
        finish(true, "ready", null);
      }
    } catch {
      // Rust can emit plain console text. An open authenticated socket is already meaningful.
    }
  });

  ws.on("close", (code, reason) => {
    clearTimeout(timer);
    if (!finished) {
      finish(false, "closed", `upstream closed code=${code} reason=${reason.toString().slice(0,120)}`);
    }
  });

  ws.on("error", err => {
    clearTimeout(timer);
    finish(false, "error", err && err.message ? err.message : "upstream error");
  });
}

const server = http.createServer((req, res) => {
  if (req.url === "/health") {
    res.writeHead(lastProbe.ok ? 200 : 503, {"content-type":"application/json; charset=utf-8"});
    res.end(JSON.stringify({
      service: "HAPYCH RCON Relay",
      rconHost: RCON_HOST,
      rconPort: RCON_PORT,
      ...lastProbe
    }));
    return;
  }
  res.writeHead(200, {"content-type":"application/json; charset=utf-8"});
  res.end(JSON.stringify({
    service: "HAPYCH RCON Relay",
    status: "online",
    endpoint: "/ws"
  }));
});

const wss = new WebSocket.Server({
  noServer: true,
  perMessageDeflate: false
});

server.on("upgrade", (req, socket, head) => {
  const url = new URL(req.url, "http://localhost");
  if (url.pathname !== "/ws") {
    socket.write("HTTP/1.1 404 Not Found\r\nConnection: close\r\n\r\n");
    socket.destroy();
    return;
  }

  const auth = req.headers.authorization || "";
  const supplied = auth.startsWith("Bearer ") ? auth.slice(7) : "";
  if (!safeEqual(supplied, RCON_PASSWORD)) {
    socket.write("HTTP/1.1 401 Unauthorized\r\nConnection: close\r\n\r\n");
    socket.destroy();
    return;
  }

  wss.handleUpgrade(req, socket, head, client => {
    wss.emit("connection", client, req);
  });
});

wss.on("connection", client => {
  const upstream = newUpstream();
  const queue = [];
  let upstreamOpened = false;
  let terminated = false;

  const closeBoth = () => {
    if (terminated) return;
    terminated = true;
    try { if (client.readyState <= 1) client.close(); } catch {}
    try { if (upstream.readyState <= 1) upstream.close(); } catch {}
  };

  const connectTimer = setTimeout(() => {
    sendJson(client, {Type:"relay_error", Message:"RCON connection timeout"});
    closeBoth();
  }, 10000);

  client.on("message", data => {
    if (upstreamOpened && upstream.readyState === WebSocket.OPEN) {
      upstream.send(data);
    } else {
      if (queue.length < 100) queue.push(Buffer.from(data));
    }
  });

  client.on("close", closeBoth);
  client.on("error", closeBoth);

  upstream.on("open", () => {
    clearTimeout(connectTimer);
    upstreamOpened = true;
    sendJson(client, {
      Type: "relay_status",
      Message: "RCON connected",
      Host: RCON_HOST,
      Port: RCON_PORT
    });
    for (const item of queue.splice(0)) {
      if (upstream.readyState === WebSocket.OPEN) upstream.send(item);
    }
  });

  upstream.on("message", data => {
    if (client.readyState === WebSocket.OPEN) client.send(data);
  });

  upstream.on("close", (code, reason) => {
    clearTimeout(connectTimer);
    if (!upstreamOpened) {
      sendJson(client, {
        Type: "relay_error",
        Message: "RCON rejected connection or password",
        Code: code
      });
    } else {
      sendJson(client, {
        Type: "relay_error",
        Message: "RCON connection closed",
        Code: code
      });
    }
    closeBoth();
  });

  upstream.on("error", err => {
    clearTimeout(connectTimer);
    sendJson(client, {
      Type: "relay_error",
      Message: err && err.message ? err.message : "RCON upstream error"
    });
    closeBoth();
  });
});

server.listen(PORT, "0.0.0.0", () => {
  console.log(`HAPYCH RCON Relay listening on :${PORT}; upstream ${RCON_HOST}:${RCON_PORT}`);
  probeRcon();
  setInterval(probeRcon, 30000).unref();
});
