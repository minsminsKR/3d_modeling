// A hidden tab has no animation frames. Wake simulation separately from drawing.
let timer = null;
self.onmessage = ({data: active}) => {
  clearInterval(timer);
  timer = active ? setInterval(() => self.postMessage(null), 50) : null;
};
