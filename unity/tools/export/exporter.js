// Injecté dans le module du jeu (même portée que les constructeurs). Exporte modèles, matériaux et textures pour Unity.
window.__export = () => {
  const files = {}; // chemin -> { b64 } ou { text }
  // ---------- textures ----------
  const texName = new Map(), texOut = {};
  const NAMED = { concrete: concreteC, concreteRough, wall: wallC, crate: crateC, card: cardC, ceil: ceilC, barrel: barrelC, fabric: fabricC, denim: denimC,
    gunWear: gunWearC, stipple: stippleC, brush: brushC, hole: holeC, flash: flashC, blob: blobC, blood: bloodC, cone: coneC, paper: paperC, med: medC, ammo: ammoC,
    holo: holoC, dot: dotC, target: targetC };
  for (const [k, c] of Object.entries(NAMED)) { texName.set(c, k); texOut[k] = c; }
  const extraN = { concrete_n: normalFrom(concreteC, 3), wall_n: normalFrom(wallC, 4), crate_n: normalFrom(crateC, 3), fabric_n: normalFrom(fabricC, 2), denim_n: normalFrom(denimC, 2) };
  for (const [k, c] of Object.entries(extraN)) texOut[k] = c;
  function tname(t, hint) {
    if (!t || !t.image) return null;
    let n = texName.get(t.image);
    if (!n) { n = hint; let i = 2; while (texOut[n]) n = hint + '_' + (i++); texName.set(t.image, n); texOut[n] = t.image; }
    return n;
  }
  // ---------- matériaux ----------
  function matDesc(m, key) {
    const d = { key, kind: m.isMeshBasicMaterial ? 'basic' : 'std' };
    if (m.color) { d.color = '#' + m.color.getHexString(); d.lin = [m.color.r, m.color.g, m.color.b]; }
    d.opacity = m.opacity; d.transparent = !!m.transparent; d.additive = m.blending === THREE.AdditiveBlending; d.doubleSide = m.side === THREE.DoubleSide;
    if (m.metalness !== undefined) d.metalness = m.metalness;
    if (m.roughness !== undefined) d.roughness = m.roughness;
    if (m.emissive && m.emissiveIntensity > 0 && m.emissive.getHex() !== 0) { d.emissive = [m.emissive.r, m.emissive.g, m.emissive.b]; d.emissiveIntensity = m.emissiveIntensity; }
    if (m.clearcoat) d.clearcoat = m.clearcoat;
    const T = (t, suf) => { const n = tname(t, key + suf); return n ? { name: n, repeat: [t.repeat.x, t.repeat.y] } : null; };
    if (m.map) d.map = T(m.map, '_map');
    if (m.normalMap) { d.normalMap = T(m.normalMap, '_n'); d.normalScale = m.normalScale ? m.normalScale.x : 1; }
    if (m.roughnessMap) d.roughnessMap = T(m.roughnessMap, '_r');
    if (m.emissiveMap) d.emissiveMap = T(m.emissiveMap, '_e');
    return d;
  }
  // ---------- écriture binaire ----------
  class W8 {
    constructor() { this.buf = new ArrayBuffer(1 << 20); this.dv = new DataView(this.buf); this.o = 0; }
    need(n) { if (this.o + n <= this.buf.byteLength) return; let s = this.buf.byteLength * 2; while (s < this.o + n) s *= 2; const nb = new ArrayBuffer(s); new Uint8Array(nb).set(new Uint8Array(this.buf)); this.buf = nb; this.dv = new DataView(nb); }
    u8(v) { this.need(1); this.dv.setUint8(this.o, v); this.o += 1; }
    u16(v) { this.need(2); this.dv.setUint16(this.o, v, true); this.o += 2; }
    i16(v) { this.need(2); this.dv.setInt16(this.o, v, true); this.o += 2; }
    u32(v) { this.need(4); this.dv.setUint32(this.o, v, true); this.o += 4; }
    f32(v) { this.need(4); this.dv.setFloat32(this.o, v, true); this.o += 4; }
    str(s) { const b = new TextEncoder().encode(s); this.u16(b.length); this.need(b.length); new Uint8Array(this.buf, this.o, b.length).set(b); this.o += b.length; }
    bytes() { return new Uint8Array(this.buf, 0, this.o); }
  }
  const b64 = u8 => { let s = ''; for (let i = 0; i < u8.length; i += 0x8000) s += String.fromCharCode.apply(null, u8.subarray(i, i + 0x8000)); return btoa(s); };
  // miroir : 'z' (armes, mains : l'avant est -z dans Three.js) ou 'x' (personnages : l'avant est +z)
  const mv = (v, ax) => ax === 'z' ? [v.x, v.y, -v.z] : [-v.x, v.y, v.z];
  const mq = (q, ax) => ax === 'z' ? [-q.x, -q.y, q.z, q.w] : [q.x, -q.y, -q.z, q.w];
  // ---------- extraction des pièces d'un objet ----------
  // named : Map(Object3D -> nom). Les maillages sont rattachés au nœud nommé le plus proche.
  function extract(root, named, matKey, ax) {
    root.updateMatrixWorld(true);
    const nodes = [], idx = new Map();
    root.traverse(o => { if (named.has(o)) { idx.set(o, nodes.length); nodes.push(o); } });
    const nearest = o => { let p = o.parent; while (p && !named.has(p)) p = p.parent; return p; };
    const nodeRecs = nodes.map(o => {
      const par = o === root ? null : nearest(o);
      const m = new THREE.Matrix4();
      if (par) m.copy(par.matrixWorld).invert().multiply(o.matrixWorld); else m.identity();
      const p = new THREE.Vector3(), q = new THREE.Quaternion(), s = new THREE.Vector3(); m.decompose(p, q, s);
      return { name: named.get(o), parent: par ? idx.get(par) : -1, pos: mv(p, ax), rot: mq(q, ax) };
    });
    const pieces = [];
    const visibleChain = o => { for (let p = o; p; p = p.parent) { if (p === root) return true; if (!p.visible) return false; } return true; };
    root.traverse(o => {
      if (!o.isMesh || !visibleChain(o)) return;
      const node = named.has(o) ? o : nearest(o);
      const M = new THREE.Matrix4().copy(node.matrixWorld).invert().multiply(o.matrixWorld);
      const N = new THREE.Matrix3().getNormalMatrix(M);
      const g = o.geometry, pa = g.attributes.position, na = g.attributes.normal, ua = g.attributes.uv;
      const index = g.index ? Array.from(g.index.array) : [...Array(pa.count).keys()];
      const groups = Array.isArray(o.material) ? g.groups.map(gr => ({ start: gr.start, count: gr.count, mat: o.material[gr.materialIndex] })) : [{ start: 0, count: index.length, mat: o.material }];
      for (const gr of groups) {
        const key = matKey(gr.mat, o); if (!key) continue;
        const remap = new Map(), P = [], Nn = [], U = [], I = [];
        const v = new THREE.Vector3();
        for (let i = gr.start; i < gr.start + gr.count && i < index.length; i++) {
          const src = index[i];
          let dst = remap.get(src);
          if (dst === undefined) {
            dst = remap.size; remap.set(src, dst);
            v.fromBufferAttribute(pa, src).applyMatrix4(M); P.push(...mv(v, ax));
            if (na) { v.fromBufferAttribute(na, src).applyMatrix3(N).normalize(); Nn.push(...mv(v, ax)); } else Nn.push(0, 1, 0);
            if (ua) U.push(ua.getX(src), ua.getY(src)); else U.push(0, 0);
          }
          I.push(dst);
        }
        for (let i = 0; i + 2 < I.length; i += 3) { const t = I[i + 1]; I[i + 1] = I[i + 2]; I[i + 2] = t; } // miroir : on inverse l'ordre des triangles
        const sig = node.uuid.slice(0, 0) + named.get(node) + '|' + key + '|' + P.length + '|' + P.slice(0, 3).map(x => x.toFixed(4)).join(',') + '|' + P.slice(-3).map(x => x.toFixed(4)).join(',');
        pieces.push({ node: idx.get(node), key, P, N: Nn, U, I, sig, mesh: o });
      }
    });
    return { nodes: nodeRecs, pieces };
  }
  // ---------- variantes : on repère les pièces propres à chaque option ----------
  function withVariants(build, dims, base) {
    const b = build(base);
    const res = b.res; res.pieces.forEach(p => p.tag = 'base');
    const extra = [];
    for (const [dim, opts] of Object.entries(dims)) for (const opt of opts) {
      if (opt === base[dim]) continue;
      const v = build(Object.assign({}, base, { [dim]: opt })).res;
      const count = new Map(); for (const p of res.pieces) count.set(p.sig, (count.get(p.sig) || 0) + 1);
      const vcount = new Map(); for (const p of v.pieces) vcount.set(p.sig, (vcount.get(p.sig) || 0) + 1);
      for (const p of v.pieces) { const c = count.get(p.sig) || 0; if (c > 0) count.set(p.sig, c - 1); else { p.tag = dim + ':' + opt; extra.push(p); } }
      for (const p of res.pieces) { const c = vcount.get(p.sig) || 0; if (c > 0) vcount.set(p.sig, c - 1); else if (p.tag === 'base') p.tag = dim + ':' + base[dim]; }
    }
    res.pieces.push(...extra);
    return b;
  }
  function writeModel(path, res, mats) {
    const w = new W8();
    w.u32(0x324d3745); // "E7M2"
    w.u16(res.nodes.length);
    for (const n of res.nodes) { w.str(n.name); w.i16(n.parent); n.pos.forEach(x => w.f32(x)); n.rot.forEach(x => w.f32(x)); }
    w.u32(res.pieces.length);
    for (const p of res.pieces) {
      w.u16(p.node); w.str(p.tag); w.str(p.key);
      const vc = p.P.length / 3; w.u32(vc); w.u32(p.I.length);
      for (const x of p.P) w.f32(x); for (const x of p.N) w.f32(x); for (const x of p.U) w.f32(x);
      for (const x of p.I) w.u32(x);
    }
    files[path + '.bytes'] = { b64: b64(w.bytes()) };
    return w.o;
  }
  const sizes = {};
  // ---------- armes du joueur ----------
  const gmKey = new Map(Object.entries(gunMats).map(([k, m]) => [m, k]));
  gmKey.set(holoMat, 'holo'); gmKey.set(brassMat, 'brass');
  let markN = 0;
  const allMats = {};
  const keyGun = (m) => {
    let k = gmKey.get(m);
    if (!k) {
      // marquages : une clé par contenu (le même texte dans plusieurs variantes = même matériau)
      const url = m.map && m.map.image ? m.map.image.toDataURL() : String(markN++);
      let h = 0; for (let i = 0; i < url.length; i += 7) h = (h * 31 + url.charCodeAt(i)) | 0;
      k = 'mark_' + (h >>> 0).toString(16); gmKey.set(m, k);
    }
    if (!allMats[k]) allMats[k] = matDesc(m, k);
    return k;
  };
  const V = v => v ? [v.x, v.y, -v.z] : null;
  const Q = e => { const q = new THREE.Quaternion().setFromEuler(new THREE.Euler(e.x, e.y, e.z)); return [-q.x, -q.y, q.z, q.w]; };
  const DIMS = { carbine: { optic: ['irons', 'reddot', 'holo'], muzzle: ['std', 'suppressor'], rail: ['light', 'laser'] },
    smg: { optic: ['irons', 'reddot', 'holo'], muzzle: ['std', 'suppressor'], rail: ['light', 'laser'] },
    shotgun: { optic: ['irons', 'reddot', 'holo'], muzzle: ['std'], rail: ['light', 'laser'] },
    pistol: { optic: ['irons', 'reddot'], muzzle: ['std', 'suppressor'], rail: ['light', 'laser'] } };
  for (const k of ['carbine', 'smg', 'shotgun', 'pistol']) {
    const metaOpt = { optic: {}, muzzle: {}, rail: {} };
    const build = att => {
      const Wd = BUILD[k](att);
      const named = new Map([[Wd.root, 'root']]);
      for (const n of ['mag', 'slide', 'pump', 'handle']) if (Wd[n]) named.set(Wd[n], n);
      const res = extract(Wd.root, named, m => keyGun(m), 'z');
      res.pieces.forEach(p => { if (p.mesh.userData.sling) p.sling = true; });
      metaOpt.optic[att.optic] = { sight: V(Wd.sight), adsDist: Wd.adsDist };
      metaOpt.muzzle[att.muzzle] = { muzzle: V(Wd.muzzle) };
      metaOpt.rail[att.rail] = { laser: V(Wd.laser) };
      return { res, Wd };
    };
    const base = { optic: 'irons', muzzle: 'std', rail: 'light' };
    const b = withVariants(build, DIMS[k], base);
    b.res.pieces.forEach(p => { if (p.sling) p.tag = 'sling'; });
    const Wd = b.Wd;
    const poses = {};
    for (const [n, [p, r]] of Object.entries(Wd.poses)) poses[n] = { pos: V(p), rot: Q(r) };
    const meta = { key: k, hip: V(Wd.hip), grip: { pos: V(Wd.grip[0]), rot: Q(Wd.grip[1]) }, poses, magGrab: V(Wd.magGrab), handleTravel: Wd.handleTravel || 0, pumpTravel: Wd.pumpTravel || 0, slideTravel: Wd.slideTravel || 0,
      preview: Wd.preview, options: metaOpt, dims: DIMS[k] };
    files[`Models/w_${k}.json`] = { text: JSON.stringify(meta, null, 1) };
    sizes[k] = writeModel(`Models/w_${k}`, b.res);
  }
  // ---------- mains et manches (vue à la première personne) ----------
  {
    const root = new THREE.Group(); root.name = 'hands';
    const gR = buildGlove(1.35, 0.4), gL = buildGlove(1.25, 0.2), sR = buildSleeve(0.42), sL = buildSleeve(0.5);
    const watch = new THREE.Mesh(new THREE.BoxGeometry(0.04, 0.012, 0.035), gunMats.watch); watch.position.set(-0.03, 0.035, 0.025);
    gL.add(watch);
    for (const o of [gR, gL, sR, sL]) root.add(o);
    const named = new Map([[root, 'root'], [gR, 'glove_r'], [gL, 'glove_l'], [sR, 'sleeve_r'], [sL, 'sleeve_l']]);
    const res = extract(root, named, m => keyGun(m), 'z'); res.pieces.forEach(p => p.tag = 'base');
    sizes.hands = writeModel('Models/vm_hands', res);
  }
  files['Models/gun_materials.json'] = { text: JSON.stringify(allMats, null, 1) };
  // ---------- personnages ----------
  const humanMats = {};
  function exportHuman(name, mats, dims, base, fixed) {
    const mk = new Map();
    for (const [k, m] of Object.entries(mats)) if (!mk.has(m)) mk.set(m, k);
    for (const [k, m] of Object.entries(botMats)) if (!mk.has(m)) mk.set(m, k);
    const keyH = m => { const k = mk.get(m) || 'misc'; if (!humanMats[k] && mk.get(m)) humanMats[k] = matDesc(m, k); return k; };
    const build = opt => {
      const o = Object.assign({ mats }, fixed);
      if (opt.style === 'hood') { o.vest = null; o.hood = true; } else if (opt.style === 'vest') { o.vest = 'plain'; o.hood = false; }
      o.headgear = opt.headgear === 'none' ? null : opt.headgear;
      const H = buildHuman(o);
      const named = new Map([[H.root, 'root'], [H.pelvis, 'pelvis'], [H.spine, 'spine'], [H.chest, 'chest'], [H.neck, 'neck'], [H.head, 'head']]);
      for (const s of ['L', 'R']) { const a = H.arms[s]; named.set(a.shoulder, 'shoulder_' + s); named.set(a.upper, 'upper_' + s); named.set(a.fore, 'fore_' + s); named.set(a.hand, 'hand_' + s); const l = H.legs[s]; named.set(l.hip, 'hip_' + s); named.set(l.knee, 'knee_' + s); named.set(l.ankle, 'ankle_' + s); }
      const res = extract(H.root, named, keyH, 'x');
      res.pieces.forEach(p => { p.part = p.mesh.userData.part; });
      return { res };
    };
    const b = withVariants(build, dims, base);
    sizes[name] = writeModel('Models/' + name, b.res);
  }
  {
    const mats = { top: clothMat(0x2d3440), pants: std({ color: 0x2c3a55, map: tex(denimC, 3, 3), normalMap: tex(extraN.denim_n, 3, 3, false), roughness: 0.92 }), skin: std({ color: 0xc59a7a, roughness: 0.6 }), shoe: std({ color: 0xdedede, roughness: 0.7 }), glove: std({ color: 0x121214, roughness: 0.9 }) };
    mats.vest = botMats.vest;
    exportHuman('h_suspect', mats, { style: ['hood', 'vest'], headgear: ['none', 'cap'] }, { style: 'hood', headgear: 'none' }, { face: 'mask' });
    const om = charMats(); om.cap = std({ color: UNIFORMS.police.vest, roughness: 0.8 });
    exportHuman('h_officer', om, { headgear: ['cap', 'helmet', 'none'] }, { headgear: 'cap' }, { face: 'officer', vest: 'police', holster: true, boots: true });
  }
  files['Models/human_materials.json'] = { text: JSON.stringify(humanMats, null, 1) };
  // ---------- armes tenues par les personnages ----------
  {
    const wm = {};
    for (const t of ['pistol', 'shotgun', 'rifle', 'police']) {
      const g = buildWorldGun(t);
      const named = new Map([[g, 'root']]);
      const mk = new Map(Object.entries(botMats).map(([k, m]) => [m, k]));
      const res = extract(g, named, m => { const k = mk.get(m) || 'misc'; if (!wm[k]) wm[k] = matDesc(m, k); return k; }, 'x');
      res.pieces.forEach(p => p.tag = 'base');
      sizes['wg_' + t] = writeModel('Models/wg_' + t, res);
      const u = g.userData, X = v => [-v.x, v.y, v.z];
      files[`Models/wg_${t}.json`] = { text: JSON.stringify({ muzzle: X(u.muzzle), grip: X(u.grip), guard: X(u.guard), type: u.type }) };
    }
    files['Models/worldgun_materials.json'] = { text: JSON.stringify(wm, null, 1) };
  }
  // ---------- matériaux du décor ----------
  const worldMats = {};
  for (const [k, m] of Object.entries(MATS)) worldMats[k] = matDesc(m, k);
  worldMats.floor.normalMap = { name: 'concrete_n', repeat: worldMats.floor.map.repeat };
  worldMats.wall.normalMap = { name: 'wall_n', repeat: [1, 1] };
  worldMats.crate.normalMap = { name: 'crate_n', repeat: [1, 1] };
  files['Models/world_materials.json'] = { text: JSON.stringify(worldMats, null, 1) };
  // ---------- textures (PNG) ----------
  for (const [n, c] of Object.entries(texOut)) files[`Textures/${n}.png`] = { b64: c.toDataURL('image/png').split(',')[1] };
  return { files, sizes, textures: Object.keys(texOut) };
};
