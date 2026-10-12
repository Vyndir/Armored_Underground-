import * as THREE from 'three';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';

import { City, GARAGE_DOOR, ROAD } from './city.js';
import { Input } from './input.js';
import { MechActor, MECH_RADIUS } from './actor.js';
import { Weapons } from './weapons.js';
import { Race, EVENTS, eventStart } from './race.js';
import { Hud, fmtTime } from './hud.js';
import { Garage } from './garage.js';
import { Audio } from './audio.js';
import { loadSave, writeSave, resetSave } from './save.js';

const $ = id => document.getElementById(id);

// ---------- Renderer / scene ----------
const canvas = $('game');
const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(devicePixelRatio, 1.5));
renderer.setSize(innerWidth, innerHeight);
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.05;

const scene = new THREE.Scene();
const FOG = 0x0a0d1a;
scene.background = new THREE.Color(FOG);
scene.fog = new THREE.FogExp2(FOG, 0.0032);
scene.add(new THREE.HemisphereLight(0x4a5a8a, 0x120a14, 0.9));
const moon = new THREE.DirectionalLight(0x8fa8ff, 0.6);
moon.position.set(-200, 400, -100);
scene.add(moon);

const camera = new THREE.PerspectiveCamera(65, innerWidth / innerHeight, 0.5, 1600);
const composer = new EffectComposer(renderer);
const renderPass = new RenderPass(scene, camera);
composer.addPass(renderPass);
const bloom = new UnrealBloomPass(new THREE.Vector2(innerWidth, innerHeight), 0.7, 0.45, 0.85);
composer.addPass(bloom);
composer.addPass(new OutputPass());

addEventListener('resize', () => {
  renderer.setSize(innerWidth, innerHeight);
  composer.setSize(innerWidth, innerHeight);
  camera.aspect = innerWidth / innerHeight;
  camera.updateProjectionMatrix();
});

// ---------- World ----------
const save = loadSave();
const audio = new Audio();
const input = new Input(canvas);
const city = new City(scene);
const hud = new Hud();
const world = { scene, city, actors: [], audio, player: null, weapons: null };
world.weapons = new Weapons(scene, world);

const player = new MechActor({ name: 'YOU', loadout: save.loadout, paint: save.paint, isPlayer: true, scene });
world.player = player;
world.actors = [player];
const headlight = new THREE.PointLight(0xbfe6ff, 40, 45, 1.6);
scene.add(headlight);
// soft key light that rides with the camera so the player's mech always reads
const keyLight = new THREE.PointLight(0xffe2c4, 30, 40, 1.6);
scene.add(keyLight);

function spawnAtGarage() {
  player.repair();
  player.place(GARAGE_DOOR.clone().add(new THREE.Vector3(-14, 0, 0)), Math.PI / 2);
  cam.yaw = Math.PI / 2;
}

// ---------- Roam markers ----------
const markers = [];
function buildMarkers() {
  for (const m of markers) scene.remove(m.group);
  markers.length = 0;
  const glow = c => new THREE.MeshBasicMaterial({ color: new THREE.Color(c), transparent: true, opacity: 0.1, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false });
  const make = (pos, color, icon, kind, ev) => {
    const group = new THREE.Group();
    group.position.copy(pos);
    const pillar = new THREE.Mesh(new THREE.CylinderGeometry(2.2, 2.2, 160, 16, 1, true), glow(color));
    pillar.position.y = 80;
    const ring = new THREE.Mesh(new THREE.TorusGeometry(7, 0.35, 8, 40), new THREE.MeshBasicMaterial({ color: new THREE.Color(color).multiplyScalar(3), toneMapped: false }));
    ring.rotation.x = Math.PI / 2; ring.position.y = 0.4;
    group.add(pillar, ring);
    scene.add(group);
    markers.push({ group, ring, pillar, pos, color, icon, kind, ev });
  };
  make(GARAGE_DOOR.clone(), '#ffb300', 'G', 'garage');
  EVENTS.forEach((ev, i) => {
    const locked = save.rep < ev.rep;
    const done = save.results[ev.id]?.bestPos === 1;
    make(eventStart(ev), locked ? '#ff3d3d' : done ? '#39ff14' : '#00e5ff', String(i + 1), 'event', ev);
  });
}
function setMarkersVisible(v) { for (const m of markers) m.group.visible = v; }

// ---------- Camera ----------
const cam = { yaw: Math.PI, pitch: 0.12, dist: 15, shake: 0, fov: 65 };
const _f = new THREE.Vector3();
function updateCamera(dt) {
  const sens = 0.0022;
  cam.yaw -= input.mouseDX * sens;
  cam.pitch = THREE.MathUtils.clamp(cam.pitch - input.mouseDY * sens, -0.55, 0.85);
  const f = _f.set(Math.sin(cam.yaw) * Math.cos(cam.pitch), Math.sin(cam.pitch), Math.cos(cam.yaw) * Math.cos(cam.pitch));
  const right = new THREE.Vector3(-Math.cos(cam.yaw), 0, Math.sin(cam.yaw));
  const pivot = player.pos.clone().add(new THREE.Vector3(0, 7.8, 0)).addScaledVector(right, 2.6);
  // pull in if a building is behind us
  let d = cam.dist + Math.min(player.speed, 80) * 0.03;
  const probe = new THREE.Vector3();
  for (let t = 2; t <= d; t += 1) {
    probe.copy(pivot).addScaledVector(f, -t);
    if (city.pointBlocked(probe)) { d = Math.max(3, t - 1.5); break; }
  }
  camera.position.copy(pivot).addScaledVector(f, -d);
  if (camera.position.y < 0.8) camera.position.y = 0.8;
  if (cam.shake > 0) {
    cam.shake = Math.max(0, cam.shake - dt * 2.5);
    const s = cam.shake * cam.shake * 0.8;
    camera.position.x += (Math.random() - 0.5) * s;
    camera.position.y += (Math.random() - 0.5) * s;
    camera.position.z += (Math.random() - 0.5) * s;
  }
  camera.lookAt(pivot.clone().addScaledVector(f, 30));
  const targetFov = 65 + Math.min(1, player.speed / 70) * 14 + (player.boosting ? 4 : 0);
  cam.fov = THREE.MathUtils.lerp(cam.fov, targetFov, Math.min(1, dt * 3));
  camera.fov = cam.fov;
  camera.updateProjectionMatrix();
}

// ---------- Player control + lock-on ----------
let lockTarget = null;
const raycastPoint = new THREE.Vector3();
function aimRay() {
  const dir = new THREE.Vector3();
  camera.getWorldDirection(dir);
  const p = new THREE.Vector3();
  // march the camera ray to the first building (or 500m out)
  for (let t = 8; t < 500; t += 3) {
    p.copy(camera.position).addScaledVector(dir, t);
    if (city.pointBlocked(p)) return raycastPoint.copy(p);
  }
  return raycastPoint.copy(camera.position).addScaledVector(dir, 500);
}

function findLock() {
  const dir = new THREE.Vector3();
  camera.getWorldDirection(dir);
  let best = null, bestAng = 0.2;
  for (const a of world.actors) {
    if (a === player || a.disabled > 0) continue;
    const c = a.pos.clone().setY(a.pos.y + 4);
    const dist = c.distanceTo(player.pos);
    if (dist > player.stats.lockRange) continue;
    const to = c.clone().sub(camera.position).normalize();
    const ang = Math.acos(THREE.MathUtils.clamp(to.dot(dir), -1, 1));
    // sticky: keep current lock in a wider cone
    const limit = a === lockTarget ? 0.32 : bestAng;
    if (ang < limit && !city.lineBlocked(player.pos.clone().setY(player.pos.y + 5), c, 5)) {
      best = a; bestAng = Math.min(ang, bestAng);
    }
  }
  if (best && best !== lockTarget) audio.ui();
  lockTarget = best;
}

function controlPlayer() {
  const c = player.controls;
  const f = new THREE.Vector3(Math.sin(cam.yaw), 0, Math.cos(cam.yaw));
  const r = new THREE.Vector3(-Math.cos(cam.yaw), 0, Math.sin(cam.yaw));
  c.move.set(0, 0, 0);
  if (input.down('KeyW')) c.move.add(f);
  if (input.down('KeyS')) c.move.sub(f);
  if (input.down('KeyD')) c.move.add(r);
  if (input.down('KeyA')) c.move.sub(r);
  if (c.move.lengthSq() > 0) c.move.normalize();
  c.boost = input.down('ShiftLeft') || input.down('ShiftRight');
  c.jump = input.down('Space');
  if (input.hit('Space')) c.jumpPressed = true;
  if (input.hit('KeyQ') || input.hit('ControlLeft')) c.quickBoost = true;
  c.fire = input.buttons.has(0);
  c.fire2 = input.clicked.has(2) || input.buttons.has(2);
  c.reload = input.hit('KeyR');
  c.face = cam.yaw;
  findLock();
  c.target = lockTarget;
  if (lockTarget) {
    const tc = lockTarget.pos.clone().setY(lockTarget.pos.y + 4);
    const w = player.stats.parts.weapon;
    const tFlight = tc.distanceTo(player.pos) / w.speed;
    c.aimPoint.copy(tc).addScaledVector(lockTarget.vel, tFlight);
  } else {
    c.aimPoint.copy(aimRay());
  }
  const chest = player.pos.clone().setY(player.pos.y + 6);
  const to = c.aimPoint.clone().sub(chest);
  c.aimPitch = Math.atan2(to.y, Math.hypot(to.x, to.z));
}

player.onQuickBoost = () => { audio.quick(); cam.shake = Math.max(cam.shake, 0.4); };

// ---------- Mech vs mech ----------
function mechCollisions() {
  const A = world.actors;
  for (let i = 0; i < A.length; i++) for (let j = i + 1; j < A.length; j++) {
    const a = A[i], b = A[j];
    const dx = b.pos.x - a.pos.x, dz = b.pos.z - a.pos.z;
    const d = Math.hypot(dx, dz);
    const min = MECH_RADIUS * 2 + 0.4;
    if (d >= min || d < 1e-4 || Math.abs(a.pos.y - b.pos.y) > 6) continue;
    const nx = dx / d, nz = dz / d;
    const push = (min - d) / 2;
    a.pos.x -= nx * push; a.pos.z -= nz * push;
    b.pos.x += nx * push; b.pos.z += nz * push;
    const rel = (a.vel.x - b.vel.x) * nx + (a.vel.z - b.vel.z) * nz;
    if (rel > 0) {
      const imp = rel * 0.9;
      a.vel.x -= nx * imp; a.vel.z -= nz * imp;
      b.vel.x += nx * imp; b.vel.z += nz * imp;
      if (rel > 22) {
        const dmg = (rel - 22) * 7;
        a.damage(dmg * 0.6, b);
        b.damage(dmg, a);
        world.weapons.burst(a.pos.clone().add(b.pos).multiplyScalar(0.5).setY(a.pos.y + 3), '#ffd27a', 20, 20);
        if (a.isPlayer || b.isPlayer) { cam.shake = 0.9; audio.hurt(); }
      }
    }
  }
}

// ---------- Game state ----------
let state = 'title';   // title | roam | race | garage | menu | results
let race = null;
let paused = false;
let pendingEvent = null;
let raceEndTimer = 0;

function openOverlay(id, v) { $(id).style.display = v ? 'flex' : 'none'; }

function startGame() {
  audio.start();
  openOverlay('title', false);
  hud.show(true);
  state = 'roam';
  input.lock();
  hud.toast('WELCOME TO THE UNDERGROUND', '#00e5ff', 3);
}
$('start-btn').onclick = startGame;

input.onLockChange = locked => {
  if (!locked && (state === 'roam' || state === 'race')) {
    paused = true;
    openOverlay('pause', true);
    $('retire-btn').style.display = state === 'race' ? 'block' : 'none';
  }
};
$('resume-btn').onclick = () => { paused = false; openOverlay('pause', false); input.lock(); };
$('retire-btn').onclick = () => {
  paused = false; openOverlay('pause', false);
  endRace(false);
  input.lock();
};
$('reset-btn').onclick = () => {
  if (!confirm('Reset all progress, credits and parts?')) return;
  Object.assign(save, resetSave());
  player.setLoadout(save.loadout, save.paint);
  if (race) endRace(false);
  buildMarkers();
  spawnAtGarage();
  hud.toast('SAVE RESET', '#ff3d3d');
};
canvas.addEventListener('click', () => {
  if ((state === 'roam' || state === 'race') && !paused) input.lock();
});

// Garage
const garage = new Garage(renderer, save, audio, () => player.setLoadout(save.loadout, save.paint));
garage.onExit = () => exitGarage();
function enterGarage() {
  state = 'garage';
  input.unlock();
  hud.show(false);
  garage.enter();
  audio.engine(0, 0, false);
}
function exitGarage() {
  garage.exit();
  state = 'roam';
  hud.show(true);
  spawnAtGarage();
  input.lock();
}

// Event card
function showEventCard(ev) {
  pendingEvent = ev;
  state = 'menu';
  input.unlock();
  const locked = save.rep < ev.rep;
  const best = save.results[ev.id];
  $('ev-type').textContent = `${ev.type}${ev.type === 'CIRCUIT' ? ` · ${ev.laps} LAPS` : ''}`;
  $('ev-name').textContent = ev.name;
  $('ev-blurb').textContent = ev.blurb;
  $('ev-meta').innerHTML = `
    <div><small>RIVALS</small>${ev.rivals}</div>
    <div><small>1ST PRIZE</small>${ev.payout[0].toLocaleString()} CR</div>
    <div><small>REP REQUIRED</small>${ev.rep}</div>
    <div><small>BEST</small>${best ? `P${best.bestPos} · ${fmtTime(best.bestTime)}` : '—'}</div>
    ${locked ? `<div class="locked">LOCKED: earn ${ev.rep - save.rep} more REP in other races.</div>` : ''}`;
  $('ev-start').style.display = locked ? 'none' : 'block';
  openOverlay('event-card', true);
}
function closeEventCard() {
  openOverlay('event-card', false);
  pendingEvent = null;
  state = 'roam';
  input.lock();
}
$('ev-cancel').onclick = closeEventCard;
$('ev-start').onclick = () => startRace();

function startRace() {
  const ev = pendingEvent;
  if (!ev || save.rep < ev.rep) return;
  openOverlay('event-card', false);
  pendingEvent = null;
  world.weapons.clear();
  race = new Race(world, ev);
  world.onKO = (victim, from) => {
    if (from === player) { hud.toast(`${victim.name} DOWN  +250 CR`, '#ff2bd6'); audio.boom(victim.pos); }
    else if (victim === player) hud.toast(from ? `TAKEN DOWN BY ${from.name}` : 'WRECKED', '#ff3d3d');
    world.weapons.explode(victim.pos.clone().setY(victim.pos.y + 4), 0, 1, null);
  };
  world.onRaceStart = () => hud.toast('GO!', '#39ff14', 1);
  cam.yaw = race.startYaw;
  setMarkersVisible(false);
  state = 'race';
  input.lock();
}

function endRace(completed) {
  if (!race) return;
  if (completed) {
    const r = race.result;
    const total = r.credits + r.bonus;
    save.credits += total;
    save.rep += r.rep;
    const prev = save.results[race.ev.id];
    save.results[race.ev.id] = {
      bestPos: prev ? Math.min(prev.bestPos, r.position) : r.position,
      bestTime: prev ? Math.min(prev.bestTime, r.time) : r.time,
    };
    writeSave(save);
  }
  const ev = race.ev;
  race.dispose();
  race = null;
  world.weapons.clear();
  buildMarkers();
  setMarkersVisible(true);
  player.repair();
  player.onRecovered = () => player.repair();
  player.place(eventStart(ev).add(new THREE.Vector3(0, 0, 0)), player.yaw);
  state = 'roam';
}

function showResults() {
  const r = race.result;
  state = 'results';
  input.unlock();
  const ord = ['1ST', '2ND', '3RD', '4TH', '5TH', '6TH'];
  $('res-event').textContent = `${race.ev.type} · ${race.ev.name}`;
  $('res-pos').textContent = r.position === 1 ? 'YOU WIN' : `${ord[r.position - 1]} PLACE`;
  $('res-pos').style.color = r.position === 1 ? '#39ff14' : '#e8edf5';
  $('res-table').innerHTML = race.final.map((x, i) => `<div class="${x === race.player ? 'me' : ''}"><span>${i + 1}. ${x === race.player ? 'YOU' : x.actor.name}</span><span>${x.finished ? fmtTime(x.time) : 'DNF'} · ${x.kos} KO</span></div>`).join('');
  $('res-reward').innerHTML = `+${r.credits.toLocaleString()} CR prize${r.bonus ? ` · +${r.bonus} CR takedowns` : ''} · +${r.rep} REP`;
  openOverlay('results', true);
}
$('res-continue').onclick = () => {
  openOverlay('results', false);
  endRace(true);
  input.lock();
};

// Hit feedback
world.onHit = (target, dmg, from) => {
  if (from === player) { hud.hit(); audio.hit(); }
  if (target === player) { hud.hurt(); cam.shake = Math.max(cam.shake, 0.35); }
};
player.onRecovered = () => player.repair();

// Global keys
addEventListener('keydown', e => {
  if (state === 'title' && (e.code === 'Enter' || e.code === 'Space')) startGame();
  else if (state === 'menu' && pendingEvent) {
    if (e.code === 'Enter') startRace();
    if (e.code === 'Escape') closeEventCard();
  } else if (state === 'results' && e.code === 'Enter') $('res-continue').click();
  else if (state === 'garage' && e.code === 'Escape') exitGarage();
});

// ---------- Main loop ----------
openOverlay('title', true);
buildMarkers();
spawnAtGarage();
let last = performance.now();
let titleT = 0;

function frame(now) {
  requestAnimationFrame(frame);
  const dt = Math.max(0, Math.min(0.05, (now - last) / 1000));
  last = now;
  step(dt);
}

function step(dt) {
  if (state === 'garage') {
    const v = garage.draw(dt);
    renderPass.scene = v.scene; renderPass.camera = v.camera;
    composer.render();
    renderPass.scene = scene; renderPass.camera = camera;
    input.endFrame();
    return;
  }

  if (state === 'title') {
    // slow attract-mode orbit around the player mech
    titleT += dt;
    const p = player.pos;
    camera.position.set(p.x + Math.sin(titleT * 0.15) * 24, 7 + Math.sin(titleT * 0.3) * 2, p.z + Math.cos(titleT * 0.15) * 24);
    camera.lookAt(p.x, 5, p.z);
    player.update(dt, world);
    world.weapons.update(dt);
    composer.render();
    input.endFrame();
    return;
  }

  const active = (state === 'roam' || state === 'race') && !paused;
  if (active) {
    controlPlayer();
    if (race) race.update(dt);
    for (const a of world.actors) a.update(dt, world);
    mechCollisions();
    world.weapons.update(dt);
    if (player.impact > 0) { cam.shake = Math.min(1.2, cam.shake + player.impact * 0.03); audio.hurt(); hud.hurt(); }
  }
  updateCamera(active ? dt : 0);

  keyLight.position.copy(camera.position).lerp(player.pos, 0.45).add(new THREE.Vector3(0, 6, 0));
  headlight.position.copy(player.pos).add(new THREE.Vector3(Math.sin(player.yaw) * 6, 7, Math.cos(player.yaw) * 6));
  audio.setListener(player.pos);
  audio.engine(player.speed, player.boostLevel, active);

  // Roam interactions
  let prompt = null;
  if (state === 'roam' && active) {
    for (const m of markers) {
      m.ring.rotation.z += dt;
      const md = Math.hypot(m.pos.x - player.pos.x, m.pos.z - player.pos.z);
      // fade the light pillar as you walk into it so it doesn't wash out the view
      m.pillar.material.opacity = 0.1 * THREE.MathUtils.clamp((md - 8) / 40, 0, 1);
      if (md < 12) {
        prompt = m.kind === 'garage' ? '<kbd>E</kbd> ENTER GARAGE' : `<kbd>E</kbd> ${m.ev.type} · ${m.ev.name}`;
        if (input.hit('KeyE')) {
          if (m.kind === 'garage') enterGarage();
          else showEventCard(m.ev);
        }
      }
    }
  }

  // Race end
  if (race && race.state === 'done' && state === 'race') {
    raceEndTimer += dt;
    if (raceEndTimer > 2) { raceEndTimer = 0; showResults(); }
    else if (raceEndTimer - dt <= 0) hud.toast(race.result.position === 1 ? 'FIRST PLACE!' : 'FINISHED', '#39ff14', 2);
  }

  hud.update(dt, { player, camera, camYaw: cam.yaw, lock: lockTarget, race, save, prompt, markers: state === 'roam' ? markers : null });
  composer.render();
  input.endFrame();
}
requestAnimationFrame(frame);

// Expose for debugging in the console
window.__game = {
  world, player, save, step, cam, endRace,
  startEvent(id) { pendingEvent = EVENTS.find(e => e.id === id); startRace(); }, get race() { return race; }, get state() { return state; } };
