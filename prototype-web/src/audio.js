// Tiny synthesized sound layer (WebAudio): engine hum, boost roar, weapons, UI.
export class Audio {
  constructor() {
    this.ctx = null;
    this.listener = null;
  }

  start() {
    if (this.ctx) { this.ctx.resume(); return; }
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    const ctx = this.ctx = new AC();
    this.master = ctx.createGain();
    this.master.gain.value = 0.5;
    this.master.connect(ctx.destination);

    // Noise buffer shared by effects
    const len = ctx.sampleRate * 2;
    this.noise = ctx.createBuffer(1, len, ctx.sampleRate);
    const d = this.noise.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;

    // Engine: two detuned oscillators through a lowpass
    this.engFilter = ctx.createBiquadFilter();
    this.engFilter.type = 'lowpass';
    this.engFilter.frequency.value = 400;
    this.engGain = ctx.createGain();
    this.engGain.gain.value = 0.0;
    this.osc1 = ctx.createOscillator(); this.osc1.type = 'sawtooth';
    this.osc2 = ctx.createOscillator(); this.osc2.type = 'square';
    this.osc1.connect(this.engFilter); this.osc2.connect(this.engFilter);
    this.engFilter.connect(this.engGain).connect(this.master);
    this.osc1.start(); this.osc2.start();

    // Boost roar: looping filtered noise
    this.boostSrc = ctx.createBufferSource();
    this.boostSrc.buffer = this.noise; this.boostSrc.loop = true;
    this.boostFilter = ctx.createBiquadFilter();
    this.boostFilter.type = 'bandpass'; this.boostFilter.frequency.value = 900; this.boostFilter.Q.value = 0.7;
    this.boostGain = ctx.createGain(); this.boostGain.gain.value = 0;
    this.boostSrc.connect(this.boostFilter).connect(this.boostGain).connect(this.master);
    this.boostSrc.start();
  }

  setListener(pos) { this.listener = pos; }

  engine(speed, boost, active) {
    if (!this.ctx) return;
    const t = this.ctx.currentTime;
    const f = 45 + speed * 1.6 + boost * 40;
    this.osc1.frequency.setTargetAtTime(f, t, 0.08);
    this.osc2.frequency.setTargetAtTime(f * 0.501, t, 0.08);
    this.engFilter.frequency.setTargetAtTime(300 + speed * 18 + boost * 900, t, 0.1);
    this.engGain.gain.setTargetAtTime(active ? 0.05 + Math.min(speed, 80) * 0.0012 : 0, t, 0.1);
    this.boostGain.gain.setTargetAtTime(active ? boost * 0.22 : 0, t, 0.06);
    this.boostFilter.frequency.setTargetAtTime(700 + speed * 15, t, 0.1);
  }

  _vol(pos, base) {
    if (!pos || !this.listener) return base;
    const d = pos.distanceTo(this.listener);
    return base / (1 + d * 0.02);
  }

  _noise(dur, freq, q, vol, type = 'bandpass') {
    const ctx = this.ctx;
    const src = ctx.createBufferSource();
    src.buffer = this.noise;
    src.playbackRate.value = 0.8 + Math.random() * 0.4;
    const f = ctx.createBiquadFilter();
    f.type = type; f.frequency.value = freq; f.Q.value = q;
    const g = ctx.createGain();
    const t = ctx.currentTime;
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    src.connect(f).connect(g).connect(this.master);
    src.start(t, Math.random()); src.stop(t + dur + 0.05);
  }

  _tone(freq, dur, vol, type = 'square', slide = 1) {
    const ctx = this.ctx;
    const o = ctx.createOscillator();
    o.type = type;
    const g = ctx.createGain();
    const t = ctx.currentTime;
    o.frequency.setValueAtTime(freq, t);
    o.frequency.exponentialRampToValueAtTime(freq * slide, t + dur);
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    o.connect(g).connect(this.master);
    o.start(t); o.stop(t + dur + 0.05);
  }

  shot(kind, pos, isPlayer) {
    if (!this.ctx) return;
    const v = this._vol(isPlayer ? null : pos, isPlayer ? 0.35 : 0.3);
    if (kind === 'rail') { this._tone(1800, 0.5, v * 0.6, 'sawtooth', 0.1); this._noise(0.4, 3000, 0.5, v); }
    else if (kind === 'scatter') this._noise(0.25, 700, 0.6, v * 1.4);
    else if (kind === 'missile') this._noise(0.6, 400, 1.2, v);
    else if (kind === 'mine') this._tone(220, 0.15, v * 0.4, 'square', 0.5);
    else this._noise(0.08, 1600, 0.8, v);
  }

  boom(pos) { if (this.ctx) this._noise(0.9, 160, 0.6, this._vol(pos, 0.9), 'lowpass'); }
  hit() { if (this.ctx) this._tone(1400, 0.05, 0.12, 'square', 0.7); }
  hurt() { if (this.ctx) this._noise(0.15, 300, 2, 0.4); }
  quick() { if (this.ctx) this._noise(0.3, 1200, 0.5, 0.35); }
  gate(final) { if (this.ctx) { this._tone(final ? 660 : 990, 0.12, 0.15, 'triangle'); this._tone(final ? 990 : 1320, 0.2, 0.12, 'triangle'); } }
  beep(f) { if (this.ctx) this._tone(f, 0.25, 0.2, 'triangle'); }
  ui() { if (this.ctx) this._tone(1200, 0.04, 0.08, 'square'); }
  buy() { if (this.ctx) { this._tone(880, 0.1, 0.12, 'triangle'); this._tone(1320, 0.18, 0.1, 'triangle'); } }
}
