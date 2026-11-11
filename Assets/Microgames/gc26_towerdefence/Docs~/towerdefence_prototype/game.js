"use strict";
// Last Bastion: browser prototype. The numbers come from TowerDefense_GDD_EN_RU.md
// (sections 3, 5, 6 and 7) so the rules can be tried before the Unity version.
// Distances are in tiles, speeds in tiles per second.

const CONFIG = {
  cols: 16,
  rows: 9,
  startGold: 250,
  startLives: 20,
  buildCountdown: 20,        // seconds; starting the wave early pays the seconds left as gold
  sellRefund: 0.7,
  upgradeCost: [0.75, 1.0],  // share of the base price for level 2 and level 3
  upgradeDamage: 1.4,
  upgradeRange: 1.1,
  hpPerWave: 0.08,           // enemy HP x (1 + 0.08 x (wave - 1))
  projectileSpeed: 9,
  shamanRadius: 1.5,
  teslaChainRange: 1.6,
};

const TOWERS = {
  archer: { name: "Archer", cost: 100, damage: 10, rate: 1.0,  range: 3.5, air: true,  color: "#3f8f3a", letter: "A", kind: "projectile" },
  cannon: { name: "Cannon", cost: 150, damage: 25, rate: 0.5,  range: 3.0, air: false, color: "#6b5b4b", letter: "C", kind: "projectile", splash: 1.0 },
  frost:  { name: "Frost",  cost: 125, damage: 4,  rate: 1.0,  range: 3.0, air: true,  color: "#3d8fd6", letter: "F", kind: "projectile", slow: 0.4, slowTime: 2 },
  tesla:  { name: "Tesla",  cost: 200, damage: 15, rate: 0.8,  range: 3.0, air: true,  color: "#e0b400", letter: "T", kind: "chain", chain: 3 },
  sniper: { name: "Sniper", cost: 250, damage: 60, rate: 0.33, range: 7.0, air: true,  color: "#8a3fb5", letter: "S", kind: "hitscan", pierceArmor: true },
};
const TOWER_ORDER = ["archer", "cannon", "frost", "tesla", "sniper"];

const ENEMIES = {
  grunt:   { hp: 50,   speed: 1.0, gold: 5,   lives: 1,  color: "#7cb342", size: 0.26 },
  runner:  { hp: 30,   speed: 2.0, gold: 6,   lives: 1,  color: "#fdd835", size: 0.2 },
  brute:   { hp: 250,  speed: 0.6, gold: 15,  lives: 2,  color: "#8d6e63", size: 0.36, armor: 0.3 },
  bat:     { hp: 40,   speed: 1.4, gold: 8,   lives: 1,  color: "#5e35b1", size: 0.24, flying: true },
  shaman:  { hp: 80,   speed: 0.9, gold: 12,  lives: 1,  color: "#26a69a", size: 0.26, heal: 5 },
  warlord: { hp: 2000, speed: 0.5, gold: 100, lives: 10, color: "#c62828", size: 0.46, shield: 300 },
};

const TARGET_MODES = ["first", "last", "strongest", "closest"];
const MODE_LABEL = { first: "First", last: "Last", strongest: "Strong", closest: "Close" };

// Ground path (tile centers). Bats fly straight from the first point to the last.
const PATH = [[-0.5, 1.5], [3.5, 1.5], [3.5, 6.5], [7.5, 6.5], [7.5, 2.5], [11.5, 2.5], [11.5, 6.5], [15, 6.5]];
const SLOTS = [[1.5, 3], [2, 5.5], [5.5, 3], [5.5, 5], [5.5, 8], [9.5, 1], [9.5, 4.5], [9.5, 7.8], [13.5, 1.5], [13.5, 4.5], [13.5, 8]];

// A group spawns `count` enemies, one every `every` seconds, starting `start` seconds into the wave.
const g = (type, count, every, start = 0, hpMul = 1) => ({ type, count, every, start, hpMul });
const WAVES = [
  [g("grunt", 8, 1.0)],
  [g("grunt", 12, 0.8)],
  [g("grunt", 8, 0.9), g("runner", 6, 0.6, 8)],
  [g("runner", 10, 0.5), g("grunt", 6, 0.9, 3)],
  [g("grunt", 10, 0.8), g("brute", 3, 2.5, 4)],
  [g("grunt", 8, 0.8), g("bat", 8, 0.8, 3)],
  [g("grunt", 8, 0.7), g("brute", 4, 2, 2), g("shaman", 3, 2.5, 3)],
  [g("runner", 10, 0.5), g("bat", 12, 0.6, 2)],
  [g("runner", 10, 0.5), g("brute", 6, 1.6, 2), g("shaman", 4, 2, 4)],
  // Boss wave. The GDD gives weaker bosses before World 3, so this one has about 1/3 of the full HP.
  [g("grunt", 10, 0.7), g("brute", 4, 2, 3), g("warlord", 1, 1, 10, 0.35)],
];

// ---------- path geometry ----------
function makeLine(points) {
  const segs = [];
  let total = 0;
  for (let i = 0; i < points.length - 1; i++) {
    const [x1, y1] = points[i], [x2, y2] = points[i + 1];
    const len = Math.hypot(x2 - x1, y2 - y1);
    segs.push({ x1, y1, x2, y2, len, start: total });
    total += len;
  }
  return {
    total,
    at(d) {
      for (const s of segs) {
        if (d <= s.start + s.len) {
          const k = Math.max(0, d - s.start) / s.len;
          return { x: s.x1 + (s.x2 - s.x1) * k, y: s.y1 + (s.y2 - s.y1) * k };
        }
      }
      const s = segs[segs.length - 1];
      return { x: s.x2, y: s.y2 };
    },
  };
}
const GROUND = makeLine(PATH);
const AIR = makeLine([PATH[0], PATH[PATH.length - 1]]);

const dist2 = (a, b) => (a.x - b.x) ** 2 + (a.y - b.y) ** 2;

// ---------- state ----------
const $ = (id) => document.getElementById(id);
const canvas = $("board");
const ctx = canvas.getContext("2d");
let tile = 40;
let dpr = 1;
let vertical = false;        // true: 9x16 field for a phone held upright
let ox = 0, oy = 0;          // pixel offset of the field inside the screen
let viewW = 0, viewH = 0;    // screen size in CSS pixels
let S;

function newGame() {
  const params = new URLSearchParams(location.search);
  S = {
    gold: Number(params.get("gold")) || CONFIG.startGold,  // ?gold=5000 for testing
    lives: CONFIG.startLives,
    wave: 0,                 // number of the wave that is running or last ran (1-based)
    phase: "build",          // build | wave | won | lost
    countdown: CONFIG.buildCountdown,
    time: 0,
    waveTime: 0,
    schedule: [],
    enemies: [],
    towers: new Array(SLOTS.length).fill(null),
    projectiles: [],
    effects: [],
    texts: [],
    speed: 1,
    paused: false,
    built: false,
    selected: -1,
    preview: null,
  };
  closeMenu();
  $("overlay").hidden = true;
  $("paused").hidden = true;
}

// ---------- waves ----------
function startWave() {
  if (S.phase !== "build" || S.wave >= WAVES.length) return;
  const bonus = Math.ceil(S.countdown);
  if (bonus > 0) {
    S.gold += bonus;
    floatText(14.5, 5.2, `Early +${bonus}`, "#ffd54f");
  }
  S.wave++;
  S.schedule = [];
  for (const grp of WAVES[S.wave - 1]) {
    for (let i = 0; i < grp.count; i++) {
      S.schedule.push({ time: grp.start + i * grp.every, type: grp.type, hpMul: grp.hpMul });
    }
  }
  S.schedule.sort((a, b) => a.time - b.time);
  S.waveTime = 0;
  S.phase = "wave";
}

function spawn(type, hpMul) {
  const def = ENEMIES[type];
  const hp = def.hp * (1 + CONFIG.hpPerWave * (S.wave - 1)) * hpMul;
  const line = def.flying ? AIR : GROUND;
  const p = line.at(0);
  S.enemies.push({
    def, type, hp, maxHp: hp,
    shield: def.shield || 0, maxShield: def.shield || 0,
    line, dist: 0, x: p.x, y: p.y,
    slowUntil: 0, slowAmount: 0, dead: false,
  });
}

function waveCleared() {
  const bonus = 20 + 5 * S.wave;
  S.gold += bonus;
  floatText(8, 4.5, `Wave ${S.wave} cleared +${bonus}`, "#ffd54f", 1.6);
  if (S.wave >= WAVES.length) return endGame(true);
  S.phase = "build";
  S.countdown = CONFIG.buildCountdown;
}

function endGame(won) {
  S.phase = won ? "won" : "lost";
  closeMenu();
  const stars = S.lives >= 18 ? 3 : S.lives >= 10 ? 2 : 1;
  $("resultTitle").textContent = won ? "Victory!" : "Defeat";
  $("stars").textContent = won ? "★".repeat(stars) + "☆".repeat(3 - stars) : "";
  $("resultText").textContent = won
    ? `Lives left: ${S.lives}/${CONFIG.startLives}`
    : `You reached wave ${S.wave} of ${WAVES.length}`;
  $("overlay").hidden = false;
}

// ---------- towers ----------
function stats(t, extraLevels = 0) {
  const lvl = t.level + extraLevels;
  return {
    damage: t.def.damage * CONFIG.upgradeDamage ** lvl,
    range: t.def.range * CONFIG.upgradeRange ** lvl,
    rate: t.def.rate,
  };
}
const upgradeCost = (t) => (t.level < 2 ? Math.round(t.def.cost * CONFIG.upgradeCost[t.level]) : null);
const refund = (t) => Math.floor(t.spent * CONFIG.sellRefund);

function build(slot, type) {
  const def = TOWERS[type];
  if (S.towers[slot] || S.gold < def.cost) return;
  S.gold -= def.cost;
  const [x, y] = SLOTS[slot];
  S.towers[slot] = { type, def, slot, x, y, level: 0, cooldown: 0, spent: def.cost, mode: "first" };
  S.built = true;
}

function upgrade(t) {
  const cost = upgradeCost(t);
  if (cost === null || S.gold < cost) return;
  S.gold -= cost;
  t.spent += cost;
  t.level++;
}

function sell(t) {
  S.gold += refund(t);
  floatText(t.x, t.y - 0.4, `+${refund(t)}`, "#ffd54f");
  S.towers[t.slot] = null;
}

function cycleMode(t) {
  t.mode = TARGET_MODES[(TARGET_MODES.indexOf(t.mode) + 1) % TARGET_MODES.length];
}

function pickTarget(t, st) {
  let best = null, bestScore = -Infinity;
  const r2 = st.range ** 2;
  for (const e of S.enemies) {
    if (e.dead || (e.def.flying && !t.def.air)) continue;
    const d2 = dist2(e, t);
    if (d2 > r2) continue;
    const progress = e.dist / e.line.total;
    const score = t.mode === "first" ? progress
      : t.mode === "last" ? -progress
      : t.mode === "strongest" ? e.hp + e.shield
      : -d2;
    if (score > bestScore) { bestScore = score; best = e; }
  }
  return best;
}

function fire(t, st, target) {
  const d = t.def;
  if (d.kind === "projectile") {
    S.projectiles.push({
      x: t.x, y: t.y, target, tx: target.x, ty: target.y,
      damage: st.damage, splash: d.splash || 0, slow: d.slow || 0, slowTime: d.slowTime || 0, color: d.color,
    });
  } else if (d.kind === "hitscan") {
    S.effects.push({ kind: "line", pts: [[t.x, t.y], [target.x, target.y]], color: "#e1bee7", until: S.time + 0.1 });
    hurt(target, st.damage, d.pierceArmor);
  } else if (d.kind === "chain") {
    const hit = [target];
    let last = target;
    while (hit.length < d.chain) {
      let next = null, nd = CONFIG.teslaChainRange ** 2;
      for (const e of S.enemies) {
        if (e.dead || hit.includes(e)) continue;
        const dd = dist2(e, last);
        if (dd <= nd) { nd = dd; next = e; }
      }
      if (!next) break;
      hit.push(next);
      last = next;
    }
    S.effects.push({ kind: "line", pts: [[t.x, t.y], ...hit.map((e) => [e.x, e.y])], color: "#fff176", until: S.time + 0.12 });
    for (const e of hit) hurt(e, st.damage, false);
  }
}

function impact(p) {
  if (p.splash) {
    S.effects.push({ kind: "ring", x: p.tx, y: p.ty, r: p.splash, color: "#ffab40", until: S.time + 0.2 });
    for (const e of S.enemies) {
      if (!e.dead && !e.def.flying && dist2(e, { x: p.tx, y: p.ty }) <= p.splash ** 2) hurt(e, p.damage, false);
    }
  } else if (!p.target.dead) {
    hurt(p.target, p.damage, false);
    if (p.slow) {
      p.target.slowUntil = S.time + p.slowTime;
      p.target.slowAmount = p.slow;
    }
  }
}

function hurt(e, amount, pierceArmor) {
  if (e.dead) return;
  if (e.shield > 0) {
    const absorbed = Math.min(e.shield, amount);
    e.shield -= absorbed;
    amount -= absorbed;
  }
  if (!pierceArmor && e.def.armor) amount *= 1 - e.def.armor;
  e.hp -= amount;
  if (e.hp <= 0) {
    e.dead = true;
    S.gold += e.def.gold;
    floatText(e.x, e.y - 0.3, `+${e.def.gold}`, "#ffd54f");
  }
}

function floatText(x, y, text, color, life = 0.9) {
  S.texts.push({ x, y, text, color, born: S.time, until: S.time + life });
}

// ---------- simulation ----------
function update(dt) {
  S.time += dt;

  if (S.phase === "build") S.countdown = Math.max(0, S.countdown - dt);
  if (S.phase === "wave") {
    S.waveTime += dt;
    while (S.schedule.length && S.schedule[0].time <= S.waveTime) {
      const s = S.schedule.shift();
      spawn(s.type, s.hpMul);
    }
  }

  // Shamans heal everyone around them except themselves.
  for (const sh of S.enemies) {
    if (!sh.def.heal || sh.dead) continue;
    for (const e of S.enemies) {
      if (e !== sh && !e.dead && dist2(e, sh) <= CONFIG.shamanRadius ** 2) {
        e.hp = Math.min(e.maxHp, e.hp + sh.def.heal * dt);
      }
    }
  }

  for (const e of S.enemies) {
    if (e.dead) continue;
    const slow = S.time < e.slowUntil ? 1 - e.slowAmount : 1;
    e.dist += e.def.speed * slow * dt;
    if (e.dist >= e.line.total) {
      e.dead = true;
      S.lives = Math.max(0, S.lives - e.def.lives);
      floatText(14.6, 5.4, `-${e.def.lives} ❤`, "#ff5252");
      continue;
    }
    const p = e.line.at(e.dist);
    e.x = p.x;
    e.y = p.y;
  }

  for (const t of S.towers) {
    if (!t) continue;
    t.cooldown = Math.max(0, t.cooldown - dt);
    if (t.cooldown > 0) continue;
    const st = stats(t);
    const target = pickTarget(t, st);
    if (!target) continue;
    t.cooldown = 1 / st.rate;
    fire(t, st, target);
  }

  for (const p of S.projectiles) {
    if (!p.target.dead) { p.tx = p.target.x; p.ty = p.target.y; }
    const dx = p.tx - p.x, dy = p.ty - p.y;
    const d = Math.hypot(dx, dy), step = CONFIG.projectileSpeed * dt;
    if (d <= step) { p.done = true; impact(p); }
    else { p.x += (dx / d) * step; p.y += (dy / d) * step; }
  }

  S.projectiles = S.projectiles.filter((p) => !p.done);
  S.enemies = S.enemies.filter((e) => !e.dead);
  S.effects = S.effects.filter((f) => f.until > S.time);
  S.texts = S.texts.filter((f) => f.until > S.time);

  if (S.lives <= 0) return endGame(false);
  if (S.phase === "wave" && !S.schedule.length && !S.enemies.length) waveCleared();
}

// ---------- drawing ----------
// The simulation always uses the horizontal 16x9 map. In vertical mode the map is
// turned 90° clockwise onto a 9x16 field, so the road comes in at the top and the
// castle is at the bottom. Distances stay the same, so the balance is identical.
function toScreen(x, y) {
  return vertical
    ? [ox + (CONFIG.rows - y) * tile, oy + x * tile]
    : [ox + x * tile, oy + y * tile];
}

function toMap(px, py) {
  return vertical
    ? [(py - oy) / tile, CONFIG.rows - (px - ox) / tile]
    : [(px - ox) / tile, (py - oy) / tile];
}

function strokeLine(points) {
  ctx.beginPath();
  points.forEach(([x, y], i) => {
    const [px, py] = toScreen(x, y);
    if (i) ctx.lineTo(px, py);
    else ctx.moveTo(px, py);
  });
  ctx.stroke();
}

function circle(x, y, r) {
  const [px, py] = toScreen(x, y);
  ctx.beginPath();
  ctx.arc(px, py, r * tile, 0, Math.PI * 2);
}

function square(x, y, half, radius) {
  const [px, py] = toScreen(x, y);
  ctx.beginPath();
  ctx.roundRect(px - half * tile, py - half * tile, 2 * half * tile, 2 * half * tile, radius * tile);
}

function draw() {
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.fillStyle = "#86b86a";
  ctx.fillRect(0, 0, viewW, viewH);

  // The field is centered; screens longer than 16:9 get extra grass at the ends.
  ctx.strokeStyle = "rgba(0,0,0,0.05)";
  ctx.lineWidth = 1;
  ctx.beginPath();
  for (let px = ox % tile; px < viewW; px += tile) { ctx.moveTo(px, 0); ctx.lineTo(px, viewH); }
  for (let py = oy % tile; py < viewH; py += tile) { ctx.moveTo(0, py); ctx.lineTo(viewW, py); }
  ctx.stroke();

  // The road starts at the edge of the screen, so enemies walk in from off-screen.
  const margin = (vertical ? oy : ox) / tile;
  const road = [[-margin - 1, PATH[0][1]], ...PATH];
  ctx.lineCap = "round";
  ctx.lineJoin = "round";
  ctx.strokeStyle = "#b89a5e";
  ctx.lineWidth = tile * 0.9;
  strokeLine(road);
  ctx.strokeStyle = "#dcc48e";
  ctx.lineWidth = tile * 0.72;
  strokeLine(road);

  drawCastle();

  SLOTS.forEach(([x, y], i) => {
    if (S.towers[i]) return;
    ctx.fillStyle = i === S.selected ? "#cfcfcf" : "#a4a4a4";
    ctx.strokeStyle = i === S.selected ? "#fff" : "#6f6f6f";
    ctx.lineWidth = 2;
    square(x, y, 0.38, 0.12);
    ctx.fill();
    ctx.stroke();
  });

  // Range circle for the selected tower, or for the tower or upgrade the player is pointing at.
  const sel = S.selected >= 0 ? S.towers[S.selected] : null;
  const ranges = [];
  if (sel) ranges.push({ x: sel.x, y: sel.y, r: stats(sel).range });
  if (S.preview) ranges.push(S.preview);
  for (const r of ranges) {
    circle(r.x, r.y, r.r);
    ctx.fillStyle = "rgba(255,255,255,0.12)";
    ctx.fill();
    ctx.strokeStyle = "rgba(255,255,255,0.65)";
    ctx.lineWidth = 2;
    ctx.stroke();
  }

  for (const t of S.towers) if (t) drawTower(t);
  for (const e of S.enemies) drawEnemy(e);

  for (const p of S.projectiles) {
    circle(p.x, p.y, p.splash ? 0.12 : 0.07);
    ctx.fillStyle = p.splash ? "#333" : p.color;
    ctx.fill();
  }

  for (const f of S.effects) {
    ctx.strokeStyle = f.color;
    ctx.lineWidth = 3;
    if (f.kind === "line") strokeLine(f.pts);
    else { circle(f.x, f.y, f.r); ctx.stroke(); }
  }

  // Floating texts always rise up the screen, in both modes.
  ctx.textAlign = "center";
  ctx.font = `bold ${Math.round(tile * 0.32)}px system-ui, sans-serif`;
  for (const f of S.texts) {
    const k = (S.time - f.born) / (f.until - f.born);
    const [px, py] = toScreen(f.x, f.y);
    const rise = py - k * 0.5 * tile;
    ctx.globalAlpha = 1 - k;
    ctx.fillStyle = "#000";
    ctx.fillText(f.text, px + 1, rise + 1);
    ctx.fillStyle = f.color;
    ctx.fillText(f.text, px, rise);
  }
  ctx.globalAlpha = 1;
}

function drawCastle() {
  // Drawn upright at the end of the road, kept inside the field.
  const [cx, cy] = toScreen(15.25, 6.5);
  const w = 1.3 * tile, h = (vertical ? 1.5 : 2) * tile;
  const x = cx - w / 2;
  const y = Math.min(cy - h / 2, oy + (vertical ? 16 : 9) * tile - h);
  ctx.fillStyle = "#9e9e9e";
  ctx.fillRect(x, y, w, h);
  ctx.fillStyle = "#7d7d7d";
  for (let i = 0; i < 3; i++) ctx.fillRect(x + i * w * 0.4, y - tile * 0.25, w * 0.2, tile * 0.25);
  ctx.fillStyle = "#5d4037";
  ctx.fillRect(x + w * 0.3, y + h * 0.55, w * 0.4, h * 0.45);
  ctx.fillStyle = "#e53935";
  ctx.fillRect(x + w * 0.5, y - tile * 0.8, tile * 0.3, tile * 0.2);
  ctx.fillStyle = "#555";
  ctx.fillRect(x + w * 0.5 - 1, y - tile * 0.8, 2, tile * 0.55);
}

function drawTower(t) {
  const { x, y, def } = t;
  const [px, py] = toScreen(x, y);
  ctx.fillStyle = "#777";
  square(x, y, 0.4, 0.14);
  ctx.fill();
  circle(x, y, 0.32);
  ctx.fillStyle = def.color;
  ctx.fill();
  ctx.strokeStyle = t.slot === S.selected ? "#fff" : "rgba(0,0,0,0.5)";
  ctx.lineWidth = 2;
  ctx.stroke();
  ctx.fillStyle = "#fff";
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.font = `bold ${Math.round(tile * 0.34)}px system-ui, sans-serif`;
  ctx.fillText(def.letter, px, py + 1);
  ctx.textBaseline = "alphabetic";
  ctx.fillStyle = "#ffd54f";
  for (let i = 0; i <= t.level; i++) {
    ctx.beginPath();
    ctx.arc(px + (i - 1) * 0.18 * tile, py + 0.3 * tile, 0.06 * tile, 0, Math.PI * 2);
    ctx.fill();
  }
}

function drawEnemy(e) {
  const { x, y, def } = e;
  const [px, py] = toScreen(x, y);
  const r = def.size * tile;
  ctx.fillStyle = def.color;
  ctx.beginPath();
  if (def.flying) {
    ctx.moveTo(px - r * 1.4, py - r * 0.4);
    ctx.lineTo(px, py + r * 0.6);
    ctx.lineTo(px + r * 1.4, py - r * 0.4);
    ctx.lineTo(px, py - r * 0.1);
    ctx.closePath();
  } else {
    ctx.arc(px, py, r, 0, Math.PI * 2);
  }
  ctx.fill();
  ctx.lineWidth = S.time < e.slowUntil ? 3 : 1.5;
  ctx.strokeStyle = S.time < e.slowUntil ? "#90caf9" : "rgba(0,0,0,0.6)";
  ctx.stroke();
  if (def.heal) {
    circle(x, y, CONFIG.shamanRadius);
    ctx.strokeStyle = "rgba(38,166,154,0.35)";
    ctx.lineWidth = 1;
    ctx.stroke();
  }

  const bw = Math.max(0.5 * tile, r * 2), bx = px - bw / 2, by = py - r - 0.18 * tile;
  ctx.fillStyle = "#222";
  ctx.fillRect(bx, by, bw, 4);
  const k = Math.max(0, e.hp / e.maxHp);
  ctx.fillStyle = k > 0.5 ? "#66bb6a" : k > 0.25 ? "#ffca28" : "#ef5350";
  ctx.fillRect(bx, by, bw * k, 4);
  if (e.maxShield) {
    ctx.fillStyle = "#222";
    ctx.fillRect(bx, by - 5, bw, 3);
    ctx.fillStyle = "#42a5f5";
    ctx.fillRect(bx, by - 5, bw * (e.shield / e.maxShield), 3);
  }
}

// ---------- HUD and menus ----------
let hudCache = "";
function refreshHud() {
  const bonus = S.phase === "build" ? Math.ceil(S.countdown) : 0;
  const key = [S.gold, S.lives, S.wave, S.phase, bonus, S.speed, S.built].join("|");
  if (key !== hudCache) {
    hudCache = key;
    $("gold").textContent = `💰 ${S.gold}`;
    $("lives").textContent = `❤️ ${S.lives}`;
    $("wave").textContent = `Wave ${S.wave}/${WAVES.length}`;
    $("speedBtn").textContent = `${S.speed}×`;
    const btn = $("startBtn");
    btn.disabled = S.phase !== "build";
    btn.firstChild.textContent = S.phase === "build" ? `Start wave ${S.wave + 1}` : "Wave in progress";
    $("bonus").textContent = bonus > 0 ? `early bonus +${bonus}` : "";
    $("hint").hidden = S.built;
  }
  for (const b of $("menu").children) b.disabled = !b._can();
}

function openMenu(slot) {
  S.selected = slot;
  S.preview = null;
  const menu = $("menu");
  menu.innerHTML = "";
  const t = S.towers[slot];
  const [sx, sy] = SLOTS[slot];
  const items = [];
  if (!t) {
    for (const key of TOWER_ORDER) {
      const d = TOWERS[key];
      items.push({
        label: d.name, sub: `${d.cost}g`, color: d.color,
        can: () => S.gold >= d.cost,
        act: () => build(slot, key),
        preview: { x: sx, y: sy, r: d.range },
      });
    }
  } else {
    const cost = upgradeCost(t);
    items.push({
      label: cost === null ? "Max" : "Upgrade", sub: cost === null ? "" : `${cost}g`, color: "#66bb6a",
      can: () => cost !== null && S.gold >= cost,
      act: () => upgrade(t),
      preview: cost === null ? null : { x: sx, y: sy, r: stats(t, 1).range },
    });
    items.push({ label: "Sell", sub: `+${refund(t)}g`, color: "#ef5350", can: () => true, act: () => sell(t) });
    items.push({ label: "Target", sub: MODE_LABEL[t.mode], color: "#90caf9", can: () => true, act: () => cycleMode(t), keepOpen: true });
  }

  // Keep the whole ring on screen.
  const R = Math.max(70, tile * 1.5);
  const [px, py] = toScreen(sx, sy);
  const cx = Math.min(Math.max(px, R + 34), viewW - R - 34);
  const cy = Math.min(Math.max(py, R + 34), viewH - R - 34);
  items.forEach((it, i) => {
    const a = -Math.PI / 2 + (i * 2 * Math.PI) / items.length;
    const b = document.createElement("button");
    b.className = "mbtn";
    b.style.left = `${cx + Math.cos(a) * R}px`;
    b.style.top = `${cy + Math.sin(a) * R}px`;
    b.style.setProperty("--c", it.color);
    b.innerHTML = `<span>${it.label}</span><small>${it.sub}</small>`;
    b._can = it.can;
    b.disabled = !it.can();
    b.addEventListener("pointerenter", () => { S.preview = it.preview || null; });
    b.addEventListener("pointerleave", () => { S.preview = null; });
    b.addEventListener("pointerdown", (ev) => ev.stopPropagation());
    b.addEventListener("click", (ev) => {
      ev.stopPropagation();
      if (!it.can()) return;
      it.act();
      if (it.keepOpen) openMenu(slot);
      else closeMenu();
    });
    menu.appendChild(b);
  });
}

function closeMenu() {
  if (S) { S.selected = -1; S.preview = null; }
  $("menu").innerHTML = "";
}

canvas.addEventListener("pointerdown", (ev) => {
  if (S.phase === "won" || S.phase === "lost" || menuOpen()) return;
  const rect = canvas.getBoundingClientRect();
  const [x, y] = toMap(ev.clientX - rect.left, ev.clientY - rect.top);
  let hit = -1;
  SLOTS.forEach(([sx, sy], i) => {
    if (Math.abs(x - sx) <= 0.55 && Math.abs(y - sy) <= 0.55) hit = i;
  });
  if (hit >= 0 && hit !== S.selected) openMenu(hit);
  else closeMenu();
});

function setPaused(p) {
  if (S.phase === "won" || S.phase === "lost" || menuOpen()) return;
  S.paused = p;
  $("paused").hidden = !p;
}

// ---------- main menu and How to play ----------
// The level stays frozen behind these screens.
const menuOpen = () => !$("mainMenu").hidden || !$("howTo").hidden;

function showMainMenu() {
  newGame();
  $("howTo").hidden = true;
  $("mainMenu").hidden = false;
}

function showHowTo(show) {
  $("howTo").hidden = !show;
  $("mainMenu").hidden = show;
}

function toggleLanguage() {
  const ru = document.querySelector('#howTo [lang="ru"]');
  const en = document.querySelector('#howTo [lang="en"]');
  ru.hidden = !ru.hidden;
  en.hidden = !ru.hidden;
  $("langBtn").textContent = ru.hidden ? "Русский" : "English";
}

$("playBtn").addEventListener("click", () => {
  newGame();
  $("mainMenu").hidden = true;
  // On a phone, hide the browser bars where the browser allows it.
  if (matchMedia("(pointer: coarse)").matches && document.documentElement.requestFullscreen) {
    document.documentElement.requestFullscreen().catch(() => {});
  }
});
$("howToBtn").addEventListener("click", () => showHowTo(true));
$("howToBackBtn").addEventListener("click", () => showHowTo(false));
$("langBtn").addEventListener("click", toggleLanguage);

$("startBtn").addEventListener("click", startWave);
$("speedBtn").addEventListener("click", () => { S.speed = S.speed === 1 ? 2 : 1; });
$("pauseBtn").addEventListener("click", () => setPaused(!S.paused));
$("resumeBtn").addEventListener("click", () => setPaused(false));
$("pauseRestartBtn").addEventListener("click", newGame);
$("pauseMenuBtn").addEventListener("click", showMainMenu);
$("restartBtn").addEventListener("click", newGame);
$("resultMenuBtn").addEventListener("click", showMainMenu);
document.addEventListener("keydown", (ev) => {
  if (!$("howTo").hidden) {
    if (ev.key === "Escape") showHowTo(false);
    return;
  }
  if (menuOpen()) return;
  if (ev.key === "Escape") setPaused(!S.paused);
  if (ev.key === " ") { ev.preventDefault(); startWave(); }
});
document.addEventListener("visibilitychange", () => { if (document.hidden) setPaused(true); });

// ---------- screen size ----------
// On a phone the game fills the whole screen: horizontal when the phone is held
// sideways, vertical when it is held upright. On a computer it is shown inside a
// phone frame; the bar above it picks the direction and the phone screen shape.
const ASPECTS = { "16:9": 16 / 9, "19.5:9": 19.5 / 9, "20:9": 20 / 9 };
const frameParam = new URLSearchParams(location.search).get("frame");  // ?frame=0 or ?frame=1
const framed = frameParam !== null ? frameParam === "1" : !matchMedia("(pointer: coarse)").matches;
let aspect = "16:9";
let frameVertical = false;
try {
  if (ASPECTS[localStorage.getItem("td-aspect")]) aspect = localStorage.getItem("td-aspect");
  frameVertical = localStorage.getItem("td-vertical") === "1";
} catch (e) { /* storage blocked */ }

function resize() {
  let w = innerWidth, h = innerHeight;
  if (framed) {
    vertical = frameVertical;
    const a = vertical ? 1 / ASPECTS[aspect] : ASPECTS[aspect];
    w = Math.min(innerWidth - 60, (innerHeight - 100) * a);
    h = w / a;
  } else {
    vertical = innerHeight > innerWidth;
  }
  viewW = Math.floor(w);
  viewH = Math.floor(h);
  const fieldW = vertical ? CONFIG.rows : CONFIG.cols;
  const fieldH = vertical ? CONFIG.cols : CONFIG.rows;
  tile = Math.min(viewW / fieldW, viewH / fieldH);
  ox = (viewW - fieldW * tile) / 2;
  oy = (viewH - fieldH * tile) / 2;
  $("game").classList.toggle("vertical", vertical);
  $("phone").classList.toggle("vertical", vertical);
  dpr = window.devicePixelRatio || 1;
  $("game").style.width = `${viewW}px`;
  $("game").style.height = `${viewH}px`;
  canvas.style.width = `${viewW}px`;
  canvas.style.height = `${viewH}px`;
  canvas.width = Math.round(viewW * dpr);
  canvas.height = Math.round(viewH * dpr);
  for (const b of document.querySelectorAll("#aspectBar [data-aspect]")) {
    b.classList.toggle("on", b.dataset.aspect === aspect);
    const [long, short] = b.dataset.aspect.split(":");
    b.textContent = vertical ? `${short}:${long}` : `${long}:${short}`;
  }
  for (const b of document.querySelectorAll("#aspectBar [data-vertical]")) {
    b.classList.toggle("on", (b.dataset.vertical === "1") === vertical);
  }
  if (S && S.selected >= 0) openMenu(S.selected);
}
window.addEventListener("resize", resize);

$("phone").classList.toggle("framed", framed);
$("aspectBar").hidden = !framed;
for (const b of document.querySelectorAll("#aspectBar [data-aspect]")) {
  b.addEventListener("click", () => {
    aspect = b.dataset.aspect;
    try { localStorage.setItem("td-aspect", aspect); } catch (e) { /* storage blocked */ }
    resize();
  });
}
for (const b of document.querySelectorAll("#aspectBar [data-vertical]")) {
  b.addEventListener("click", () => {
    frameVertical = b.dataset.vertical === "1";
    try { localStorage.setItem("td-vertical", b.dataset.vertical); } catch (e) { /* storage blocked */ }
    resize();
  });
}

let last = performance.now();
function frame(now) {
  const dt = Math.min(0.05, (now - last) / 1000);
  last = now;
  if (!S.paused && !menuOpen() && (S.phase === "build" || S.phase === "wave")) {
    for (let i = 0; i < S.speed && (S.phase === "build" || S.phase === "wave"); i++) update(dt);
  }
  draw();
  refreshHud();
  requestAnimationFrame(frame);
}

// Handle for testing from the browser console, e.g. game.state().gold = 9999
window.game = { state: () => S, startWave, build, upgrade, update, newGame };

showMainMenu();
resize();
requestAnimationFrame(frame);
