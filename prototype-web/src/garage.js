import * as THREE from 'three';
import { PARTS, SLOTS, SLOT_LABELS, computeStats, getPart } from './parts.js';
import { MechVisual } from './mech.js';
import { writeSave } from './save.js';

const $ = id => document.getElementById(id);

function partStats(slot, p) {
  const s = [];
  if (p.ap) s.push(['AP', p.ap]);
  s.push(['WEIGHT', p.weight]);
  switch (slot) {
    case 'head': s.push(['LOCK RANGE', `${p.lockRange}m`]); break;
    case 'core': s.push(['BOOST', `${Math.round(p.boostMult * 100)}%`]); break;
    case 'arms': s.push(['FIRE RATE', `${Math.round(p.fireRate * 100)}%`]); break;
    case 'legs': s.push(['LOAD LIMIT', p.load], ['SPEED', `${Math.round(p.speed * 100)}%`], ['JUMP', `${Math.round(p.jump * 100)}%`], ['GRIP', `${Math.round(p.grip * 100)}%`]); break;
    case 'booster': s.push(['THRUST', `${Math.round(p.boost * 100)}%`], ['EN DRAIN', `${Math.round(p.drain * 100)}%`], ['QUICK BOOST', `${Math.round(p.qb * 100)}%`]); break;
    case 'generator': s.push(['EN CAPACITY', p.cap], ['EN RECOVERY', `${p.regen}/s`]); break;
    case 'weapon': s.push(['DAMAGE', p.pellets > 1 ? `${p.dmg}×${p.pellets}` : p.dmg], ['FIRE RATE', `${p.rate}/s`], ['MAGAZINE', p.mag], ['RELOAD', `${p.reload}s`]); break;
    case 'shoulder': s.push(['DAMAGE', p.count > 1 ? `${p.dmg}×${p.count}` : p.dmg], ['COOLDOWN', `${p.cooldown}s`]); break;
  }
  return s;
}

const SUMMARY = [
  ['AP', s => s.ap, 0],
  ['WEIGHT / LOAD', s => `${s.carried} / ${s.load}`, null],
  ['GROUND SPEED', s => Math.round(s.groundSpeed * 3.6), 0, ' km/h'],
  ['BOOST SPEED', s => Math.round(s.boostSpeed * 3.6), 0, ' km/h'],
  ['EN CAPACITY', s => s.enCap, 0],
  ['EN RECOVERY', s => s.enRegen, 0, '/s'],
  ['BOOST TIME', s => (s.enCap / s.boostDrain).toFixed(1), 0, 's'],
  ['LOCK RANGE', s => s.lockRange, 0, 'm'],
  ['WEAPON DPS', s => Math.round(s.parts.weapon.dmg * s.parts.weapon.pellets * s.parts.weapon.rate * s.fireRateMult), 0],
];

// Garage: own little showroom scene + DOM panels for parts and paint.
export class Garage {
  constructor(renderer, save, audio, onApply) {
    this.renderer = renderer;
    this.save = save;
    this.audio = audio;
    this.onApply = onApply;
    this.slot = 'legs';
    this.preview = null;

    const scene = this.scene = new THREE.Scene();
    scene.background = new THREE.Color(0x07090f);
    scene.fog = new THREE.Fog(0x07090f, 30, 70);
    this.camera = new THREE.PerspectiveCamera(40, innerWidth / innerHeight, 0.1, 200);
    scene.add(new THREE.HemisphereLight(0x8899cc, 0x111118, 1.2));
    const key = new THREE.SpotLight(0xffd9a0, 900, 60, 0.6, 0.5);
    key.position.set(8, 18, 10);
    scene.add(key);
    const rim = new THREE.SpotLight(0x00e5ff, 700, 60, 0.7, 0.5);
    rim.position.set(-10, 8, -12);
    scene.add(rim);
    const floor = new THREE.Mesh(new THREE.CircleGeometry(40, 48), new THREE.MeshStandardMaterial({ color: 0x15171d, roughness: 0.25, metalness: 0.5 }));
    floor.rotation.x = -Math.PI / 2;
    scene.add(floor);
    const plat = new THREE.Mesh(new THREE.CylinderGeometry(6, 6.4, 0.4, 48), new THREE.MeshStandardMaterial({ color: 0x23262e, roughness: 0.5, metalness: 0.6 }));
    plat.position.y = 0.2;
    scene.add(plat);
    this.ringMat = new THREE.MeshBasicMaterial({ color: 0x00e5ff, toneMapped: false });
    const ring = new THREE.Mesh(new THREE.TorusGeometry(6.2, 0.08, 8, 64), this.ringMat);
    ring.rotation.x = Math.PI / 2; ring.position.y = 0.42;
    scene.add(ring);
    this.turn = new THREE.Group();
    this.turn.position.y = 0.4;
    scene.add(this.turn);
    this.angle = 0.6;

    $('garage-exit').onclick = () => this.onExit?.();
    for (const k of ['primary', 'secondary', 'neon']) {
      const inp = $(`paint-${k}`);
      inp.oninput = () => { this.save.paint[k] = inp.value; this._apply(); };
    }
    const canvas = renderer.domElement;
    let dragging = false, lx = 0;
    canvas.addEventListener('mousedown', e => { if (this.open) { dragging = true; lx = e.clientX; } });
    addEventListener('mouseup', () => { dragging = false; });
    addEventListener('mousemove', e => { if (dragging && this.open) { this.angle += (e.clientX - lx) * 0.01; lx = e.clientX; } });
  }

  enter() {
    this.open = true;
    $('garage').style.display = 'flex';
    for (const k of ['primary', 'secondary', 'neon']) $(`paint-${k}`).value = this.save.paint[k];
    this._rebuildPreview();
    this.render();
  }

  exit() {
    this.open = false;
    $('garage').style.display = 'none';
  }

  _rebuildPreview() {
    if (!this.preview) {
      this.preview = new MechVisual(this.save.loadout, this.save.paint);
      this.turn.add(this.preview.root);
    } else this.preview.build(this.save.loadout, this.save.paint);
    this.ringMat.color.set(this.save.paint.neon);
  }

  _apply() {
    writeSave(this.save);
    this._rebuildPreview();
    this.onApply?.();
    this.render();
  }

  render() {
    const save = this.save;
    // Slot list
    $('garage-slots').innerHTML = SLOTS.map(s => `
      <button class="slot ${s === this.slot ? 'sel' : ''}" data-slot="${s}">
        <small>${SLOT_LABELS[s]}</small><span>${getPart(s, save.loadout[s]).name}</span>
      </button>`).join('');
    for (const b of document.querySelectorAll('#garage-slots .slot')) {
      b.onclick = () => { this.slot = b.dataset.slot; this.audio.ui(); this.render(); };
    }
    // Part list
    const cur = computeStats(save.loadout);
    $('garage-parts').innerHTML = `<h3>${SLOT_LABELS[this.slot]}</h3>` + PARTS[this.slot].map(p => {
      const owned = save.owned[p.id];
      const equipped = save.loadout[this.slot] === p.id;
      const afford = save.credits >= p.price;
      const btn = equipped ? '<button disabled>EQUIPPED</button>'
        : owned ? `<button data-act="equip" data-id="${p.id}">EQUIP</button>`
          : `<button data-act="buy" data-id="${p.id}" ${afford ? '' : 'disabled'}>BUY ${p.price.toLocaleString()} CR</button>`;
      return `<div class="part ${equipped ? 'eq' : ''}" data-id="${p.id}">
        <div class="pname">${p.name}${owned ? '' : `<em>${p.price.toLocaleString()} CR</em>`}</div>
        <div class="pdesc">${p.desc}</div>
        <div class="pstats">${partStats(this.slot, p).map(([k, v]) => `<span><small>${k}</small>${v}</span>`).join('')}</div>
        ${btn}
      </div>`;
    }).join('');
    for (const el of document.querySelectorAll('#garage-parts .part')) {
      el.onmouseenter = () => this._summary({ ...save.loadout, [this.slot]: el.dataset.id });
      el.onmouseleave = () => this._summary(save.loadout);
    }
    for (const b of document.querySelectorAll('#garage-parts button[data-act]')) {
      b.onclick = () => {
        const p = getPart(this.slot, b.dataset.id);
        if (b.dataset.act === 'buy') {
          if (save.credits < p.price) return;
          save.credits -= p.price;
          save.owned[p.id] = true;
          this.audio.buy();
        } else this.audio.ui();
        save.loadout[this.slot] = p.id;
        this._apply();
      };
    }
    $('garage-credits').innerHTML = `CR <b>${save.credits.toLocaleString()}</b> &nbsp; REP <b>${save.rep}</b>`;
    this._summary(save.loadout, cur);
  }

  _summary(loadout) {
    const base = computeStats(this.save.loadout);
    const s = computeStats(loadout);
    $('garage-stats').innerHTML = SUMMARY.map(([label, fn, _, unit = '']) => {
      const v = fn(s), b = fn(base);
      let delta = '';
      if (typeof v === 'number' && v !== b) {
        const d = Math.round((v - b) * 10) / 10;
        delta = `<i class="${d > 0 ? 'up' : 'down'}">${d > 0 ? '+' : ''}${d}</i>`;
      }
      return `<div><small>${label}</small><b>${v}${unit}</b>${delta}</div>`;
    }).join('') + (s.overweight ? '<div class="warn">OVERWEIGHT: SPEED PENALTY</div>' : '');
  }

  draw(dt) {
    this.angle += dt * 0.25;
    const r = 21;
    this.camera.aspect = innerWidth / innerHeight;
    this.camera.updateProjectionMatrix();
    this.camera.position.set(Math.sin(this.angle) * r, 6.5, Math.cos(this.angle) * r);
    this.camera.lookAt(0, 4.2, 0);
    // shift the mech left of center so panels don't cover it
    this.camera.setViewOffset(innerWidth, innerHeight, 60, 0, innerWidth, innerHeight);
    if (this.preview) {
      this.preview.animate({ dt, speed: 0, localVel: new THREE.Vector3(), grounded: true, boosting: false, boostLevel: 0.15, aimPitch: -0.3, disabled: false, height: 0 });
    }
    return { scene: this.scene, camera: this.camera };
  }
}
