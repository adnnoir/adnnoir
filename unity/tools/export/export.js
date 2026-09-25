// Exporte modèles, matériaux et textures du jeu web vers le projet Unity.
// Utilisation (depuis la racine du dépôt) :
//   1. npm install playwright   (une fois)
//   2. python3 -m http.server 8765 --directory bodycam   (dans un autre terminal)
//   3. node unity/tools/export/export.js
const { chromium } = require('playwright');
const fs = require('fs'), path = require('path');
const ROOT = path.resolve(__dirname, '../../..');
const OUT = path.join(ROOT, 'unity/Entrepot7/Assets/Entrepot7/Resources/E7');
(async () => {
  // copie du jeu avec l'exportateur injecté juste avant le démarrage
  const src = fs.readFileSync(path.join(ROOT, 'bodycam/index.html'), 'utf8');
  const exp = fs.readFileSync(path.join(__dirname, 'exporter.js'), 'utf8');
  const marker = '// Démarrage : on prépare';
  if (!src.includes(marker)) throw new Error('Repère introuvable dans bodycam/index.html : ' + marker);
  fs.writeFileSync(path.join(ROOT, 'bodycam/_export.html'), src.replace(marker, exp + '\n' + marker));
  const b = await chromium.launch({ args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
  const p = await b.newPage({ viewport: { width: 800, height: 450 } });
  p.on('pageerror', e => console.error('Erreur de page :', e.message));
  await p.goto('http://localhost:8765/_export.html');
  await p.waitForFunction(() => window.__e7Ready === true, null, { timeout: 180000 });
  const r = await p.evaluate(() => window.__export());
  for (const [f, v] of Object.entries(r.files)) {
    const fp = path.join(OUT, f); fs.mkdirSync(path.dirname(fp), { recursive: true });
    if (v.b64) fs.writeFileSync(fp, Buffer.from(v.b64, 'base64')); else fs.writeFileSync(fp, v.text);
  }
  await b.close();
  fs.unlinkSync(path.join(ROOT, 'bodycam/_export.html'));
  console.log('Export terminé :', Object.keys(r.files).length, 'fichiers. Tailles :', JSON.stringify(r.sizes));
})();
