import crypto from "node:crypto";

const WS_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

export class WebSocketPeer {
  constructor(socket) {
    this.socket = socket;
    this.buffer = Buffer.alloc(0);
    this.alive = true;
    this.onMessage = null;
    this.onClose = null;

    socket.on("data", (chunk) => this.#read(chunk));
    socket.on("close", () => this.#close());
    socket.on("error", () => this.#close());
  }

  sendJson(payload) {
    if (!this.alive) return;
    this.#sendFrame(JSON.stringify(payload));
  }

  close() {
    if (!this.alive) return;
    this.alive = false;
    try {
      this.socket.end();
    } catch {
      // Socket may already be gone.
    }
  }

  #read(chunk) {
    this.buffer = Buffer.concat([this.buffer, chunk]);
    while (this.buffer.length >= 2) {
      const first = this.buffer[0];
      const second = this.buffer[1];
      const opcode = first & 0x0f;
      const masked = Boolean(second & 0x80);
      let length = second & 0x7f;
      let offset = 2;

      if (length === 126) {
        if (this.buffer.length < offset + 2) return;
        length = this.buffer.readUInt16BE(offset);
        offset += 2;
      } else if (length === 127) {
        this.close();
        return;
      }

      if (!masked) {
        this.close();
        return;
      }
      if (this.buffer.length < offset + 4 + length) return;

      const mask = this.buffer.subarray(offset, offset + 4);
      offset += 4;
      const payload = this.buffer.subarray(offset, offset + length);
      this.buffer = this.buffer.subarray(offset + length);

      for (let i = 0; i < payload.length; i += 1) {
        payload[i] ^= mask[i % 4];
      }

      if (opcode === 0x8) {
        this.close();
        return;
      }
      if (opcode === 0x9) {
        this.#sendFrame(payload, 0xA);
        continue;
      }
      if (opcode !== 0x1) continue;

      try {
        this.onMessage?.(JSON.parse(payload.toString("utf8")));
      } catch {
        // Ignore malformed client packets.
      }
    }
  }

  #sendFrame(data, opcode = 0x1) {
    const payload = Buffer.isBuffer(data) ? data : Buffer.from(data);
    let header;
    if (payload.length < 126) {
      header = Buffer.from([0x80 | opcode, payload.length]);
    } else if (payload.length < 65536) {
      header = Buffer.alloc(4);
      header[0] = 0x80 | opcode;
      header[1] = 126;
      header.writeUInt16BE(payload.length, 2);
    } else {
      return;
    }
    this.socket.write(Buffer.concat([header, payload]));
  }

  #close() {
    if (!this.alive) return;
    this.alive = false;
    this.onClose?.();
  }
}

export function acceptWebSocket(req, socket) {
  const key = req.headers["sec-websocket-key"];
  if (!key) {
    socket.destroy();
    return null;
  }

  const accept = crypto.createHash("sha1").update(`${key}${WS_GUID}`).digest("base64");
  socket.write([
    "HTTP/1.1 101 Switching Protocols",
    "Upgrade: websocket",
    "Connection: Upgrade",
    `Sec-WebSocket-Accept: ${accept}`,
    "",
    ""
  ].join("\r\n"));
  return new WebSocketPeer(socket);
}
