import * as THREE from 'three';
import { computeStats } from './parts.js';
import { MechVisual } from './mech.js';

const GRAVITY = 34;
const RADIUS = 2.6;
const _v = new THREE.Vector3();
const _d = new THREE.Vector3();

export function makeControls() {
  return {
    move: new THREE.Vector3(),  // desired world-space direction (XZ), length 0..1
    face: 0,                    // desired facing yaw
    aimPitch: 0,
    boost: false,
    jump: false,
    jumpPressed: false,
    quickBoost: false,
    fire: false,
    fire2: false,
    reload: false,
    aimPoint: new THREE.Vector3(),
    target: null,
  };
}

// A mech in the world: physics, energy, armor and weapon state.
// Driven by a controller (player or AI) that fills `controls`.
export class MechActor {
  constructor({ name, loadout, paint, isPlayer = false, scene, statScale = 1 }) {
    this.name = name;
    this.isPlayer = isPlayer;
    this.scene = scene;
    this.statScale = statScale;
    this.pos = new THREE.Vector3();
    this.vel = new THREE.Vector3();
    this.yaw = 0;
    this.controls = makeControls();
    this.visual = new MechVisual(loadout, paint);
    scene.add(this.visual.root);
    this.setLoadout(loadout, paint);
    this.grounded = true;
    this.boostLevel = 0;
    this.disabled = 0;
    this.hitFlash = 0;
    this.lastHitBy = null;
    this.impact = 0;
  }

  setLoadout(loadout, paint) {
    this.loadout = { ...loadout };
    this.paint = { ...paint };
    this.stats = computeStats(loadout);
    if (this.statScale !== 1) {
      this.stats.groundSpeed *= this.statScale;
      this.stats.boostSpeed *= this.statScale;
      this.stats.accel *= this.statScale;
    }
    this.visual.build(loadout, paint);
    this.repair();
  }

  repair() {
    this.ap = this.stats.ap;
    this.en = this.stats.enCap;
    this.enLockout = 0;
    this.enDelay = 0;
    this.overheated = false;
    const w = this.stats.parts.weapon;
    this.ammo = w.mag;
    this.reloading = 0;
    this.fireCd = 0;
    this.shoulderCd = 0;
    this.qbCd = 0;
    this.disabled = 0;
  }

  get center() { return _v.copy(this.pos).setY(this.pos.y + 4); }
  get speed() { return Math.hypot(this.vel.x, this.vel.z); }

  place(pos, yaw) {
    this.pos.copy(pos);
    this.vel.set(0, 0, 0);
    this.yaw = yaw;
    this.controls.face = yaw;
    this.visual.root.position.copy(pos);
    this.visual.root.rotation.y = yaw;
  }

  damage(amount, from) {
    if (this.disabled > 0) return;
    this.ap -= amount;
    this.hitFlash = 0.15;
    this.lastHitBy = from;
    if (this.ap <= 0) {
      this.ap = 0;
      this.disabled = 3.0;
      this.onDisabled?.(from);
    }
  }

  update(dt, world) {
    const c = this.controls;
    const st = this.stats;
    this.hitFlash = Math.max(0, this.hitFlash - dt);
    this.impact = 0;

    if (this.disabled > 0) {
      this.disabled -= dt;
      // coast to a stop
      this.vel.x *= Math.max(0, 1 - dt * 2);
      this.vel.z *= Math.max(0, 1 - dt * 2);
      c.boost = c.fire = c.fire2 = c.quickBoost = c.jump = false;
      c.move.set(0, 0, 0);
      if (this.disabled <= 0) this.onRecovered?.();
    }

    // ---- Energy ----
    const wantsBoost = c.boost && !this.overheated && c.move.lengthSq() > 0.01;
    const hovering = c.jump && !this.grounded && !this.overheated && this.vel.y < 8;
    let drain = 0;
    if (wantsBoost) drain += st.boostDrain;
    if (hovering) drain += st.boostDrain * 0.8;
    if (drain > 0) {
      this.en -= drain * dt;
      this.enDelay = 0.5;
    } else {
      this.enDelay -= dt;
      if (this.enDelay <= 0) this.en += st.enRegen * dt * (this.overheated ? 0.7 : 1);
    }
    if (this.en <= 0) { this.en = 0; this.overheated = true; }
    if (this.overheated && this.en >= st.enCap * 0.4) this.overheated = false;
    this.en = Math.min(this.en, st.enCap);
    this.boostLevel = THREE.MathUtils.lerp(this.boostLevel, wantsBoost ? 1 : (hovering ? 0.6 : 0.08), Math.min(1, dt * 8));

    // ---- Horizontal movement ----
    const maxSpeed = wantsBoost ? st.boostSpeed : st.groundSpeed;
    const target = _d.copy(c.move).setY(0);
    if (target.lengthSq() > 1) target.normalize();
    target.multiplyScalar(maxSpeed);
    const hv = new THREE.Vector3(this.vel.x, 0, this.vel.z);
    const diff = target.sub(hv);
    let accel;
    if (c.move.lengthSq() < 0.01) {
      // no input: coast (boost glide) or brake
      accel = this.grounded ? (this.boostLevel > 0.3 ? 10 : 34) : 4;
    } else {
      accel = st.accel * (wantsBoost ? 1.5 : 1) * (this.grounded ? st.grip : 0.55);
      // over max speed (after quick boost): bleed off gently instead of snapping
      if (hv.length() > maxSpeed + 2) accel *= 0.5;
    }
    const dl = diff.length();
    const step = accel * dt;
    if (dl > step) diff.multiplyScalar(step / dl);
    this.vel.x += diff.x;
    this.vel.z += diff.z;

    // ---- Quick boost ----
    this.qbCd -= dt;
    if (c.quickBoost && this.qbCd <= 0 && this.en > st.qbCost * 0.5) {
      const dir = c.move.lengthSq() > 0.01 ? c.move.clone().setY(0).normalize() : new THREE.Vector3(Math.sin(this.yaw), 0, Math.cos(this.yaw));
      this.vel.x += dir.x * st.qbForce;
      this.vel.z += dir.z * st.qbForce;
      this.en -= st.qbCost;
      this.enDelay = 0.6;
      this.qbCd = 0.45;
      this.onQuickBoost?.();
    }
    c.quickBoost = false;

    // ---- Vertical ----
    if (c.jumpPressed && this.grounded && !this.overheated) {
      this.vel.y = st.jump;
      this.grounded = false;
    }
    c.jumpPressed = false;
    if (hovering) this.vel.y += (GRAVITY + 10 - Math.max(0, this.vel.y) * 2) * dt;
    this.vel.y -= GRAVITY * dt;

    // ---- Integrate + collide ----
    const preSpeed = this.speed;
    this.pos.addScaledVector(this.vel, dt);
    const n = world.city.collide(this.pos, RADIUS);
    if (n) {
      const into = this.vel.x * n.x + this.vel.z * n.z;
      if (into < 0) {
        this.vel.x -= n.x * into * 1.15;
        this.vel.z -= n.z * into * 1.15;
        // scrape: lose some speed
        this.vel.x *= 0.92; this.vel.z *= 0.92;
        const hit = -into;
        if (hit > 22) {
          this.damage((hit - 22) * 9, null);
          this.impact = hit;
        }
      }
    }
    const gy = world.city.groundHeight(this.pos.x, this.pos.z, this.pos.y);
    if (this.pos.y <= gy) {
      if (!this.grounded && this.vel.y < -25) this.impact = Math.max(this.impact, -this.vel.y * 0.5);
      this.pos.y = gy;
      this.vel.y = Math.max(0, this.vel.y);
      this.grounded = true;
    } else if (this.pos.y > gy + 0.3) {
      this.grounded = false;
    }
    this.height = this.pos.y - gy;

    // ---- Facing ----
    let dy = c.face - this.yaw;
    dy = Math.atan2(Math.sin(dy), Math.cos(dy));
    this.yaw += dy * Math.min(1, dt * 10);

    // ---- Weapons ----
    this.fireCd -= dt;
    this.shoulderCd -= dt;
    const w = st.parts.weapon;
    if (this.reloading > 0) {
      this.reloading -= dt;
      if (this.reloading <= 0) this.ammo = w.mag;
    } else if ((c.reload && this.ammo < w.mag) || this.ammo <= 0) {
      this.reloading = w.reload;
    }
    if (c.fire && this.fireCd <= 0 && this.ammo > 0 && this.reloading <= 0 && this.disabled <= 0) {
      this.fireCd = 1 / (w.rate * st.fireRateMult);
      this.ammo--;
      world.weapons.fireWeapon(this, w, c.aimPoint);
    }
    const sp = st.parts.shoulder;
    if (c.fire2 && this.shoulderCd <= 0 && this.disabled <= 0) {
      if (sp.kind === 'mine' || c.target) {
        this.shoulderCd = sp.cooldown;
        world.weapons.fireShoulder(this, sp, c.target);
      }
    }

    // ---- Visual ----
    const vr = this.visual.root;
    vr.position.copy(this.pos);
    vr.rotation.y = this.yaw;
    const local = this.vel.clone().applyAxisAngle(new THREE.Vector3(0, 1, 0), -this.yaw);
    this.visual.animate({
      dt, speed: this.speed, localVel: local, grounded: this.grounded,
      boosting: wantsBoost, boostLevel: this.boostLevel, aimPitch: c.aimPitch,
      disabled: this.disabled > 0, height: this.height,
    });
    this.boosting = wantsBoost;
    this.preSpeed = preSpeed;
  }

  muzzleWorld(out) {
    this.visual.root.updateMatrixWorld(true);
    return this.visual.muzzle.getWorldPosition(out);
  }

  remove() {
    this.scene.remove(this.visual.root);
    this.visual.dispose();
  }
}

export const MECH_RADIUS = RADIUS;
