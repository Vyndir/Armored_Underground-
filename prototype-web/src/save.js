import { DEFAULT_LOADOUT, DEFAULT_PAINT, SLOTS } from './parts.js';

const KEY = 'armored-underground-save-v1';

function fresh() {
  const owned = {};
  for (const s of SLOTS) owned[DEFAULT_LOADOUT[s]] = true;
  return {
    credits: 1500,
    rep: 0,
    owned,
    loadout: { ...DEFAULT_LOADOUT },
    paint: { ...DEFAULT_PAINT },
    results: {}, // eventId -> { bestPos, bestTime }
  };
}

export function loadSave() {
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) return fresh();
    const data = JSON.parse(raw);
    const base = fresh();
    return { ...base, ...data, loadout: { ...base.loadout, ...data.loadout }, paint: { ...base.paint, ...data.paint }, owned: { ...base.owned, ...data.owned } };
  } catch {
    return fresh();
  }
}

export function writeSave(save) {
  try { localStorage.setItem(KEY, JSON.stringify(save)); } catch { /* storage unavailable */ }
}

export function resetSave() {
  try { localStorage.removeItem(KEY); } catch { /* ignore */ }
  return fresh();
}
