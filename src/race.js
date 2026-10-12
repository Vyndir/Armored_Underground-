import * as THREE from 'three';
import { node, ROAD } from './city.js';
import { MechActor } from './actor.js';
import { RacerAI } from './ai.js';

// Underground race events. Routes are lists of grid intersections where each
// consecutive pair shares a road, so every leg is a straight street.
export const EVENTS = [
  {
    id: 'canal', name: 'CANAL LINE', type: 'SPRINT', laps: 1, rep: 0, skill: 0.82, rivals: 3,
    route: [[5, 4], [9, 4], [9, 8], [5, 8], [5, 10], [0, 10]],
    payout: [3200, 1800, 900, 300], repGain: [3, 2, 1, 0],
    blurb: 'Warm-up sprint down the canal-side boulevard. Weapons hot.',
  },
  {
    id: 'neonloop', name: 'NEON LOOP', type: 'CIRCUIT', laps: 2, rep: 2, skill: 0.92, rivals: 3,
    route: [[3, 3], [7, 3], [7, 6], [5, 6], [5, 8], [2, 8], [2, 5], [3, 5]],
    payout: [4800, 2600, 1300, 400], repGain: [4, 2, 1, 0],
    blurb: 'Two laps through the club district. Tight corners, tighter rivals.',
  },
  {
    id: 'ironring', name: 'IRON RING', type: 'CIRCUIT', laps: 2, rep: 5, skill: 1.0, rivals: 4,
    route: [[1, 1], [9, 1], [9, 9], [1, 9]],
    payout: [7000, 3800, 1900, 800, 300], repGain: [5, 3, 2, 1, 0],
    blurb: 'Long straights around the industrial ring. Boost economy matters.',
  },
  {
    id: 'deaddrop', name: 'DEAD DROP', type: 'SPRINT', laps: 1, rep: 9, skill: 1.1, rivals: 3, boss: true,
    route: [[1, 5], [3, 5], [3, 1], [6, 1], [6, 9], [8, 9], [8, 2], [10, 2]],
    payout: [12000, 5000, 2000, 500], repGain: [8, 3, 1, 0],
    blurb: 'Invitation only. VANTA, king of the underground, runs a rail cannon.',
  },
];

const RIVALS = [
  { name: 'KESTREL', loadout: { head: 'hd_moth', core: 'cr_wasp', arms: 'ar_needle', legs: 'lg_hopper', booster: 'bs_comet', generator: 'gn_surge', weapon: 'wp_gale', shoulder: 'sh_swarm' }, paint: { primary: '#5a1220', secondary: '#d0d0d0', neon: '#ff3d3d' } },
  { name: 'MOTHBALL', loadout: { head: 'hd_bastion', core: 'cr_anvil', arms: 'ar_hauler', legs: 'lg_bulwark', booster: 'bs_flare', generator: 'gn_coil', weapon: 'wp_maul', shoulder: 'sh_mines' }, paint: { primary: '#4a4f3a', secondary: '#a69c6e', neon: '#ffb300' } },
  { name: 'ROOK', loadout: { head: 'hd_pike', core: 'cr_ferro', arms: 'ar_hand', legs: 'lg_strider', booster: 'bs_tern', generator: 'gn_ember', weapon: 'wp_hornet', shoulder: 'sh_swarm' }, paint: { primary: '#1d2b4a', secondary: '#9fb4d8', neon: '#2979ff' } },
  { name: 'SOLDER', loadout: { head: 'hd_moth', core: 'cr_ferro', arms: 'ar_needle', legs: 'lg_glide', booster: 'bs_tern', generator: 'gn_surge', weapon: 'wp_gale', shoulder: 'sh_mines' }, paint: { primary: '#2e1a47', secondary: '#c7b8e0', neon: '#7c4dff' } },
  { name: 'GLASSJAW', loadout: { head: 'hd_pike', core: 'cr_wasp', arms: 'ar_hand', legs: 'lg_glide', booster: 'bs_comet', generator: 'gn_ember', weapon: 'wp_hornet', shoulder: 'sh_spear' }, paint: { primary: '#0e3b36', secondary: '#bfe8e0', neon: '#39ff14' } },
];
const BOSS = { name: 'VANTA', loadout: { head: 'hd_moth', core: 'cr_wasp', arms: 'ar_needle', legs: 'lg_glide', booster: 'bs_comet', generator: 'gn_surge', weapon: 'wp_lance', shoulder: 'sh_spear' }, paint: { primary: '#0a0a0c', secondary: '#2a2a30', neon: '#ff2bd6' } };

export function eventStart(ev) { return node(...ev.route[0]); }

export class Race {
  constructor(world, ev) {
    this.world = world;
    this.ev = ev;
    this.state = 'countdown';
    this.countdown = 3.99;
    this.time = 0;
    this.finishOrder = [];

    // Gate list
    const pts = ev.route.map(r => node(...r));
    this.gates = [];
    if (ev.type === 'CIRCUIT') {
      for (let l = 0; l < ev.laps; l++) {
        for (let k = 1; k < pts.length; k++) this.gates.push(pts[k]);
        this.gates.push(pts[0]);
      }
    } else {
      for (let k = 1; k < pts.length; k++) this.gates.push(pts[k]);
    }
    this.gatesPerLap = ev.type === 'CIRCUIT' ? pts.length : this.gates.length;
    this.startPos = pts[0];

    // Grid: line up behind the start node facing the first gate
    const fwd = new THREE.Vector3().subVectors(pts[1], pts[0]).setY(0).normalize();
    const right = new THREE.Vector3(fwd.z, 0, -fwd.x);
    const yaw = Math.atan2(fwd.x, fwd.z);
    this.startYaw = yaw;
    const slots = [];
    for (let k = 0; k < 6; k++) {
      const row = Math.floor(k / 2), col = k % 2 ? 1 : -1;
      slots.push(pts[0].clone().addScaledVector(fwd, -14 - row * 13).addScaledVector(right, col * 5.5));
    }

    // Racers: player + rivals
    const player = world.player;
    this.racers = [];
    const order = [];
    const pool = [...RIVALS].sort(() => Math.random() - 0.5).slice(0, ev.rivals - (ev.boss ? 1 : 0));
    if (ev.boss) pool.unshift(BOSS);
    // Player starts at the back of the grid (classic underground style)
    pool.forEach((r, i) => order.push({ rival: r, slot: slots[i] }));
    order.push({ rival: null, slot: slots[pool.length] });
    order.forEach(({ rival, slot }, i) => {
      let actor, ai = null;
      if (rival) {
        const skill = ev.skill * (rival === BOSS ? 1.06 : 0.96 + Math.random() * 0.08);
        actor = new MechActor({ name: rival.name, loadout: rival.loadout, paint: rival.paint, scene: world.scene, statScale: skill });
        actor.boss = rival === BOSS;
        ai = new RacerAI(actor, this, skill, (i % 3 - 1) * 5);
      } else {
        actor = player;
        actor.repair();
      }
      actor.place(slot, yaw);
      const racer = { actor, ai, next: 0, finished: false, time: 0, respawn: slot.clone(), respawnYaw: yaw, kos: 0 };
      if (ai) ai.racer = racer;
      actor.onRecovered = () => this._respawn(racer);
      this.racers.push(racer);
    });
    this.player = this.racers.find(r => r.actor === player);
    for (const r of this.racers) {
      r.actor.onDisabled = from => {
        const fr = this.racers.find(x => x.actor === from);
        if (fr) fr.kos++;
        world.onKO?.(r.actor, from);
      };
    }
    world.actors = this.racers.map(r => r.actor);

    // Gate visuals: next gate ring + beacon
    const neon = new THREE.Color('#00e5ff');
    this.ring = new THREE.Mesh(new THREE.TorusGeometry(ROAD / 2 - 1, 0.5, 8, 48),
      new THREE.MeshBasicMaterial({ color: neon.clone().multiplyScalar(3), toneMapped: false, transparent: true, opacity: 0.9 }));
    this.beacon = new THREE.Mesh(new THREE.CylinderGeometry(2.5, 2.5, 300, 12, 1, true),
      new THREE.MeshBasicMaterial({ color: neon.clone().multiplyScalar(1.5), transparent: true, opacity: 0.18, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }));
    this.ring2 = new THREE.Mesh(this.ring.geometry, new THREE.MeshBasicMaterial({ color: neon, transparent: true, opacity: 0.25, toneMapped: false }));
    world.scene.add(this.ring, this.beacon, this.ring2);
    this._placeGate();
  }

  gateDir(i) {
    const a = i === 0 ? this.startPos : this.gates[i - 1];
    const b = this.gates[i];
    return new THREE.Vector3().subVectors(b, a).setY(0).normalize();
  }

  _placeGate() {
    const n = this.player.next;
    const show = (mesh, i) => {
      if (i >= this.gates.length) { mesh.visible = false; return; }
      mesh.visible = true;
      const g = this.gates[i];
      const d = this.gateDir(i);
      mesh.position.set(g.x, ROAD / 2 - 1, g.z);
      mesh.rotation.set(0, Math.atan2(d.x, d.z), 0);
    };
    show(this.ring, n);
    show(this.ring2, n + 1);
    const final = n === this.gates.length - 1;
    this.ring.material.color.set(final ? '#ffd400' : '#00e5ff').multiplyScalar(3);
    if (n < this.gates.length) this.beacon.position.set(this.gates[n].x, 150, this.gates[n].z);
    this.beacon.visible = n < this.gates.length;
  }

  _respawn(r) {
    const a = r.actor;
    a.repair();
    a.ap = Math.round(a.stats.ap * 0.6);
    const next = this.gates[Math.min(r.next, this.gates.length - 1)];
    const d = new THREE.Vector3().subVectors(next, r.respawn).setY(0);
    a.place(r.respawn, Math.atan2(d.x, d.z));
    if (a.isPlayer) this.world.onPlayerRespawn?.();
  }

  progressOf(r) {
    if (r.finished) return 1e9 - r.time;
    const g = this.gates[Math.min(r.next, this.gates.length - 1)];
    return r.next * 10000 - Math.hypot(g.x - r.actor.pos.x, g.z - r.actor.pos.z);
  }

  standings() {
    return [...this.racers].sort((a, b) => this.progressOf(b) - this.progressOf(a));
  }

  get lap() { return Math.min(this.ev.laps, Math.floor(this.player.next / this.gatesPerLap) + 1); }

  update(dt) {
    if (this.state === 'countdown') {
      const before = Math.ceil(this.countdown);
      this.countdown -= dt;
      const after = Math.ceil(this.countdown);
      if (after !== before) this.world.audio?.beep(after <= 0 ? 880 : 440);
      if (this.countdown <= 0) { this.state = 'running'; this.world.onRaceStart?.(); }
      for (const r of this.racers) {
        // hold everyone on the grid
        r.actor.controls.move.set(0, 0, 0);
        r.actor.controls.boost = false;
        r.actor.controls.fire = r.actor.controls.fire2 = false;
      }
      return;
    }
    if (this.state === 'running') this.time += dt;

    for (const r of this.racers) {
      if (r.ai) r.ai.update(dt);
      if (r.finished) continue;
      const g = this.gates[r.next];
      const a = r.actor;
      if (Math.hypot(g.x - a.pos.x, g.z - a.pos.z) < ROAD / 2 + 6) {
        r.respawn = new THREE.Vector3(g.x, 0, g.z);
        r.next++;
        if (r === this.player) { this.world.audio?.gate(r.next >= this.gates.length); this._placeGate(); }
        if (r.next >= this.gates.length) {
          r.finished = true;
          r.time = this.time;
          this.finishOrder.push(r);
          if (r === this.player) this.finish();
        }
      }
    }
  }

  finish() {
    this.state = 'done';
    // Everyone still racing is ranked by current progress
    const final = [...this.finishOrder];
    for (const r of this.standings()) if (!final.includes(r)) final.push(r);
    this.final = final;
    const pos = final.indexOf(this.player);
    this.result = {
      position: pos + 1,
      total: final.length,
      time: this.time,
      credits: this.ev.payout[pos] || 0,
      rep: this.ev.repGain[pos] || 0,
      kos: this.player.kos,
      bonus: this.player.kos * 250,
    };
  }

  dispose() {
    for (const r of this.racers) if (r.actor !== this.world.player) r.actor.remove();
    this.world.scene.remove(this.ring, this.beacon, this.ring2);
    this.world.actors = [this.world.player];
    this.world.player.onRecovered = null;
    this.world.player.onDisabled = null;
  }
}
