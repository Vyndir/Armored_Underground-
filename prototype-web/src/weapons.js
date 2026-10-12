import * as THREE from 'three';
import { GLOW_TEX } from './city.js';

const _a = new THREE.Vector3();
const _b = new THREE.Vector3();
const _c = new THREE.Vector3();
const UP = new THREE.Vector3(0, 1, 0);
const Z = new THREE.Vector3(0, 0, 1);

function centerOf(actor, out) { return out.copy(actor.pos).setY(actor.pos.y + 4); }

// Distance from point p to segment ab.
function segDist(a, b, p) {
  _c.subVectors(b, a);
  const len2 = _c.lengthSq();
  let t = len2 > 0 ? _a.subVectors(p, a).dot(_c) / len2 : 0;
  t = Math.max(0, Math.min(1, t));
  return _a.copy(a).addScaledVector(_c, t).distanceTo(p);
}

// Projectiles, missiles, mines, rail beams and effects.
export class Weapons {
  constructor(scene, world) {
    this.scene = scene;
    this.world = world;
    this.bullets = [];
    this.missiles = [];
    this.mines = [];
    this.beams = [];
    this.blasts = [];

    this.bulletGeo = new THREE.BoxGeometry(0.18, 0.18, 3.2);
    this.bulletMat = new THREE.MeshBasicMaterial({ color: new THREE.Color('#ffcf70').multiplyScalar(3), toneMapped: false });
    this.enemyBulletMat = new THREE.MeshBasicMaterial({ color: new THREE.Color('#ff5a5a').multiplyScalar(3), toneMapped: false });
    this.missileGeo = new THREE.BoxGeometry(0.4, 0.4, 1.6);
    this.missileMat = new THREE.MeshBasicMaterial({ color: 0xdddddd });
    this.mineGeo = new THREE.CylinderGeometry(0.9, 1.1, 0.4, 12);
    this.mineMat = new THREE.MeshBasicMaterial({ color: new THREE.Color('#ff2b2b').multiplyScalar(2), toneMapped: false });
    this.blastGeo = new THREE.SphereGeometry(1, 16, 12);

    // Pooled point lights for muzzle flashes / explosions
    this.lights = [];
    for (let i = 0; i < 4; i++) {
      const l = new THREE.PointLight(0xffaa55, 0, 40, 2);
      scene.add(l);
      this.lights.push({ light: l, t: 0, max: 0 });
    }
    this.lightIdx = 0;

    // Particle sparks / smoke
    const N = 1200;
    this.pN = N;
    this.pPos = new Float32Array(N * 3);
    this.pVel = new Float32Array(N * 3);
    this.pCol = new Float32Array(N * 3);
    this.pLife = new Float32Array(N);
    this.pIdx = 0;
    for (let i = 0; i < N; i++) this.pPos[i * 3 + 1] = -1000;
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(this.pPos, 3));
    g.setAttribute('color', new THREE.BufferAttribute(this.pCol, 3));
    this.points = new THREE.Points(g, new THREE.PointsMaterial({
      size: 0.9, map: GLOW_TEX, vertexColors: true, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, toneMapped: false,
    }));
    this.points.frustumCulled = false;
    scene.add(this.points);
  }

  flash(pos, color, intensity, dur = 0.08) {
    const L = this.lights[this.lightIdx++ % this.lights.length];
    L.light.position.copy(pos);
    L.light.color.set(color);
    L.light.intensity = intensity;
    L.max = intensity; L.t = dur; L.dur = dur;
  }

  spark(pos, vel, color, life = 0.4, spread = 10) {
    const i = this.pIdx++ % this.pN;
    this.pPos[i * 3] = pos.x; this.pPos[i * 3 + 1] = pos.y; this.pPos[i * 3 + 2] = pos.z;
    this.pVel[i * 3] = vel.x + (Math.random() - 0.5) * spread;
    this.pVel[i * 3 + 1] = vel.y + (Math.random() - 0.5) * spread;
    this.pVel[i * 3 + 2] = vel.z + (Math.random() - 0.5) * spread;
    this.pCol[i * 3] = color.r; this.pCol[i * 3 + 1] = color.g; this.pCol[i * 3 + 2] = color.b;
    this.pLife[i] = life;
  }

  burst(pos, color, n = 12, speed = 18, life = 0.5) {
    const col = new THREE.Color(color);
    for (let k = 0; k < n; k++) this.spark(pos, _b.set(0, speed * 0.3, 0), col, life * (0.5 + Math.random()), speed);
  }

  fireWeapon(owner, w, aimPoint) {
    const muzzle = owner.muzzleWorld(new THREE.Vector3());
    const dir = new THREE.Vector3().subVectors(aimPoint, muzzle).normalize();
    this.flash(muzzle, w.kind === 'rail' ? 0x88ddff : 0xffaa55, w.kind === 'rail' ? 60 : 20);
    this.world.audio?.shot(w.kind === 'rail' ? 'rail' : (w.pellets > 1 ? 'scatter' : 'rifle'), muzzle, owner.isPlayer);
    if (w.kind === 'rail') { this.rail(owner, w, muzzle, dir); return; }
    for (let p = 0; p < w.pellets; p++) {
      const d = dir.clone();
      d.x += (Math.random() - 0.5) * w.spread * 2;
      d.y += (Math.random() - 0.5) * w.spread * 2;
      d.z += (Math.random() - 0.5) * w.spread * 2;
      d.normalize();
      const mesh = new THREE.Mesh(this.bulletGeo, owner.isPlayer ? this.bulletMat : this.enemyBulletMat);
      mesh.position.copy(muzzle);
      mesh.quaternion.setFromUnitVectors(Z, d);
      this.scene.add(mesh);
      const vel = d.multiplyScalar(w.speed).add(owner.vel);
      this.bullets.push({ mesh, vel, owner, dmg: w.dmg, life: 1.4 });
    }
  }

  rail(owner, w, from, dir) {
    const end = from.clone();
    let hit = null;
    const step = 3;
    const p = new THREE.Vector3();
    for (let d = 0; d < 700; d += step) {
      p.copy(from).addScaledVector(dir, d);
      for (const a of this.world.actors) {
        if (a === owner || a.disabled > 0) continue;
        if (centerOf(a, _b).distanceTo(p) < 3.6) { hit = a; break; }
      }
      end.copy(p);
      if (hit || this.world.city.pointBlocked(p)) break;
    }
    if (hit) this.applyHit(hit, w.dmg, owner, end);
    else this.burst(end, '#88ddff', 16, 22);
    const len = from.distanceTo(end);
    const beam = new THREE.Mesh(new THREE.CylinderGeometry(0.25, 0.25, len, 6, 1, true),
      new THREE.MeshBasicMaterial({ color: new THREE.Color('#88ddff').multiplyScalar(4), transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false }));
    beam.position.copy(from).add(end).multiplyScalar(0.5);
    beam.quaternion.setFromUnitVectors(UP, dir);
    this.scene.add(beam);
    this.beams.push({ mesh: beam, life: 0.35 });
  }

  fireShoulder(owner, sp, target) {
    const base = new THREE.Vector3(owner.pos.x, owner.pos.y + 7, owner.pos.z);
    if (sp.kind === 'mine') {
      const back = new THREE.Vector3(-Math.sin(owner.yaw), 0, -Math.cos(owner.yaw));
      for (let k = 0; k < sp.count; k++) {
        const mesh = new THREE.Mesh(this.mineGeo, this.mineMat);
        const pos = owner.pos.clone().addScaledVector(back, 5 + k * 4);
        pos.x += (k - 1) * 3 * Math.cos(owner.yaw);
        pos.z -= (k - 1) * 3 * Math.sin(owner.yaw);
        pos.y = this.world.city.groundHeight(pos.x, pos.z, owner.pos.y + 1) + 0.2;
        mesh.position.copy(pos);
        this.scene.add(mesh);
        this.mines.push({ mesh, owner, dmg: sp.dmg, arm: 0.6, life: 40 });
      }
      this.world.audio?.shot('mine', base, owner.isPlayer);
      return;
    }
    const fwd = new THREE.Vector3(Math.sin(owner.yaw), 0, Math.cos(owner.yaw));
    for (let k = 0; k < sp.count; k++) {
      const mesh = new THREE.Mesh(this.missileGeo, this.missileMat);
      mesh.position.copy(base);
      const side = new THREE.Vector3(fwd.z, 0, -fwd.x).multiplyScalar((k - (sp.count - 1) / 2) * 0.8);
      const vel = fwd.clone().multiplyScalar(30).add(new THREE.Vector3(0, 25, 0)).add(side.multiplyScalar(12)).add(owner.vel);
      this.scene.add(mesh);
      this.missiles.push({ mesh, vel, owner, target, dmg: sp.dmg, life: 6, delay: k * 0.08, big: sp.count === 1 });
    }
    this.world.audio?.shot('missile', base, owner.isPlayer);
  }

  applyHit(target, dmg, owner, at) {
    target.damage(dmg, owner);
    this.burst(at, '#ffd27a', 8, 16, 0.35);
    this.world.onHit?.(target, dmg, owner);
  }

  explode(pos, dmg, radius, owner, color = '#ff8a3d') {
    this.flash(pos, 0xff7a30, 120, 0.25);
    this.burst(pos, color, 40, 30, 0.8);
    this.burst(pos, '#2a2a33', 16, 8, 1.2);
    const mesh = new THREE.Mesh(this.blastGeo, new THREE.MeshBasicMaterial({
      color: new THREE.Color(color).multiplyScalar(3), transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false,
    }));
    mesh.position.copy(pos);
    this.scene.add(mesh);
    this.blasts.push({ mesh, life: 0.4, radius });
    this.world.audio?.boom(pos);
    for (const a of this.world.actors) {
      if (a.disabled > 0) continue;
      const d = centerOf(a, _b).distanceTo(pos);
      if (d < radius) {
        const f = 1 - d / radius;
        this.applyHit(a, dmg * (0.4 + 0.6 * f), owner, pos);
        // shove
        const push = _c.subVectors(a.pos, pos).setY(0).normalize().multiplyScalar(18 * f);
        a.vel.add(push);
        a.vel.y += 8 * f;
      }
    }
  }

  update(dt) {
    const actors = this.world.actors;
    const city = this.world.city;
    // Bullets
    for (let i = this.bullets.length - 1; i >= 0; i--) {
      const b = this.bullets[i];
      const prev = _a.copy(b.mesh.position).clone();
      b.mesh.position.addScaledVector(b.vel, dt);
      b.life -= dt;
      let dead = b.life <= 0;
      if (!dead) {
        for (const a of actors) {
          if (a === b.owner || a.disabled > 0) continue;
          const c = centerOf(a, new THREE.Vector3());
          if (segDist(prev, b.mesh.position, c) < 3.2) {
            this.applyHit(a, b.dmg, b.owner, b.mesh.position);
            dead = true; break;
          }
        }
      }
      if (!dead && city.pointBlocked(b.mesh.position)) {
        this.burst(b.mesh.position, '#ffcf70', 5, 12, 0.25);
        dead = true;
      }
      if (dead) { this.scene.remove(b.mesh); this.bullets.splice(i, 1); }
    }
    // Missiles
    for (let i = this.missiles.length - 1; i >= 0; i--) {
      const m = this.missiles[i];
      m.life -= dt;
      m.delay -= dt;
      if (m.delay < 0) {
        if (m.target && m.target.disabled <= 0) {
          const want = centerOf(m.target, new THREE.Vector3()).sub(m.mesh.position).normalize().multiplyScalar(m.big ? 140 : 160);
          const turn = Math.min(1, dt * (m.life < 5.4 ? 3.2 : 0.8));
          m.vel.lerp(want, turn);
        } else {
          m.vel.multiplyScalar(1 + dt * 0.8);
        }
        const sp = m.vel.length();
        if (sp < 140) m.vel.multiplyScalar(1 + dt * 1.5);
      } else {
        m.vel.y -= 20 * dt;
      }
      m.mesh.position.addScaledVector(m.vel, dt);
      m.mesh.quaternion.setFromUnitVectors(Z, _b.copy(m.vel).normalize());
      this.spark(m.mesh.position, _b.set(0, 0, 0), new THREE.Color('#ff9a40'), 0.25, 2);
      this.spark(m.mesh.position, _b.set(0, 1, 0), new THREE.Color('#222230'), 0.7, 1);
      let boom = m.life <= 0 || city.pointBlocked(m.mesh.position);
      for (const a of actors) {
        if (boom) break;
        if (a === m.owner || a.disabled > 0) continue;
        if (centerOf(a, _b).distanceTo(m.mesh.position) < 4) boom = true;
      }
      if (boom) {
        this.explode(m.mesh.position.clone(), m.dmg, m.big ? 10 : 6, m.owner);
        this.scene.remove(m.mesh);
        this.missiles.splice(i, 1);
      }
    }
    // Mines
    for (let i = this.mines.length - 1; i >= 0; i--) {
      const m = this.mines[i];
      m.arm -= dt; m.life -= dt;
      m.mesh.material = this.mineMat;
      m.mesh.scale.setScalar(1 + Math.sin(performance.now() * 0.012) * 0.1);
      let boom = m.life <= 0;
      if (m.arm <= 0) {
        for (const a of actors) {
          if (a.disabled > 0) continue;
          if (a === m.owner && m.arm > -1.5) continue;
          if (a.pos.distanceTo(m.mesh.position) < 7) { boom = true; break; }
        }
      }
      if (boom) {
        this.explode(m.mesh.position.clone().setY(m.mesh.position.y + 2), m.dmg, 11, m.owner, '#ff3d3d');
        this.scene.remove(m.mesh);
        this.mines.splice(i, 1);
      }
    }
    // Beams / blasts
    for (let i = this.beams.length - 1; i >= 0; i--) {
      const b = this.beams[i];
      b.life -= dt;
      b.mesh.material.opacity = Math.max(0, b.life / 0.35);
      b.mesh.scale.x = b.mesh.scale.z = 1 + (0.35 - b.life) * 6;
      if (b.life <= 0) { this.scene.remove(b.mesh); b.mesh.geometry.dispose(); this.beams.splice(i, 1); }
    }
    for (let i = this.blasts.length - 1; i >= 0; i--) {
      const b = this.blasts[i];
      b.life -= dt;
      const t = 1 - b.life / 0.4;
      b.mesh.scale.setScalar(b.radius * (0.3 + t));
      b.mesh.material.opacity = Math.max(0, 1 - t);
      if (b.life <= 0) { this.scene.remove(b.mesh); b.mesh.material.dispose(); this.blasts.splice(i, 1); }
    }
    // Lights
    for (const L of this.lights) {
      if (L.t > 0) { L.t -= dt; L.light.intensity = L.max * Math.max(0, L.t / L.dur); }
      else L.light.intensity = 0;
    }
    // Particles
    for (let i = 0; i < this.pN; i++) {
      if (this.pLife[i] <= 0) continue;
      this.pLife[i] -= dt;
      const k = i * 3;
      this.pVel[k + 1] -= 9 * dt;
      this.pPos[k] += this.pVel[k] * dt;
      this.pPos[k + 1] += this.pVel[k + 1] * dt;
      this.pPos[k + 2] += this.pVel[k + 2] * dt;
      if (this.pLife[i] <= 0) { this.pPos[k + 1] = -1000; }
      else {
        const f = Math.min(1, this.pLife[i] * 3);
        this.pCol[k] *= 0.97 + 0.03 * f; this.pCol[k + 1] *= 0.95 + 0.05 * f; this.pCol[k + 2] *= 0.95 + 0.05 * f;
      }
    }
    this.points.geometry.attributes.position.needsUpdate = true;
    this.points.geometry.attributes.color.needsUpdate = true;
  }

  clear() {
    for (const list of [this.bullets, this.missiles, this.mines, this.beams, this.blasts]) {
      for (const o of list) this.scene.remove(o.mesh);
      list.length = 0;
    }
  }
}
