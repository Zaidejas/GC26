// Red Flags (Minesweeper) prototype.
// Same rules as Board.cs in 02_Technical_Design.md.
// Naming as in the docs: "mine" = hidden red flag, "flag" = player's yellow flag.

// ---------------------------------------------------------------- rules ----

class Board {
  constructor(width, height, mines) {
    this.width = width;
    this.height = height;
    // Keep at least 9 cells free for the safe 3x3 area around the first tap.
    this.mineCount = Math.max(1, Math.min(mines, width * height - 9));
    this.flagCount = 0;
    this.revealedCount = 0;
    this.minesPlaced = false;
    this.cells = [];
    for (let x = 0; x < width; x++) {
      const column = [];
      for (let y = 0; y < height; y++)
        column.push({ isMine: false, adjacent: 0, isRevealed: false, isFlagged: false });
      this.cells.push(column);
    }
  }

  get(x, y) { return this.cells[x][y]; }
  inBounds(x, y) { return x >= 0 && y >= 0 && x < this.width && y < this.height; }

  // Up to 8 neighbours (fewer at edges and corners).
  neighbours(x, y) {
    const result = [];
    for (let dx = -1; dx <= 1; dx++)
      for (let dy = -1; dy <= 1; dy++)
        if ((dx || dy) && this.inBounds(x + dx, y + dy)) result.push([x + dx, y + dy]);
    return result;
  }

  // Called on the first tap, so the first tap and its neighbours are always safe.
  placeMines(safeX, safeY) {
    let placed = 0;
    while (placed < this.mineCount) {
      const x = Math.floor(Math.random() * this.width);
      const y = Math.floor(Math.random() * this.height);
      const inSafeArea = Math.abs(x - safeX) <= 1 && Math.abs(y - safeY) <= 1;
      if (this.cells[x][y].isMine || inSafeArea) continue;
      this.cells[x][y].isMine = true;
      placed++;
    }
    for (let x = 0; x < this.width; x++)
      for (let y = 0; y < this.height; y++)
        this.cells[x][y].adjacent = this.neighbours(x, y).filter(([nx, ny]) => this.cells[nx][ny].isMine).length;
    this.minesPlaced = true;
  }

  // Opens a cell; a 0 opens its neighbours too (flood fill with a queue).
  // Returns the list of cells that were opened.
  reveal(x, y) {
    const changed = [];
    const start = this.cells[x][y];
    if (start.isRevealed || start.isFlagged) return changed;

    const queue = [[x, y]];
    while (queue.length) {
      const [px, py] = queue.shift();
      const c = this.cells[px][py];
      if (c.isRevealed || c.isFlagged) continue;
      c.isRevealed = true;
      changed.push([px, py]);
      if (c.isMine) break;                    // hit a red flag: the round is lost
      this.revealedCount++;
      if (c.adjacent === 0)
        for (const [nx, ny] of this.neighbours(px, py))
          if (!this.cells[nx][ny].isRevealed) queue.push([nx, ny]);
    }
    return changed;
  }

  // Places or removes a yellow flag. Returns true if something changed.
  toggleFlag(x, y) {
    const c = this.cells[x][y];
    if (c.isRevealed) return false;
    c.isFlagged = !c.isFlagged;
    this.flagCount += c.isFlagged ? 1 : -1;
    return true;
  }

  // Tapping an open number whose yellow flags match it opens the other neighbours.
  chord(x, y) {
    const c = this.cells[x][y];
    if (!c.isRevealed || c.adjacent === 0) return [];
    const near = this.neighbours(x, y);
    const flags = near.filter(([nx, ny]) => this.cells[nx][ny].isFlagged).length;
    if (flags !== c.adjacent) return [];
    return near.flatMap(([nx, ny]) => this.reveal(nx, ny));
  }

  get isWon() { return this.revealedCount === this.width * this.height - this.mineCount; }
}

// Loss lines: one per line in loss_lines.js; empty lines and "#" comments are skipped.
const LossLines = {
  parse(text) {
    return (text || "").split("\n").map(l => l.trim()).filter(l => l.length > 0 && !l.startsWith("#"));
  },
  pickRandom(lines) {
    return lines.length ? lines[Math.floor(Math.random() * lines.length)] : "";
  },
};

// ------------------------------------------------------------------- UI ----

const LEVELS = [
  { name: "Beginner", width: 9, height: 9, mines: 10 },
  { name: "Intermediate", width: 16, height: 16, mines: 40 },
  { name: "Expert", width: 16, height: 30, mines: 99 },
];
const LONG_PRESS_MS = 400;
const MOVE_CANCEL_PX = 10;

const $ = id => document.getElementById(id);
// The lines come from loss_lines.js (loaded before this file), so they also work when
// index.html is opened straight from disk, where browsers block reading .txt files.
const lossLines = LossLines.parse(window.LOSS_LINES_TEXT);
let level, board, state, flagMode, elapsed, lastTick, tiles;

// Settings and best times live in localStorage (PlayerPrefs in Unity).
function load(key) { try { return localStorage.getItem(key); } catch { return null; } }
function save(key, value) { try { localStorage.setItem(key, value); } catch { /* private mode */ } }

// ---- menu
// The menu (difficulty choice) is the first screen and the only way into a game.
function showMenu() {
  state = "menu";
  $("board").innerHTML = "";              // never show an old board when a new game starts
  $("panel").classList.add("hidden");
  $("game").classList.add("hidden");
  $("menu").classList.remove("hidden");
  const list = $("levels");
  list.innerHTML = "";
  for (const lv of LEVELS) {
    const best = load("best_" + lv.name);
    const btn = document.createElement("button");
    btn.className = "level";
    btn.innerHTML = `${lv.name}<small>${lv.width}×${lv.height} · ${lv.mines} red flags · Best: ${best ? best + " s" : "—"}</small>`;
    btn.onclick = () => { history.pushState({ screen: "game" }, ""); startGame(lv); };
    list.appendChild(btn);
  }
}

// ---- game
function startGame(lv) {
  level = lv;
  $("menu").classList.add("hidden");
  $("game").classList.remove("hidden");
  newRound();
}

function newRound() {
  board = new Board(level.width, level.height, level.mines);
  state = "ready";
  elapsed = 0;
  setFlagMode(false);
  $("panel").classList.add("hidden");
  buildBoard();
  updateHud();
}

function buildBoard() {
  const el = $("board");
  const wrap = $("board-wrap");
  // Fit the board to the free space; very tall boards scroll instead of shrinking too much.
  const w = (wrap.clientWidth - 16) / board.width;
  const h = (wrap.clientHeight - 16) / board.height;
  const cell = Math.max(20, Math.floor(Math.min(w, h, 44)));
  el.style.setProperty("--cell", cell + "px");
  el.style.gridTemplateColumns = `repeat(${board.width}, ${cell}px)`;
  el.innerHTML = "";
  tiles = [];
  // Row by row, so the grid fills left to right, top to bottom.
  for (let y = 0; y < board.height; y++)
    for (let x = 0; x < board.width; x++) {
      const t = document.createElement("div");
      t.className = "cell";
      t.dataset.x = x;
      t.dataset.y = y;
      el.appendChild(t);
      (tiles[x] ||= [])[y] = t;
    }
}

function drawCell(x, y, gameOver = false, hit = false) {
  const c = board.get(x, y);
  const t = tiles[x][y];
  t.className = "cell";
  t.innerHTML = "";
  if (!c.isRevealed) {
    if (gameOver && c.isMine && !c.isFlagged) { t.classList.add("open"); t.innerHTML = '<span class="flag"></span>'; }
    else if (c.isFlagged) {
      t.innerHTML = '<span class="flag yellow"></span>';
      if (gameOver && !c.isMine) t.classList.add("wrong");
    }
    return;
  }
  t.classList.add("open");
  if (c.isMine) { t.innerHTML = '<span class="flag"></span>'; if (hit) t.classList.add("hit"); }
  else if (c.adjacent > 0) { t.textContent = c.adjacent; t.classList.add("n" + c.adjacent); }
}

function drawAll(gameOver, hit) {
  for (let x = 0; x < board.width; x++)
    for (let y = 0; y < board.height; y++)
      drawCell(x, y, gameOver, hit && hit[0] === x && hit[1] === y);
}

const isOver = () => state === "won" || state === "lost";

function onTap(x, y) {
  if (isOver()) return;
  if (board.get(x, y).isRevealed) return afterOpen(board.chord(x, y));
  if (flagMode) return onFlag(x, y);
  if (board.get(x, y).isFlagged) return;
  if (!board.minesPlaced) { board.placeMines(x, y); state = "playing"; lastTick = performance.now(); }
  afterOpen(board.reveal(x, y));
}

function onFlag(x, y) {
  if (isOver() || !board.toggleFlag(x, y)) return;
  drawCell(x, y);
  updateHud();
}

function afterOpen(changed) {
  changed.forEach(([x, y]) => drawCell(x, y));
  const hit = changed.find(([x, y]) => board.get(x, y).isMine);
  if (hit) return lose(hit);
  if (board.isWon) win();
}

function lose(hit) {
  state = "lost";
  drawAll(true, hit);
  vibrate(200);
  updateHud();
  const line = LossLines.pickRandom(lossLines);
  showPanel("You hit a red flag!", line);
}

function win() {
  state = "won";
  // Cover the remaining red flags with yellow flags.
  for (let x = 0; x < board.width; x++)
    for (let y = 0; y < board.height; y++) {
      const c = board.get(x, y);
      if (c.isMine && !c.isFlagged) { c.isFlagged = true; board.flagCount++; }
    }
  drawAll(false);
  updateHud();
  const time = Math.floor(elapsed);
  const best = Number(load("best_" + level.name)) || Infinity;
  const record = time < best;
  if (record) save("best_" + level.name, time);
  showPanel("You won!", `Time: ${time} s${record ? " · New record!" : ""}`);
}

function showPanel(title, line) {
  $("panel-title").textContent = title;
  $("panel-line").textContent = line;
  $("panel-line").classList.toggle("hidden", !line);
  $("panel").classList.remove("hidden");
}

// ---- HUD
function digits(n) {
  if (n < 0) return "-" + String(Math.min(99, -n)).padStart(2, "0");
  return String(Math.min(999, n)).padStart(3, "0");
}

function updateHud(holding = false) {
  $("counter").textContent = digits(board.mineCount - board.flagCount);
  $("timer").textContent = digits(Math.floor(elapsed));
  $("btn-face").textContent = state === "won" ? "😎" : state === "lost" ? "😵" : holding ? "😮" : "🙂";
}

function setFlagMode(on) {
  flagMode = on;
  $("mode-reveal").classList.toggle("active", !on);
  $("mode-flag").classList.toggle("active", on);
}

function vibrate(ms) { if (navigator.vibrate) navigator.vibrate(ms); }

// Timer: only counts while playing and while the page is visible.
setInterval(() => {
  if (state !== "playing") return;
  const now = performance.now();
  if (!document.hidden) elapsed = Math.min(999, elapsed + (now - lastTick) / 1000);
  lastTick = now;
  updateHud(!!touch);
}, 200);
document.addEventListener("visibilitychange", () => { lastTick = performance.now(); });

// ---- touch / mouse: tap, long press, right click
let touch = null;   // { x, y, startX, startY, timer, longDone, tile }

function cancelTouch() {
  if (!touch) return;
  clearTimeout(touch.timer);
  touch.tile.classList.remove("pressed");
  touch = null;
  updateHud();
}

$("board").addEventListener("pointerdown", e => {
  const tile = e.target.closest(".cell");
  if (!tile || isOver()) return;
  const x = +tile.dataset.x, y = +tile.dataset.y;
  if (e.button === 2) { onFlag(x, y); return; }          // right click = flag (desktop)
  if (e.button !== 0) return;
  cancelTouch();
  touch = { x, y, startX: e.clientX, startY: e.clientY, longDone: false, tile };
  if (!board.get(x, y).isRevealed) tile.classList.add("pressed");
  updateHud(true);
  touch.timer = setTimeout(() => {                       // long press = flag
    if (!touch) return;
    touch.longDone = true;
    tile.classList.remove("pressed");
    if (!board.get(x, y).isRevealed) { onFlag(x, y); vibrate(30); }
  }, LONG_PRESS_MS);
});

window.addEventListener("pointermove", e => {
  if (touch && Math.hypot(e.clientX - touch.startX, e.clientY - touch.startY) > MOVE_CANCEL_PX) cancelTouch();
});

window.addEventListener("pointerup", () => {
  if (!touch) return;
  const { x, y, longDone } = touch;
  cancelTouch();
  if (!longDone) onTap(x, y);
});

window.addEventListener("pointercancel", cancelTouch);      // e.g. the page started scrolling
$("board").addEventListener("contextmenu", e => e.preventDefault());

// ---- buttons and Back
function backToMenu() { if (history.state && history.state.screen === "game") history.back(); else showMenu(); }

$("btn-face").onclick = newRound;
$("btn-again").onclick = newRound;
$("btn-menu").onclick = backToMenu;
$("btn-panel-menu").onclick = backToMenu;
$("mode-reveal").onclick = () => setFlagMode(false);
$("mode-flag").onclick = () => setFlagMode(true);
window.addEventListener("popstate", showMenu);               // Android Back button
window.addEventListener("keydown", e => { if (e.key === "Escape" && !$("game").classList.contains("hidden")) backToMenu(); });

showMenu();
