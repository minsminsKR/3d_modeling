// Binary min-heap keyed by node.f. Used by grid A* so each pop is O(log n)
// instead of sorting the whole open list every iteration.

export class MinHeap {
  constructor() {
    this.data = [];
  }

  get size() {
    return this.data.length;
  }

  clear() {
    this.data.length = 0;
  }

  push(node) {
    const data = this.data;
    data.push(node);
    this._siftUp(data.length - 1);
  }

  pop() {
    const data = this.data;
    if (data.length === 0) {
      return undefined;
    }
    const top = data[0];
    const last = data.pop();
    if (data.length > 0) {
      data[0] = last;
      this._siftDown(0);
    }
    return top;
  }

  _siftUp(index) {
    const data = this.data;
    while (index > 0) {
      const parent = (index - 1) >> 1;
      if (data[parent].f <= data[index].f) {
        break;
      }
      const tmp = data[parent];
      data[parent] = data[index];
      data[index] = tmp;
      index = parent;
    }
  }

  _siftDown(index) {
    const data = this.data;
    const n = data.length;
    while (true) {
      let smallest = index;
      const left = index * 2 + 1;
      const right = left + 1;
      if (left < n && data[left].f < data[smallest].f) {
        smallest = left;
      }
      if (right < n && data[right].f < data[smallest].f) {
        smallest = right;
      }
      if (smallest === index) {
        break;
      }
      const tmp = data[smallest];
      data[smallest] = data[index];
      data[index] = tmp;
      index = smallest;
    }
  }
}
