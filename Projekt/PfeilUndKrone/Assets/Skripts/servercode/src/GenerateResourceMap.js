const KING = { CASTLE: "Castle", MOAT: "Moat" };
const FILL = ["Wheat", "Wood", "Ore", "Desert"];

function mkRng(seed) {
  let s = seed | 0; if (s === 0) s = 0x9e3779b9;
  return () => { s ^= s<<13; s ^= s>>>17; s ^= s<<5; return (s>>>0)/0x100000000; };
}

function axialCoords(radius = 3) {
  const out = [];
  for (let q = -radius; q <= radius; q++) {
    const rMin = Math.max(-radius, -q - radius);
    const rMax = Math.min( radius, -q + radius);
    for (let r = rMin; r <= rMax; r++) out.push({ q, r });
  }
  return out; // 37 for radius 3
}

function neighbors({q,r}) {
  return [
    { q: q+1, r: r   },
    { q: q+1, r: r-1 },
    { q: q,   r: r-1 },
    { q: q-1, r: r   },
    { q: q-1, r: r+1 },
    { q: q,   r: r+1 },
  ];
}

function generateResourceMap(seed = Date.now(), radius = 3) {
  const rand = mkRng(seed);
  const coords = axialCoords(radius);

  // 1) Place castle at center
  const center = { q: 0, r: 0 };

  // 2) Moats ring around center
  const moatRing = neighbors(center);

  // Build a quick lookup for reserved tiles
  const key = (c) => `${c.q},${c.r}`;
  const reserved = new Map();
  reserved.set(key(center), KING.CASTLE);
  for (const m of moatRing) reserved.set(key(m), KING.MOAT);

  // 3) Fill remaining tiles randomly (deterministic)
  const pickFill = () => FILL[Math.floor(rand() * FILL.length)];

  const kingsMap = coords.map(c => {
    const k = key(c);
    const res = reserved.has(k) ? reserved.get(k) : pickFill();
    return { q: c.q, r: c.r, resource: res };
  });

  const banditMap = [...reserved.entries()].map(([k, res]) => {
    const [q, r] = k.split(',').map(Number);
    return { q, r, resource: res };
  });

  return { seed, banditMap, kingsMap };
}

module.exports = { generateResourceMap };