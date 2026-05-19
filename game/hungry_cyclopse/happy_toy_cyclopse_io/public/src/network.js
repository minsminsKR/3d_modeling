export class NetworkClient {
  constructor() {
    this.id = null;
    this.snapshot = null;
    this.connected = false;
    this.listeners = new Map();
    const protocol = location.protocol === "https:" ? "wss" : "ws";
    this.socket = new WebSocket(`${protocol}://${location.host}/ws`);
    this.socket.addEventListener("open", () => {
      this.connected = true;
      this.emit("open");
    });
    this.socket.addEventListener("close", () => {
      this.connected = false;
      this.emit("close");
    });
    this.socket.addEventListener("message", (event) => this.#handle(JSON.parse(event.data)));
  }

  on(type, callback) {
    if (!this.listeners.has(type)) this.listeners.set(type, []);
    this.listeners.get(type).push(callback);
  }

  emit(type, payload) {
    for (const callback of this.listeners.get(type) || []) callback(payload);
  }

  send(type, payload = {}) {
    if (!this.connected) return;
    this.socket.send(JSON.stringify({ type, ...payload }));
  }

  #handle(message) {
    if (message.type === "welcome") {
      this.id = message.id;
      this.emit("welcome", message);
    } else if (message.type === "snapshot") {
      this.snapshot = message;
      this.emit("snapshot", message);
    } else if (message.type === "event") {
      this.emit(message.event, message);
    }
  }
}
