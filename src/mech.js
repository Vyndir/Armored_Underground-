import * as THREE from 'three';
import { getPart } from './parts.js';

// Procedural mech model assembled from the equipped parts.
// The root sits at the feet; +Z is forward.

function box(w, h, d, mat, x = 0, y = 0, z = 0) {
  const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat);
  m.position.set(x, y, z);
  m.castShadow = false;
  return m;
}

export class MechVisual {
  constructor(loadout, paint) {
    this.root = new THREE.Group();
    this.walkPhase = 0;
    this.lean = new THREE.Vector2();
    this.build(loadout, paint);
  }

  build(loadout, paint) {
    for (const c of [...this.root.children]) this.root.remove(c);
    this.loadout = { ...loadout };
    const P = slot => getPart(slot, loadout[slot]);
    const primary = new THREE.MeshStandardMaterial({ color: paint.primary, roughness: 0.45, metalness: 0.65 });
    const secondary = new THREE.MeshStandardMaterial({ color: paint.secondary, roughness: 0.5, metalness: 0.5 });
    const dark = new THREE.MeshStandardMaterial({ color: 0x15171c, roughness: 0.7, metalness: 0.6 });
    const neonColor = new THREE.Color(paint.neon);
    const neon = new THREE.MeshBasicMaterial({ color: neonColor.clone().multiplyScalar(2.5), toneMapped: false });
    this.neonColor = neonColor;
    this.mats = [primary, secondary, dark, neon];

    const legs = P('legs');
    const hipY = { biped: 3.6, reverse: 3.9, hover: 2.6, heavy: 3.2 }[legs.type];
    this.hipY = hipY;

    // ---- Legs ----
    this.legs = [];
    const legGroup = new THREE.Group();
    this.root.add(legGroup);
    for (const side of [-1, 1]) {
      const hip = new THREE.Group();
      hip.position.set(side * 1.0, hipY, 0);
      legGroup.add(hip);
      const knee = new THREE.Group();
      let footY;
      if (legs.type === 'biped') {
        hip.add(box(0.9, 1.9, 1.0, primary, 0, -0.9, 0.1));
        hip.add(box(0.95, 0.3, 1.05, neon, 0, -0.4, 0.1));
        knee.position.set(0, -1.8, 0.15);
        knee.add(box(0.8, 1.9, 0.9, secondary, 0, -0.9, -0.05));
        knee.add(box(1.0, 0.35, 1.9, dark, 0, -1.75, 0.35));
        footY = -1.8;
      } else if (legs.type === 'reverse') {
        hip.add(box(0.8, 0.8, 2.0, primary, 0, -0.5, 0.6));
        knee.position.set(0, -0.7, 1.4);
        knee.add(box(0.7, 2.3, 0.7, secondary, 0, -1.0, -0.8).rotateX(-0.6));
        knee.add(box(0.75, 1.3, 0.6, dark, 0, -2.6, -1.3));
        knee.add(box(1.1, 0.3, 2.0, dark, 0, -3.2, -1.0));
        knee.add(box(0.8, 0.2, 0.2, neon, 0, -1.0, -0.4).rotateX(-0.6));
        footY = -3.2;
      } else if (legs.type === 'hover') {
        hip.add(box(1.1, 1.2, 1.4, primary, 0, -0.5, 0));
        knee.position.set(0, -1.2, 0);
        knee.add(box(1.4, 0.9, 2.4, secondary, 0, -0.3, 0));
        const pad = new THREE.Mesh(new THREE.CylinderGeometry(1.0, 1.2, 0.2, 16), neon);
        pad.position.set(0, -0.85, 0);
        knee.add(pad);
        footY = -0.9;
      } else {
        hip.add(box(1.4, 1.6, 1.5, primary, 0, -0.7, 0));
        knee.position.set(0, -1.5, 0);
        knee.add(box(1.5, 1.4, 1.6, secondary, 0, -0.6, 0));
        knee.add(box(1.8, 0.5, 2.8, dark, 0, -1.4, 0.2));
        knee.add(box(1.85, 0.15, 2.85, neon, 0, -1.15, 0.2));
        footY = -1.65;
      }
      hip.add(knee);
      this.legs.push({ hip, knee, side, footY });
    }

    // ---- Torso / core ----
    const torso = new THREE.Group();
    torso.position.y = hipY + 0.2;
    this.root.add(torso);
    this.torso = torso;
    const core = P('core');
    torso.add(box(1.6, 0.8, 1.2, dark, 0, 0.3, 0)); // waist
    if (core.id === 'cr_wasp') {
      torso.add(box(2.4, 1.6, 1.8, primary, 0, 1.4, 0.1));
      torso.add(box(1.6, 0.8, 1.2, secondary, 0, 2.3, 0.3));
    } else if (core.id === 'cr_anvil') {
      torso.add(box(3.4, 2.2, 2.4, primary, 0, 1.6, 0));
      torso.add(box(3.0, 0.5, 2.2, secondary, 0, 2.9, 0));
    } else {
      torso.add(box(2.8, 1.9, 2.0, primary, 0, 1.5, 0));
      torso.add(box(2.0, 0.5, 1.6, secondary, 0, 2.6, 0.1));
    }
    torso.add(box(1.4, 0.15, 0.1, neon, 0, 1.3, 1.25));

    // ---- Head ----
    const head = P('head');
    const hg = new THREE.Group();
    hg.position.set(0, 3.0, 0.2);
    torso.add(hg);
    if (head.id === 'hd_moth') {
      hg.add(box(0.8, 0.5, 1.1, secondary, 0, 0.3, 0));
      hg.add(box(1.8, 0.08, 0.5, primary, 0, 0.45, -0.1)); // antenna wings
      hg.add(box(0.6, 0.12, 0.05, neon, 0, 0.32, 0.56));
    } else if (head.id === 'hd_bastion') {
      hg.add(box(1.3, 0.9, 1.3, primary, 0, 0.45, 0));
      hg.add(box(1.0, 0.1, 0.05, neon, 0, 0.5, 0.66));
    } else {
      hg.add(box(1.0, 0.7, 1.2, secondary, 0, 0.35, 0));
      hg.add(box(0.2, 0.9, 0.2, dark, 0.35, 0.8, -0.3));
      hg.add(box(0.7, 0.14, 0.05, neon, 0, 0.4, 0.61));
    }

    // ---- Arms ----
    const arms = P('arms');
    const armThick = arms.id === 'ar_hauler' ? 1.15 : arms.id === 'ar_needle' ? 0.7 : 0.9;
    this.arms = [];
    for (const side of [-1, 1]) {
      const sh = new THREE.Group();
      const sx = (core.id === 'cr_anvil' ? 2.2 : core.id === 'cr_wasp' ? 1.7 : 1.9) * side;
      sh.position.set(sx, 2.0, 0);
      torso.add(sh);
      sh.add(box(1.2, 0.9, 1.3, primary, side * 0.2, 0.2, 0)); // pauldron
      sh.add(box(armThick, 1.4, armThick, secondary, side * 0.25, -0.7, 0));
      const fore = new THREE.Group();
      fore.position.set(side * 0.25, -1.4, 0);
      fore.add(box(armThick * 1.05, 1.4, armThick * 1.1, primary, 0, -0.5, 0.2));
      sh.add(fore);
      this.arms.push({ sh, fore, side });
    }
    // Weapon in right hand
    const wp = P('weapon');
    const right = this.arms[1].fore;
    const gun = new THREE.Group();
    gun.position.set(0, -1.0, 0);
    gun.rotation.x = Math.PI / 2;
    right.add(gun);
    const glen = { wp_hornet: 2.6, wp_gale: 1.7, wp_maul: 2.2, wp_lance: 4.0 }[wp.id] || 2.4;
    gun.add(box(0.45, 0.6, glen, dark, 0, 0, glen / 2 - 0.3));
    gun.add(box(0.2, 0.2, 0.6, secondary, 0, 0.05, glen - 0.1));
    if (wp.id === 'wp_lance') gun.add(box(0.5, 0.1, glen * 0.8, neon, 0, 0.35, glen / 2));
    this.muzzle = new THREE.Object3D();
    this.muzzle.position.set(0, 0, glen + 0.3);
    gun.add(this.muzzle);
    this.gun = gun;

    // Shoulder unit on left shoulder
    const shp = P('shoulder');
    const pod = new THREE.Group();
    pod.position.set(-1.2, 3.1, -0.4);
    torso.add(pod);
    if (shp.id === 'sh_mines') {
      pod.add(box(1.0, 0.8, 1.6, secondary, 0, 0, -0.2));
      pod.add(box(0.6, 0.6, 0.1, neon, 0, 0, -1.02));
    } else {
      const big = shp.id === 'sh_spear';
      pod.add(box(big ? 1.0 : 1.3, big ? 1.0 : 0.9, big ? 2.4 : 1.6, secondary, 0, 0, 0));
      pod.add(box(big ? 0.5 : 1.0, big ? 0.5 : 0.5, 0.1, neon, 0, 0, big ? 1.22 : 0.82));
    }
    this.pod = pod;

    // Back booster with flames
    const bs = P('booster');
    const pack = new THREE.Group();
    pack.position.set(0, 1.6, -1.2);
    torso.add(pack);
    pack.add(box(2.0, 1.4, 0.8, dark, 0, 0, -0.1));
    this.flames = [];
    const flameMat = new THREE.MeshBasicMaterial({ color: neonColor.clone().lerp(new THREE.Color(1, 1, 1), 0.35).multiplyScalar(1.4), transparent: true, opacity: 0.7, blending: THREE.AdditiveBlending, depthWrite: false, toneMapped: false });
    const nozzles = bs.id === 'bs_comet' ? [-0.6, 0, 0.6] : [-0.5, 0.5];
    for (const nx of nozzles) {
      const noz = new THREE.Mesh(new THREE.CylinderGeometry(0.28, 0.4, 0.6, 10), secondary);
      noz.rotation.x = Math.PI / 2;
      noz.position.set(nx, -0.1, -0.7);
      pack.add(noz);
      const fl = new THREE.Mesh(new THREE.ConeGeometry(0.32, 2.4, 10, 1, true), flameMat);
      fl.rotation.x = -Math.PI / 2;
      fl.position.set(nx, -0.1, -2.1);
      pack.add(fl);
      this.flames.push(fl);
    }
    this.flameMat = flameMat;

    // Glow sprite under the mech (boost wash on the asphalt)
    if (!this.wash) {
      const c = document.createElement('canvas'); c.width = c.height = 64;
      const g = c.getContext('2d');
      const grad = g.createRadialGradient(32, 32, 0, 32, 32, 32);
      grad.addColorStop(0, 'rgba(255,255,255,0.9)'); grad.addColorStop(1, 'rgba(255,255,255,0)');
      g.fillStyle = grad; g.fillRect(0, 0, 64, 64);
      this.washTex = new THREE.CanvasTexture(c);
    }
    const washGeo = new THREE.PlaneGeometry(9, 9); washGeo.rotateX(-Math.PI / 2);
    this.wash = new THREE.Mesh(washGeo, new THREE.MeshBasicMaterial({ map: this.washTex, color: neonColor, transparent: true, opacity: 0.4, blending: THREE.AdditiveBlending, depthWrite: false }));
    this.wash.position.y = 0.08;
    this.root.add(this.wash);
  }

  setVisible(v) { this.root.visible = v; }

  // state: { speed, localVel (Vector3 in mech frame), grounded, boosting, boostLevel, aimPitch, disabled, dt }
  animate(s) {
    const dt = s.dt;
    const groundedWalk = s.grounded && !s.boosting && s.speed > 1;
    this.walkPhase += dt * (groundedWalk ? Math.min(s.speed, 30) * 0.32 : 0);
    // lean toward motion (skating pose)
    const tl = new THREE.Vector2(
      THREE.MathUtils.clamp(s.localVel.z / 80, -0.35, 0.35),
      THREE.MathUtils.clamp(-s.localVel.x / 90, -0.3, 0.3),
    );
    this.lean.lerp(tl, Math.min(1, dt * 6));
    this.torso.rotation.x = this.lean.x * (s.boosting ? 1 : 0.5);
    this.torso.rotation.z = this.lean.y;

    const isHover = this.loadout.legs === 'lg_glide';
    for (const L of this.legs) {
      let hipRot = 0, kneeRot = 0;
      if (s.disabled) { hipRot = -0.6; kneeRot = 1.1; }
      else if (!s.grounded) { hipRot = -0.5; kneeRot = 0.9; }
      else if (groundedWalk && !isHover) {
        const ph = this.walkPhase + (L.side > 0 ? Math.PI : 0);
        hipRot = Math.sin(ph) * 0.5;
        kneeRot = Math.max(0, -Math.cos(ph)) * 0.7;
      } else if (s.boosting) { hipRot = -0.25 + this.lean.x; kneeRot = 0.45; }
      L.hip.rotation.x = THREE.MathUtils.lerp(L.hip.rotation.x, hipRot, Math.min(1, dt * 12));
      L.knee.rotation.x = THREE.MathUtils.lerp(L.knee.rotation.x, kneeRot, Math.min(1, dt * 12));
      L.hip.rotation.z = this.lean.y * 0.6;
    }
    // Bob while walking
    const bob = groundedWalk && !isHover ? Math.abs(Math.sin(this.walkPhase)) * 0.15 : 0;
    this.torso.position.y = this.hipY + 0.2 - bob - (s.disabled ? 1.0 : 0);
    // Gun arm points along aim pitch
    this.arms[1].sh.rotation.x = -Math.PI / 2 - s.aimPitch;
    this.arms[0].sh.rotation.x = s.boosting ? 0.5 : 0.1;

    const lvl = s.boostLevel;
    for (const f of this.flames) {
      const flick = 0.85 + Math.random() * 0.3;
      f.scale.set(0.6 + lvl * 0.6, 0.25 + lvl * 1.4 * flick, 0.6 + lvl * 0.6);
      f.position.z = -0.9 - (0.25 + lvl * 1.4 * flick) * 1.2;
    }
    this.flameMat.opacity = 0.2 + lvl * 0.5;
    this.wash.material.opacity = s.grounded ? 0.15 + lvl * 0.5 : Math.max(0, 0.3 - s.height * 0.03);
    this.wash.position.y = 0.08 - s.height;
    this.wash.visible = s.height < 12;
  }

  dispose() {
    this.root.traverse(o => { if (o.geometry) o.geometry.dispose(); });
    for (const m of this.mats) m.dispose();
  }
}
