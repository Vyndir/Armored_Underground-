import * as THREE from 'three';

const _t = new THREE.Vector3();

// Rival racer brain: follows the gate list, manages boost, and fights.
export class RacerAI {
  constructor(actor, race, skill, lane) {
    this.actor = actor;
    this.race = race;
    this.skill = skill;
    this.lane = lane;
    this.burst = 0;
    this.pause = 1 + Math.random() * 2;
    this.stuck = 0;
    this.qbTimer = 2 + Math.random() * 3;
    this.aimErr = new THREE.Vector3();
  }

  update(dt) {
    const a = this.actor;
    const c = a.controls;
    const race = this.race;
    const r = this.racer;
    if (race.state !== 'running' || a.disabled > 0) return;
    if (r.finished) {
      // cruise to a stop past the line
      c.move.set(0, 0, 0); c.boost = false; c.fire = c.fire2 = false;
      return;
    }

    // ---- Navigation ----
    const gates = race.gates;
    const g = gates[r.next];
    const prev = r.next === 0 ? race.startPos : gates[r.next - 1];
    const seg = _t.subVectors(g, prev).setY(0).normalize();
    const right = new THREE.Vector3(seg.z, 0, -seg.x);
    // Follow the street: project onto the current leg (offset by our lane) and
    // chase a point a little further along it. When we have drifted off the
    // leg (overshot a corner), head straight back to it first.
    const segLen = prev.distanceTo(g);
    const rel = new THREE.Vector3().subVectors(a.pos, prev).setY(0);
    const along = THREE.MathUtils.clamp(rel.dot(seg), 0, segLen);
    const lateral = rel.dot(right) - this.lane;
    const look = Math.max(0, 34 - Math.abs(lateral) * 2.5);
    const aimAlong = Math.min(segLen, along + look);
    const target = prev.clone().addScaledVector(seg, aimAlong).addScaledVector(right, this.lane);
    const toT = target.sub(a.pos).setY(0);
    const dist = Math.hypot(g.x - a.pos.x, g.z - a.pos.z);
    const after = gates[r.next + 1];
    let corner = 0;
    if (after) {
      const seg2 = new THREE.Vector3().subVectors(after, g).setY(0).normalize();
      corner = Math.acos(THREE.MathUtils.clamp(seg.dot(seg2), -1, 1));
    }
    const dir = toT.lengthSq() > 0.01 ? toT.normalize() : seg.clone();

    // Brake for corners: v^2 = vc^2 + 2ad
    const cornerSpeed = corner > 0.3 ? 30 + (1 - corner / Math.PI) * 30 : 999;
    const brakeDist = Math.max(0, (a.speed * a.speed - cornerSpeed * cornerSpeed) / (2 * 55)) + 12;
    const braking = dist < brakeDist && a.speed > cornerSpeed;
    c.move.copy(dir).multiplyScalar(braking ? 0.3 : 1);
    if (braking) {
      // bleed speed by steering against current velocity a bit
      const vdir = a.vel.clone().setY(0).normalize();
      c.move.addScaledVector(vdir, -0.6);
    }
    const enFrac = a.en / a.stats.enCap;
    c.boost = !braking && (a.boosting ? enFrac > 0.05 : enFrac > 0.35);
    c.face = Math.atan2(a.vel.x || dir.x, a.vel.z || dir.z);

    // occasional quick boost on long straights
    this.qbTimer -= dt;
    if (this.qbTimer <= 0 && !braking && dist > 120 && enFrac > 0.6) {
      c.quickBoost = true;
      this.qbTimer = 3 + Math.random() * 4 / this.skill;
    }

    // Unstick
    if (a.speed < 4) this.stuck += dt; else this.stuck = 0;
    if (this.stuck > 1.5) {
      c.jumpPressed = true;
      c.jump = true;
      c.quickBoost = true;
      c.move.copy(dir);
      this.stuck = 0;
    } else c.jump = false;

    // ---- Combat ----
    let best = null, bestD = 170;
    for (const o of race.racers) {
      const oa = o.actor;
      if (oa === a || oa.disabled > 0) continue;
      const d = oa.pos.distanceTo(a.pos);
      // prefer the player a bit (they are the challenger)
      const score = d * (oa.isPlayer ? 0.7 : 1);
      if (score < bestD) {
        const fwd = new THREE.Vector3(Math.sin(a.yaw), 0, Math.cos(a.yaw));
        const to = oa.pos.clone().sub(a.pos).setY(0).normalize();
        if (fwd.dot(to) > 0.3) { best = oa; bestD = score; }
      }
    }
    c.target = best;
    c.fire = false; c.fire2 = false;
    if (best) {
      const from = a.pos.clone().setY(a.pos.y + 5);
      const to = best.pos.clone().setY(best.pos.y + 4);
      if (!race.world.city.lineBlocked(from, to, 6)) {
        if (this.burst > 0) {
          this.burst -= dt;
          // lead the target, with skill-based error that drifts over time
          const w = a.stats.parts.weapon;
          const tFlight = from.distanceTo(to) / w.speed;
          const err = 3.5 / this.skill;
          this.aimErr.lerp(new THREE.Vector3((Math.random() - 0.5) * err * 2, (Math.random() - 0.5) * err, (Math.random() - 0.5) * err * 2), dt * 2);
          c.aimPoint.copy(to).addScaledVector(best.vel, tFlight * 0.8).add(this.aimErr);
          c.fire = true;
          if (this.burst <= 0) this.pause = (1.0 + Math.random() * 1.8) / this.skill;
        } else {
          this.pause -= dt;
          if (this.pause <= 0) this.burst = 0.8 + Math.random() * 1.2;
        }
        if (Math.random() < dt * 0.25 * this.skill && a.stats.parts.shoulder.kind === 'missile') c.fire2 = true;
      }
      const dy = best.pos.y + 4 - (a.pos.y + 5);
      c.aimPitch = Math.atan2(dy, Math.max(1, Math.hypot(best.pos.x - a.pos.x, best.pos.z - a.pos.z)));
    } else {
      c.aimPitch = 0;
    }
    // Mine droppers lay mines when someone is right behind
    if (a.stats.parts.shoulder.kind === 'mine') {
      for (const o of race.racers) {
        const oa = o.actor;
        if (oa === a) continue;
        const back = new THREE.Vector3(-Math.sin(a.yaw), 0, -Math.cos(a.yaw));
        const to = oa.pos.clone().sub(a.pos).setY(0);
        if (to.length() < 40 && back.dot(to.normalize()) > 0.7 && Math.random() < dt * 0.8) c.fire2 = true;
      }
    }
  }
}
