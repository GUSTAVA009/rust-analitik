const http = require("http");
const net = require("net");
const WebSocket = require("ws");

const PORT = Number(process.env.PORT || 10000);
const RCON_HOST = process.env.RCON_HOST || "";
const RCON_PORT = Number(process.env.RCON_PORT || 0);
const HEALTH_RCON_PASSWORD = process.env.RCON_PASSWORD || "";

if (!RCON_HOST || !RCON_PORT) {
  console.error("Missing RCON_HOST or RCON_PORT");
  process.exit(1);
}

const attempts = new Map();
const RATE_WINDOW_MS = 60_000;
const RATE_LIMIT = 12;

function clientIp(req) {
  const forwarded = String(req.headers["x-forwarded-for"] || "").split(",")[0].trim();
  return forwarded || req.socket.remoteAddress || "unknown";
}

function allowAttempt(ip) {
  const now = Date.now();
  const item = attempts.get(ip);
  if (!item || now - item.startedAt > RATE_WINDOW_MS) {
    attempts.set(ip, {startedAt: now, count: 1});
    return true;
  }
  item.count += 1;
  return item.count <= RATE_LIMIT;
}

function upstreamUrl(password) {
  return `ws://${RCON_HOST}:${RCON_PORT}/${encodeURIComponent(password)}`;
}

function newUpstream(password) {
  return new WebSocket(upstreamUrl(password), {
    perMessageDeflate: false,
    handshakeTimeout: 10_000,
    headers: {
      "User-Agent": "HAPYCH-RCON-Relay/1.1"
    }
  });
}

function sendJson(ws, obj) {
  if (ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(obj));
}

function tcpHealth(callback) {
  const started = Date.now();
  const socket = net.createConnection({host: RCON_HOST, port: RCON_PORT});
  let done = false;
  const finish = (ok, error = null) => {
    if (done) return;
    done = true;
    try { socket.destroy(); } catch {}
    callback({ ok, latencyMs: Date.now() - started, error });
  };
  socket.setTimeout(3500);
  socket.once("connect", () => finish(true));
  socket.once("timeout", () => finish(false, "timeout"));
  socket.once("error", err => finish(false, err && err.message ? err.message : "tcp error"));
}

function authenticatedHealth(callback) {
  if (!HEALTH_RCON_PASSWORD) {
    tcpHealth(result => callback({...result, auth: "not_configured"}));
    return;
  }

  const started = Date.now();
  const ws = newUpstream(HEALTH_RCON_PASSWORD);
  let done = false;
  let opened = false;
  let serverInfo = null;
  let playerList = null;
  let serverInfoKeys = [];
  let playerKeys = [];

  const finish = (ok, state, error = null) => {
    if (done) return;
    done = true;
    try { ws.terminate(); } catch {}
    callback({
      ok,
      state,
      auth: opened ? "accepted" : "failed",
      latencyMs: Date.now() - started,
      error,
      diagnostics: {
        serverInfo,
        serverInfoKeys,
        playerCount: Array.isArray(playerList) ? playerList.length : null,
        playerKeys
      }
    });
  };

  const timer = setTimeout(() => {
    finish(opened, opened ? "connected_no_telemetry" : "timeout", opened ? "Connected but serverinfo/playerlist telemetry incomplete" : "Rust WebRCON auth probe timed out");
  }, 10000);

  const sendCommand = (id, message) => {
    ws.send(JSON.stringify({
      Identifier: id,
      Message: message,
      Name: "HAPYCH Relay Health",
      Type: 3
    }));
  };

  ws.once("open", () => {
    opened = true;
    sendCommand(970001, "serverinfo");
    sendCommand(970002, "playerlist");
  });

  ws.on("message", data => {
    const raw = data.toString();
    let outer;
    try { outer = JSON.parse(raw); } catch { return; }
    const id = Number(outer && outer.Identifier);
    const message = String(outer && outer.Message || "");

    if (id === 970001 || message.includes("Hostname") || message.includes("MaxPlayers")) {
      try {
        const o = JSON.parse(message);
        serverInfoKeys = Object.keys(o || {});
        const pick = (names) => {
          for (const name of names) {
            if (o && o[name] !== undefined && o[name] !== null) return o[name];
          }
          return null;
        };
        serverInfo = {
          hostname: pick(["Hostname","hostname"]),
          players: pick(["Players","players"]),
          maxPlayers: pick(["MaxPlayers","maxPlayers","Maxplayers"]),
          queued: pick(["Queued","queued"]),
          joining: pick(["Joining","joining"]),
          fps: pick(["Framerate","FPS","Fps","framerate"]),
          memory: pick(["Memory","MemoryMb","memory"]),
          entities: pick(["EntityCount","Entities","entityCount"]),
          uptime: pick(["Uptime","uptime"]),
          map: pick(["Map","map"]),
          gameTime: pick(["GameTime","gametime"])
        };
      } catch {}
    }

    if (id === 970002 || (message.startsWith("[") && (message.includes("SteamID") || message.includes("DisplayName")))) {
      try {
        const arr = JSON.parse(message);
        if (Array.isArray(arr)) {
          playerList = arr;
          playerKeys = arr.length && arr[0] && typeof arr[0] === "object" ? Object.keys(arr[0]) : [];
        }
      } catch {}
    }

    if (serverInfo && Array.isArray(playerList)) {
      clearTimeout(timer);
      finish(true, "ready", null);
    }
  });

  ws.once("close", (code, reason) => {
    clearTimeout(timer);
    finish(false, "closed", `code=${code} reason=${reason.toString().slice(0,120)}`);
  });

  ws.once("error", err => {
    clearTimeout(timer);
    finish(false, "error", err && err.message ? err.message : "WebRCON error");
  });
}

function relayPathHealth(callback) {
  if (!HEALTH_RCON_PASSWORD) {
    callback({ok:false, state:"not_configured", error:"RCON_PASSWORD not configured"});
    return;
  }

  const started = Date.now();
  const local = new WebSocket(`ws://127.0.0.1:${PORT}/ws`, {
    perMessageDeflate: false,
    handshakeTimeout: 5000,
    headers: { Authorization: `Bearer ${HEALTH_RCON_PASSWORD}` }
  });
  let done = false;
  const finish = (ok, state, error = null, serverInfo = null) => {
    if (done) return;
    done = true;
    try { local.terminate(); } catch {}
    callback({ok, state, latencyMs:Date.now()-started, error, serverInfo});
  };
  const timer = setTimeout(() => finish(false, "timeout", "relay path did not return serverinfo"), 12000);

  local.on("open", () => {
    local.send(JSON.stringify({
      Identifier: 980001,
      Message: "serverinfo",
      Name: "HAPYCH Relay Selftest",
      Type: 3
    }));
  });

  local.on("message", data => {
    let outer;
    try { outer = JSON.parse(data.toString()); } catch { return; }
    if (outer && outer.Type === "relay_error") {
      clearTimeout(timer);
      finish(false, "relay_error", String(outer.Message || "relay error"));
      return;
    }
    const message = String(outer && outer.Message || "");
    if (Number(outer && outer.Identifier) === 980001 || message.includes("Hostname")) {
      try {
        const info = JSON.parse(message);
        clearTimeout(timer);
        finish(true, "ready", null, {
          Hostname: info.Hostname ?? null,
          Players: info.Players ?? null,
          MaxPlayers: info.MaxPlayers ?? null,
          Framerate: info.Framerate ?? null,
          Memory: info.Memory ?? null,
          EntityCount: info.EntityCount ?? null,
          Uptime: info.Uptime ?? null,
          Map: info.Map ?? null
        });
      } catch {}
    }
  });

  local.on("error", err => {
    clearTimeout(timer);
    finish(false, "error", err && err.message ? err.message : "relay selftest error");
  });
}

const server = http.createServer((req, res) => {
  if (req.url === "/relay-health") {
    relayPathHealth(result => {
      res.writeHead(result.ok ? 200 : 503, {"content-type":"application/json; charset=utf-8"});
      res.end(JSON.stringify({
        service:"HAPYCH RCON Relay",
        path:"phone-relay-rust",
        ...result
      }));
    });
    return;
  }

  if (req.url === "/health") {
    authenticatedHealth(result => {
      res.writeHead(result.ok ? 200 : 503, {"content-type":"application/json; charset=utf-8"});
      res.end(JSON.stringify({
        service: "HAPYCH RCON Relay",
        status: result.ok ? "ready" : "upstream_unreachable",
        auth: result.auth || "unknown",
        state: result.state || null,
        rconHost: RCON_HOST,
        rconPort: RCON_PORT,
        latencyMs: result.latencyMs,
        error: result.error,
        diagnostics: result.diagnostics || null
      }));
    });
    return;
  }

  res.writeHead(200, {"content-type":"application/json; charset=utf-8"});
  res.end(JSON.stringify({
    service: "HAPYCH RCON Relay",
    status: "online",
    endpoint: "/ws",
    auth: "Authorization: Bearer <RCON password>"
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

  const ip = clientIp(req);
  if (!allowAttempt(ip)) {
    socket.write("HTTP/1.1 429 Too Many Requests\r\nConnection: close\r\nRetry-After: 60\r\n\r\n");
    socket.destroy();
    return;
  }

  const auth = String(req.headers.authorization || "");
  const suppliedPassword = auth.startsWith("Bearer ") ? auth.slice(7) : "";
  if (!suppliedPassword) {
    socket.write("HTTP/1.1 401 Unauthorized\r\nConnection: close\r\n\r\n");
    socket.destroy();
    return;
  }

  wss.handleUpgrade(req, socket, head, client => {
    client._hapychPassword = suppliedPassword;
    wss.emit("connection", client, req);
  });
});

wss.on("connection", client => {
  const password = client._hapychPassword;
  delete client._hapychPassword;

  const upstream = newUpstream(password);
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
    sendJson(client, {Type:"relay_error", Message:"Rust WebRCON connection timeout"});
    closeBoth();
  }, 12_000);

  client.on("message", (data, isBinary) => {
    // Rust WebRCON expects JSON as a TEXT WebSocket frame.
    // node-ws exposes text payloads as Buffer, so forwarding Buffer directly
    // silently turns the frame into binary. Normalize every app command to text.
    const text = isBinary ? data.toString("utf8") : data.toString();
    let outbound = text;
    try {
      const msg = JSON.parse(text);
      if (msg && typeof msg === "object") {
        if (!Number.isFinite(Number(msg.Identifier))) msg.Identifier = Date.now();
        if (typeof msg.Message !== "string") msg.Message = String(msg.Message || "");
        if (!msg.Name) msg.Name = "HAPYCH Admin";
        msg.Type = 3;
        outbound = JSON.stringify(msg);
        const commandName = String(msg.Message || "").trim().split(/\s+/)[0] || "<empty>";
        console.log(`client command -> Rust: ${commandName}`);
      }
    } catch {
      console.warn("client sent non-JSON WebRCON payload; forwarding as text");
    }

    if (upstreamOpened && upstream.readyState === WebSocket.OPEN) {
      upstream.send(outbound, { binary: false });
    } else if (queue.length < 100) {
      queue.push(outbound);
    }
  });

  client.on("close", closeBoth);
  client.on("error", closeBoth);

  upstream.on("open", () => {
    clearTimeout(connectTimer);
    upstreamOpened = true;
    sendJson(client, {
      Type: "relay_status",
      Message: "Rust WebRCON connected",
      Host: RCON_HOST,
      Port: RCON_PORT
    });
    for (const item of queue.splice(0)) {
      if (upstream.readyState === WebSocket.OPEN) upstream.send(item, { binary: false });
    }
  });

  upstream.on("message", (data, isBinary) => {
    if (client.readyState === WebSocket.OPEN) {
      if (isBinary) client.send(data, { binary: true });
      else client.send(data.toString(), { binary: false });
    }
  });

  upstream.on("close", (code, reason) => {
    clearTimeout(connectTimer);
    sendJson(client, {
      Type: "relay_error",
      Message: upstreamOpened ? "Rust WebRCON connection closed" : "Rust WebRCON rejected connection or password",
      Code: code,
      Reason: reason.toString().slice(0,120)
    });
    closeBoth();
  });

  upstream.on("error", err => {
    clearTimeout(connectTimer);
    sendJson(client, {
      Type: "relay_error",
      Message: err && err.message ? err.message : "Rust WebRCON upstream error"
    });
    closeBoth();
  });
});

server.listen(PORT, "0.0.0.0", () => {
  console.log(`HAPYCH RCON Relay listening on :${PORT}; upstream ${RCON_HOST}:${RCON_PORT}`);
});
