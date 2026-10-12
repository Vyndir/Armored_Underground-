import { GRID, roadCoord, HALF, ROAD } from './city.js';

const $ = id => document.getElementById(id);

export function fmtTime(t) {
  const m = Math.floor(t / 60);
  const s = t - m * 60;
  return `${m}:${s < 10 ? '0' : ''}${s.toFixed(2)}`;
}

// DOM heads-up display + heading-up minimap.
export class Hud {
  constructor() {
    this.el = $('hud');
    this.ap = $('ap-fill'); this.apNum = $('ap-num');
    this.en = $('en-fill'); this.enWrap = $('en-bar');
    this.speed = $('speed-num'); this.gear = $('speed-gauge');
    this.wpName = $('wp-name'); this.wpAmmo = $('wp-ammo');
    this.shName = $('sh-name'); this.shCd = $('sh-cd');
    this.raceBox = $('race-info'); this.pos = $('race-pos'); this.lap = $('race-lap'); this.timer = $('race-time');
    this.board = $('race-board');
    this.wallet = $('wallet');
    this.prompt = $('prompt');
    this.countdown = $('countdown');
    this.toastEl = $('toast');
    this.lockBox = $('lockbox');
    this.hitMarker = $('hitmarker');
    this.downEl = $('down');
    this.speedLines = $('speedlines');
    this.damageVignette = $('damage');
    this.map = $('minimap');
    this.mctx = this.map.getContext('2d');
    this.toastT = 0;
    this.hitT = 0;
    this.dmgT = 0;
  }

  show(v) { this.el.style.display = v ? 'block' : 'none'; }

  toast(text, color = '#00e5ff', dur = 2) {
    this.toastEl.textContent = text;
    this.toastEl.style.color = color;
    this.toastEl.style.opacity = 1;
    this.toastT = dur;
  }

  hit() { this.hitT = 0.15; }
  hurt() { this.dmgT = 0.4; }

  update(dt, { player, camera, camYaw, lock, race, save, prompt, markers }) {
    const st = player.stats;
    const apf = player.ap / st.ap;
    this.ap.style.width = `${apf * 100}%`;
    this.ap.style.background = apf < 0.3 ? '#ff3d3d' : '#e8edf5';
    this.apNum.textContent = Math.ceil(player.ap);
    this.en.style.width = `${(player.en / st.enCap) * 100}%`;
    this.enWrap.classList.toggle('overheat', player.overheated);

    const kmh = Math.round(player.speed * 3.6);
    this.speed.textContent = kmh;
    this.gear.style.setProperty('--p', Math.min(1, player.speed / st.boostSpeed));
    this.gear.classList.toggle('boost', player.boosting);

    const w = st.parts.weapon;
    this.wpName.textContent = w.name;
    this.wpAmmo.textContent = player.reloading > 0 ? 'RELOADING' : `${player.ammo} / ${w.mag}`;
    const sp = st.parts.shoulder;
    this.shName.textContent = sp.name;
    this.shCd.textContent = player.shoulderCd > 0 ? `${player.shoulderCd.toFixed(1)}s` : (sp.kind === 'mine' || lock ? 'READY' : 'NO LOCK');

    // speed lines when really moving
    this.speedLines.style.opacity = Math.max(0, (player.speed - 40) / 40) * (player.boosting ? 1 : 0.5);

    // Race panel
    if (race) {
      this.raceBox.style.display = 'block';
      this.timer.style.display = 'block';
      const stand = race.standings();
      const pos = stand.indexOf(race.player) + 1;
      this.pos.innerHTML = `${pos}<small>/${stand.length}</small>`;
      this.lap.textContent = race.ev.type === 'CIRCUIT' ? `LAP ${race.lap}/${race.ev.laps}` : `${race.ev.type} · GATE ${Math.min(race.player.next + 1, race.gates.length)}/${race.gates.length}`;
      this.timer.textContent = fmtTime(race.time);
      this.board.innerHTML = stand.map((r, i) => `<div class="${r === race.player ? 'me' : ''}${r.actor.disabled > 0 ? ' down' : ''}"><b>${i + 1}</b> ${r === race.player ? 'YOU' : r.actor.name}</div>`).join('');
      if (race.state === 'countdown') {
        const n = Math.ceil(race.countdown);
        this.countdown.textContent = n > 3 ? '' : n;
        this.countdown.style.display = 'block';
      } else if (race.time < 1) {
        this.countdown.textContent = 'GO';
      } else this.countdown.style.display = 'none';
    } else {
      this.raceBox.style.display = 'none';
      this.timer.style.display = 'none';
      this.countdown.style.display = 'none';
    }
    this.wallet.innerHTML = `CR <b>${save.credits.toLocaleString()}</b> &nbsp; REP <b>${save.rep}</b>`;

    this.prompt.style.display = prompt ? 'block' : 'none';
    if (prompt) this.prompt.innerHTML = prompt;

    // Toast
    if (this.toastT > 0) { this.toastT -= dt; if (this.toastT <= 0) this.toastEl.style.opacity = 0; }
    // Hit marker
    this.hitT -= dt; this.hitMarker.style.opacity = this.hitT > 0 ? 1 : 0;
    this.dmgT -= dt; this.damageVignette.style.opacity = Math.max(0, this.dmgT * 2) + (apf < 0.3 ? 0.35 : 0);
    this.downEl.style.display = player.disabled > 0 ? 'block' : 'none';

    // Lock box
    if (lock) {
      const p = lock.pos.clone().setY(lock.pos.y + 4).project(camera);
      if (p.z < 1) {
        this.lockBox.style.display = 'block';
        this.lockBox.style.left = `${(p.x * 0.5 + 0.5) * innerWidth}px`;
        this.lockBox.style.top = `${(-p.y * 0.5 + 0.5) * innerHeight}px`;
        this.lockBox.dataset.name = `${lock.name}  ${Math.round(lock.pos.distanceTo(player.pos))}m`;
        this.lockBox.querySelector('span').textContent = this.lockBox.dataset.name;
        this.lockBox.querySelector('i').style.width = `${(lock.ap / lock.stats.ap) * 100}%`;
      } else this.lockBox.style.display = 'none';
    } else this.lockBox.style.display = 'none';

    this.drawMap(player, camYaw, race, markers);
  }

  drawMap(player, yaw, race, markers) {
    const g = this.mctx;
    const W = this.map.width, H = this.map.height;
    const cx = W / 2, cy = H / 2;
    const scale = 0.32;
    const fx = Math.sin(yaw), fz = Math.cos(yaw);
    const rx = -Math.cos(yaw), rz = Math.sin(yaw);
    const tr = (x, z) => {
      const dx = x - player.pos.x, dz = z - player.pos.z;
      return [cx + (dx * rx + dz * rz) * scale, cy - (dx * fx + dz * fz) * scale];
    };
    g.clearRect(0, 0, W, H);
    g.save();
    g.beginPath(); g.arc(cx, cy, W / 2 - 2, 0, Math.PI * 2); g.clip();
    g.fillStyle = 'rgba(6,10,18,0.82)';
    g.fillRect(0, 0, W, H);
    // Roads
    g.strokeStyle = 'rgba(120,140,170,0.35)';
    g.lineWidth = ROAD * scale;
    g.lineCap = 'butt';
    for (let i = 0; i <= GRID; i++) {
      const c = roadCoord(i);
      g.beginPath(); g.moveTo(...tr(c, -HALF)); g.lineTo(...tr(c, HALF)); g.stroke();
      g.beginPath(); g.moveTo(...tr(-HALF, c)); g.lineTo(...tr(HALF, c)); g.stroke();
    }
    // Markers
    for (const m of markers || []) {
      const [x, y] = tr(m.pos.x, m.pos.z);
      g.fillStyle = m.color;
      g.beginPath(); g.arc(x, y, 6, 0, Math.PI * 2); g.fill();
      g.fillStyle = '#000'; g.font = 'bold 9px sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
      g.fillText(m.icon, x, y + 0.5);
    }
    if (race) {
      // remaining route
      g.strokeStyle = 'rgba(0,229,255,0.9)';
      g.lineWidth = 3;
      g.beginPath();
      g.moveTo(cx, cy);
      for (let i = race.player.next; i < Math.min(race.gates.length, race.player.next + 6); i++) g.lineTo(...tr(race.gates[i].x, race.gates[i].z));
      g.stroke();
      for (const r of race.racers) {
        if (r === race.player) continue;
        const [x, y] = tr(r.actor.pos.x, r.actor.pos.z);
        g.fillStyle = r.actor.disabled > 0 ? '#555' : (r.actor.boss ? '#ff2bd6' : '#ff4d4d');
        g.beginPath(); g.arc(x, y, 4.5, 0, Math.PI * 2); g.fill();
      }
    }
    // Player arrow (relative to camera yaw)
    const rel = player.yaw - yaw;
    g.translate(cx, cy);
    g.rotate(-rel);
    g.fillStyle = '#00e5ff';
    g.beginPath(); g.moveTo(0, -8); g.lineTo(6, 7); g.lineTo(0, 3); g.lineTo(-6, 7); g.closePath(); g.fill();
    g.restore();
    g.strokeStyle = 'rgba(0,229,255,0.5)';
    g.lineWidth = 2;
    g.beginPath(); g.arc(cx, cy, W / 2 - 2, 0, Math.PI * 2); g.stroke();
  }
}
