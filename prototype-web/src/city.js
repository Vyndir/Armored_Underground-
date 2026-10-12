import * as THREE from 'three';

// Procedural night city on a Manhattan grid. Roads run along grid lines,
// buildings fill the blocks between them.

export const GRID = 10;          // blocks per side
export const ROAD = 26;          // road width
export const PITCH = 110;        // road-center to road-center
export const BLOCK = PITCH - ROAD;
export const HALF = (GRID * PITCH) / 2;

export const roadCoord = i => -HALF + i * PITCH;
export const node = (i, j) => new THREE.Vector3(roadCoord(i), 0, roadCoord(j));

// Block that holds the garage (its door faces the road at j = GARAGE_BLOCK[1]).
export const GARAGE_BLOCK = [4, 4];
export const GARAGE_DOOR = new THREE.Vector3(roadCoord(4) + PITCH / 2, 0, roadCoord(4));

function mulberry32(a) {
  return function () {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const SIGN_WORDS = ['KAIJU RAMEN', 'VOLTLINE', 'HEXA//CORP', 'NIGHTSHIFT', 'ZERO-G', 'BLUE LOTUS', 'NEON 9', 'OKAMI', 'MIDNIGHT OIL', 'AFTERBURN', 'SKYHOOK', 'PARTS & SALVAGE', 'GRIDLOCK', 'KOI CLUB', 'OVERCLOCK', 'RUST BELT', 'SHOGUN BAR', 'ION WASH'];
const NEON_COLORS = ['#ff2bd6', '#00e5ff', '#ffb300', '#7c4dff', '#39ff14', '#ff3d3d', '#2979ff'];

function windowTexture(rand, tint) {
  const c = document.createElement('canvas');
  c.width = 256; c.height = 256;
  const g = c.getContext('2d');
  g.fillStyle = '#05070c';
  g.fillRect(0, 0, 256, 256);
  const cols = 16, rows = 16;
  for (let y = 0; y < rows; y++) {
    // whole floors tend to be lit or dark together
    const floorLit = rand() < 0.45 ? 0.6 : 0.1;
    for (let x = 0; x < cols; x++) {
      const lit = rand() < floorLit;
      if (lit) {
        const warm = rand() < 0.75;
        const b = 0.35 + rand() * 0.5;
        g.fillStyle = warm ? `rgba(255,${Math.floor(200 * b + 30)},${Math.floor(120 * b)},${b})` : tint;
      } else {
        g.fillStyle = '#0a0e16';
      }
      g.fillRect(x * 16 + 3, y * 16 + 5, 10, 8);
    }
  }
  const t = new THREE.CanvasTexture(c);
  t.wrapS = t.wrapT = THREE.RepeatWrapping;
  t.colorSpace = THREE.SRGBColorSpace;
  t.anisotropy = 4;
  return t;
}

function signTexture(text, color) {
  const c = document.createElement('canvas');
  c.width = 512; c.height = 128;
  const g = c.getContext('2d');
  g.fillStyle = 'rgba(0,0,0,0.85)';
  g.fillRect(0, 0, 512, 128);
  g.strokeStyle = color;
  g.lineWidth = 6;
  g.strokeRect(6, 6, 500, 116);
  g.font = 'bold 64px "Arial Black", Impact, sans-serif';
  g.textAlign = 'center';
  g.textBaseline = 'middle';
  g.shadowColor = color;
  g.shadowBlur = 18;
  g.fillStyle = color;
  g.fillText(text, 256, 68, 470);
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

function glowTexture() {
  const c = document.createElement('canvas');
  c.width = c.height = 64;
  const g = c.getContext('2d');
  const grad = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  grad.addColorStop(0, 'rgba(255,255,255,1)');
  grad.addColorStop(0.4, 'rgba(255,255,255,0.35)');
  grad.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = grad;
  g.fillRect(0, 0, 64, 64);
  return new THREE.CanvasTexture(c);
}

export const GLOW_TEX = glowTexture();

// Scale the UVs of a box so window textures tile at a fixed world size.
function tileBoxUVs(geo, w, h, d) {
  const uv = geo.attributes.uv;
  // face order: +x, -x, +y, -y, +z, -z (4 verts each)
  const sizes = [[d, h], [d, h], [w, d], [w, d], [w, h], [w, h]];
  for (let f = 0; f < 6; f++) {
    const [su, sv] = sizes[f];
    for (let k = 0; k < 4; k++) {
      const i = f * 4 + k;
      uv.setXY(i, uv.getX(i) * su / 40, uv.getY(i) * sv / 40);
    }
  }
  uv.needsUpdate = true;
}

export class City {
  constructor(scene) {
    this.scene = scene;
    this.group = new THREE.Group();
    scene.add(this.group);
    this.buildings = [];             // AABBs { minX, maxX, minZ, maxZ, top }
    this.blockIndex = new Map();     // "i,j" -> buildings in that block
    const rand = mulberry32(1337);
    this.rand = rand;

    this._ground();
    this._buildings(rand);
    this._roadDetails(rand);
    this._boundary();
  }

  _ground() {
    const size = HALF * 2 + 400;
    const asphalt = new THREE.MeshStandardMaterial({ color: 0x14161c, roughness: 0.32, metalness: 0.35 });
    const ground = new THREE.Mesh(new THREE.PlaneGeometry(size, size), asphalt);
    ground.rotation.x = -Math.PI / 2;
    this.group.add(ground);

    // Raised sidewalks per block
    const walkMat = new THREE.MeshStandardMaterial({ color: 0x23262e, roughness: 0.8 });
    const walkGeo = new THREE.BoxGeometry(BLOCK, 0.3, BLOCK);
    const walks = new THREE.InstancedMesh(walkGeo, walkMat, GRID * GRID);
    const m = new THREE.Matrix4();
    let n = 0;
    for (let i = 0; i < GRID; i++) for (let j = 0; j < GRID; j++) {
      m.makeTranslation(roadCoord(i) + PITCH / 2, 0.15, roadCoord(j) + PITCH / 2);
      walks.setMatrixAt(n++, m);
    }
    this.group.add(walks);

    // Dashed center lines
    const dashGeo = new THREE.BoxGeometry(0.4, 0.05, 5);
    const dashMat = new THREE.MeshBasicMaterial({ color: 0xc8b46a });
    const perRoad = Math.floor((HALF * 2) / 12);
    const dashes = new THREE.InstancedMesh(dashGeo, dashMat, (GRID + 1) * perRoad * 2);
    n = 0;
    const q = new THREE.Quaternion();
    const yRot = new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), Math.PI / 2);
    const s = new THREE.Vector3(1, 1, 1);
    for (let r = 0; r <= GRID; r++) {
      for (let k = 0; k < perRoad; k++) {
        const t = -HALF + k * 12 + 6;
        if (Math.abs(((t + HALF) % PITCH)) < ROAD / 2 + 2 || Math.abs(((t + HALF) % PITCH) - PITCH) < ROAD / 2 + 2) continue;
        m.compose(new THREE.Vector3(roadCoord(r), 0.03, t), q.identity(), s);
        dashes.setMatrixAt(n++, m);
        m.compose(new THREE.Vector3(t, 0.03, roadCoord(r)), yRot, s);
        dashes.setMatrixAt(n++, m);
      }
    }
    dashes.count = n;
    this.group.add(dashes);
  }

  _buildings(rand) {
    const texs = [];
    for (let k = 0; k < 5; k++) texs.push(windowTexture(rand, k % 2 ? 'rgba(140,210,255,0.6)' : 'rgba(255,150,220,0.45)'));
    const sideMats = texs.map(t => new THREE.MeshStandardMaterial({
      color: 0x1a1d26, map: t, emissive: 0xffffff, emissiveMap: t, emissiveIntensity: 0.85, roughness: 0.6, metalness: 0.3,
    }));
    const roofMat = new THREE.MeshStandardMaterial({ color: 0x15171d, roughness: 0.9 });
    const neonMats = NEON_COLORS.map(c => new THREE.MeshBasicMaterial({ color: new THREE.Color(c).multiplyScalar(2.2), toneMapped: false }));

    for (let i = 0; i < GRID; i++) {
      for (let j = 0; j < GRID; j++) {
        const x0 = roadCoord(i) + ROAD / 2 + 3;
        const z0 = roadCoord(j) + ROAD / 2 + 3;
        const span = BLOCK - 6;
        const list = [];
        this.blockIndex.set(`${i},${j}`, list);

        if (i === GARAGE_BLOCK[0] && j === GARAGE_BLOCK[1]) {
          this._garage(x0, z0, span, list);
          continue;
        }
        const centerDist = Math.hypot(i - GRID / 2 + 0.5, j - GRID / 2 + 0.5);
        const heightScale = 1.6 - centerDist * 0.12;
        // Split block into 1, 2 or 4 lots
        const split = rand();
        const lots = [];
        if (split < 0.25) lots.push([x0, z0, span, span]);
        else if (split < 0.6) {
          const a = span * (0.35 + rand() * 0.3);
          if (rand() < 0.5) { lots.push([x0, z0, a - 2, span]); lots.push([x0 + a + 2, z0, span - a - 2, span]); }
          else { lots.push([x0, z0, span, a - 2]); lots.push([x0, z0 + a + 2, span, span - a - 2]); }
        } else {
          const a = span / 2;
          for (const [dx, dz] of [[0, 0], [a, 0], [0, a], [a, a]]) lots.push([x0 + dx + 1, z0 + dz + 1, a - 2, a - 2]);
        }
        for (const [lx, lz, w, d] of lots) {
          const h = Math.max(10, (12 + Math.pow(rand(), 1.7) * 110) * heightScale);
          const inset = rand() < 0.3 ? 2 : 0;
          this._addBuilding(lx + inset, lz + inset, w - inset * 2, d - inset * 2, h, sideMats[Math.floor(rand() * sideMats.length)], roofMat, list);
          // Neon trim + signs
          const nm = neonMats[Math.floor(rand() * neonMats.length)];
          if (rand() < 0.5) {
            const strip = new THREE.Mesh(new THREE.BoxGeometry(0.6, h * 0.8, 0.6), nm);
            strip.position.set(lx + inset, h * 0.4, lz + inset);
            this.group.add(strip);
          }
          if (rand() < 0.35) {
            const ring = new THREE.Mesh(new THREE.BoxGeometry(w - inset * 2 + 0.6, 0.5, d - inset * 2 + 0.6), nm);
            ring.position.set(lx + w / 2, h + 0.3, lz + d / 2);
            this.group.add(ring);
          }
          if (rand() < 0.55) this._sign(lx + inset, lz + inset, w - inset * 2, d - inset * 2, h, rand);
        }
      }
    }
  }

  _addBuilding(x, z, w, d, h, sideMat, roofMat, list) {
    const geo = new THREE.BoxGeometry(w, h, d);
    tileBoxUVs(geo, w, h, d);
    const mesh = new THREE.Mesh(geo, [sideMat, sideMat, roofMat, roofMat, sideMat, sideMat]);
    mesh.position.set(x + w / 2, h / 2, z + d / 2);
    this.group.add(mesh);
    const box = { minX: x, maxX: x + w, minZ: z, maxZ: z + d, top: h };
    this.buildings.push(box);
    list.push(box);
    return mesh;
  }

  _sign(x, z, w, d, h, rand) {
    const word = SIGN_WORDS[Math.floor(rand() * SIGN_WORDS.length)];
    const color = NEON_COLORS[Math.floor(rand() * NEON_COLORS.length)];
    const mat = new THREE.MeshBasicMaterial({ map: signTexture(word, color), toneMapped: false, side: THREE.DoubleSide });
    mat.color.setScalar(1.25);
    const sw = Math.min(22, Math.max(w, d) * 0.8);
    const sign = new THREE.Mesh(new THREE.PlaneGeometry(sw, sw / 4), mat);
    const y = Math.min(h - 4, 8 + rand() * 30);
    const face = Math.floor(rand() * 4);
    if (face === 0) { sign.position.set(x + w / 2, y, z - 0.3); sign.rotation.y = Math.PI; }
    else if (face === 1) { sign.position.set(x + w / 2, y, z + d + 0.3); }
    else if (face === 2) { sign.position.set(x - 0.3, y, z + d / 2); sign.rotation.y = -Math.PI / 2; }
    else { sign.position.set(x + w + 0.3, y, z + d / 2); sign.rotation.y = Math.PI / 2; }
    this.group.add(sign);
  }

  _garage(x0, z0, span, list) {
    const mat = new THREE.MeshStandardMaterial({ color: 0x2a2d35, roughness: 0.7, metalness: 0.4 });
    // Main hangar, door facing -z (toward road j=4)
    const geo = new THREE.BoxGeometry(span, 18, span);
    const mesh = new THREE.Mesh(geo, mat);
    mesh.position.set(x0 + span / 2, 9, z0 + span / 2);
    this.group.add(mesh);
    const box = { minX: x0, maxX: x0 + span, minZ: z0, maxZ: z0 + span, top: 18 };
    this.buildings.push(box);
    list.push(box);
    // Lit door
    const door = new THREE.Mesh(new THREE.PlaneGeometry(26, 14), new THREE.MeshBasicMaterial({ color: new THREE.Color('#ff9a1f').multiplyScalar(0.55) }));
    door.position.set(x0 + span / 2, 7, z0 - 0.2);
    door.rotation.y = Math.PI;
    this.group.add(door);
    const sm = new THREE.MeshBasicMaterial({ map: signTexture('GARAGE // RIG SHOP', '#ffb300'), toneMapped: false, side: THREE.DoubleSide });
    sm.color.setScalar(1.1);
    const sign = new THREE.Mesh(new THREE.PlaneGeometry(36, 9), sm);
    sign.position.set(x0 + span / 2, 20, z0 - 0.4);
    sign.rotation.y = Math.PI;
    this.group.add(sign);
  }

  _roadDetails(rand) {
    // Streetlights along every road, both sides, with fake light pools.
    const positions = [];
    for (let r = 0; r <= GRID; r++) {
      for (let k = 0; k < GRID; k++) {
        for (const t of [0.3, 0.7]) {
          const along = roadCoord(k) + PITCH * t;
          const side = (t === 0.3 ? 1 : -1) * (ROAD / 2 + 1.5);
          positions.push([roadCoord(r) + side, along, side > 0 ? -1 : 1, 'x']);
          positions.push([along, roadCoord(r) + side, side > 0 ? -1 : 1, 'z']);
        }
      }
    }
    const poleGeo = new THREE.CylinderGeometry(0.25, 0.35, 11, 6);
    const poleMat = new THREE.MeshStandardMaterial({ color: 0x30333a, roughness: 0.6, metalness: 0.6 });
    const lampGeo = new THREE.BoxGeometry(1.6, 0.4, 0.9);
    const lampMat = new THREE.MeshBasicMaterial({ color: new THREE.Color('#ffd9a0').multiplyScalar(3), toneMapped: false });
    const poolGeo = new THREE.PlaneGeometry(22, 22);
    poolGeo.rotateX(-Math.PI / 2);
    const poolMat = new THREE.MeshBasicMaterial({ map: GLOW_TEX, color: 0xffb070, transparent: true, opacity: 0.32, blending: THREE.AdditiveBlending, depthWrite: false });
    const poles = new THREE.InstancedMesh(poleGeo, poleMat, positions.length);
    const lamps = new THREE.InstancedMesh(lampGeo, lampMat, positions.length);
    const pools = new THREE.InstancedMesh(poolGeo, poolMat, positions.length);
    const m = new THREE.Matrix4();
    positions.forEach(([a, b, dir, axis], n) => {
      const [x, z] = axis === 'x' ? [a, b] : [a, b];
      m.makeTranslation(x, 5.5, z);
      poles.setMatrixAt(n, m);
      const lx = axis === 'x' ? x + dir * 2.5 : x;
      const lz = axis === 'z' ? z + dir * 2.5 : z;
      m.makeTranslation(lx, 11, lz);
      lamps.setMatrixAt(n, m);
      m.makeTranslation(lx + (axis === 'x' ? dir * 3 : 0), 0.06, lz + (axis === 'z' ? dir * 3 : 0));
      pools.setMatrixAt(n, m);
    });
    this.group.add(poles, lamps, pools);
  }

  _boundary() {
    // Tall dark walls around the city so the grid edge reads as a highway barrier.
    const mat = new THREE.MeshStandardMaterial({ color: 0x101218, roughness: 0.9 });
    const neon = new THREE.MeshBasicMaterial({ color: new THREE.Color('#ff2bd6').multiplyScalar(2), toneMapped: false });
    const e = HALF + ROAD / 2 + 2;
    for (const [x, z, w, d] of [[0, -e - 2, e * 2 + 8, 4], [0, e + 2, e * 2 + 8, 4], [-e - 2, 0, 4, e * 2 + 8], [e + 2, 0, 4, e * 2 + 8]]) {
      const wall = new THREE.Mesh(new THREE.BoxGeometry(w, 14, d), mat);
      wall.position.set(x, 7, z);
      this.group.add(wall);
      const strip = new THREE.Mesh(new THREE.BoxGeometry(w + 0.2, 0.5, d + 0.2), neon);
      strip.position.set(x, 3, z);
      this.group.add(strip);
    }
    this.limit = e - 3;
  }

  _nearby(x, z) {
    const bi = Math.floor((x + HALF) / PITCH);
    const bj = Math.floor((z + HALF) / PITCH);
    const out = [];
    for (let di = -1; di <= 1; di++) for (let dj = -1; dj <= 1; dj++) {
      const l = this.blockIndex.get(`${bi + di},${bj + dj}`);
      if (l) out.push(...l);
    }
    return out;
  }

  groundHeight(x, z, y) {
    let g = 0;
    for (const b of this._nearby(x, z)) {
      if (x >= b.minX && x <= b.maxX && z >= b.minZ && z <= b.maxZ && y >= b.top - 1.5) g = Math.max(g, b.top);
    }
    return g;
  }

  // Push a vertical capsule (circle in XZ) out of buildings. Returns hit normal or null.
  collide(pos, radius) {
    let hit = null;
    for (const b of this._nearby(pos.x, pos.z)) {
      if (pos.y >= b.top - 1.5) continue;
      const cx = Math.max(b.minX, Math.min(pos.x, b.maxX));
      const cz = Math.max(b.minZ, Math.min(pos.z, b.maxZ));
      let dx = pos.x - cx, dz = pos.z - cz;
      const d2 = dx * dx + dz * dz;
      if (d2 >= radius * radius) continue;
      if (d2 < 1e-6) {
        // center inside box: push out along the shallowest axis
        const opts = [[pos.x - b.minX, -1, 0], [b.maxX - pos.x, 1, 0], [pos.z - b.minZ, 0, -1], [b.maxZ - pos.z, 0, 1]];
        opts.sort((a, c) => a[0] - c[0]);
        const [depth, nx, nz] = opts[0];
        pos.x += nx * (depth + radius);
        pos.z += nz * (depth + radius);
        hit = new THREE.Vector3(nx, 0, nz);
      } else {
        const d = Math.sqrt(d2);
        dx /= d; dz /= d;
        pos.x = cx + dx * radius;
        pos.z = cz + dz * radius;
        hit = new THREE.Vector3(dx, 0, dz);
      }
    }
    const L = this.limit;
    if (Math.abs(pos.x) > L) { hit = new THREE.Vector3(-Math.sign(pos.x), 0, 0); pos.x = Math.sign(pos.x) * L; }
    if (Math.abs(pos.z) > L) { hit = new THREE.Vector3(0, 0, -Math.sign(pos.z)); pos.z = Math.sign(pos.z) * L; }
    return hit;
  }

  pointBlocked(p) {
    if (p.y < 0) return true;
    for (const b of this._nearby(p.x, p.z)) {
      if (p.x >= b.minX && p.x <= b.maxX && p.z >= b.minZ && p.z <= b.maxZ && p.y <= b.top) return true;
    }
    return Math.abs(p.x) > this.limit + 4 || Math.abs(p.z) > this.limit + 4;
  }

  // Coarse line-of-sight check by sampling along the segment.
  lineBlocked(a, b, step = 4) {
    const d = a.distanceTo(b);
    const n = Math.ceil(d / step);
    const p = new THREE.Vector3();
    for (let k = 1; k < n; k++) {
      p.lerpVectors(a, b, k / n);
      if (this.pointBlocked(p)) return true;
    }
    return false;
  }
}
