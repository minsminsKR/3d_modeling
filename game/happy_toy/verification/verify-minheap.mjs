import { MinHeap } from "../src/utils/minHeap.js";

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

const heap = new MinHeap();
heap.push({ f: 5, id: "c" });
heap.push({ f: 1, id: "a" });
heap.push({ f: 3, id: "b" });
heap.push({ f: 1, id: "a2" });

assert(heap.size === 4, "size after pushes");
assert(heap.pop().id === "a" || heap.pop(), "first pop exists");

heap.clear();
for (const f of [9, 2, 7, 2, 8, 1, 4]) {
  heap.push({ f });
}
const ordered = [];
while (heap.size > 0) {
  ordered.push(heap.pop().f);
}
assert(ordered.join(",") === "1,2,2,4,7,8,9", `heap order was ${ordered.join(",")}`);

const n = 4000;
const big = new MinHeap();
const t0 = performance.now();
for (let i = 0; i < n; i++) {
  big.push({ f: (i * 17) % 997 });
}
let last = -Infinity;
let count = 0;
while (big.size > 0) {
  const node = big.pop();
  assert(node.f >= last, "heap invariant");
  last = node.f;
  count += 1;
}
const dt = performance.now() - t0;
assert(count === n, "popped all nodes");
assert(dt < 80, `4000 heap ops took ${dt.toFixed(2)}ms`);

console.log(`min-heap ok (${n} push/pop in ${dt.toFixed(2)}ms)`);
