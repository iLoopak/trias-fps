import { roomCode } from './shared/rules.js';

export function invitePage(link, headers, encrypted) {
  const code = roomCode(link.slice('/join/'.length));
  if (!code || !headers.host || !/^[a-z0-9.:[\]-]+$/i.test(headers.host)) return null;
  const scheme = encrypted || headers['x-forwarded-proto'] === 'https' ? 'https:' : 'http:';
  let origin;
  try { origin = new URL(scheme + '//' + headers.host).origin; } catch { return null; }
  const launch = 'trias://join/' + code + '?server=' + encodeURIComponent(origin);
  return `<!doctype html><html lang="cs"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>Pozvánka TRIAS</title><style>body{background:#08111d;color:#e8f0f6;font:18px system-ui;margin:12vh auto;padding:24px;max-width:570px}a{color:#68e3ec}a.launch{display:inline-block;padding:12px 18px;background:#68e3ec;color:#08111d;font-weight:bold;text-decoration:none}code{color:#ffbd83}</style>
<main><h1>Pozvánka do TRIAS</h1><p>Místnost <code>${code}</code></p><p><a class="launch" href="${launch}">Otevřít a připojit se</a></p>
<p>Pokud se hra neotevře, vlož tuto pozvánku v lobby do volby <b>Z pozvánky</b>. Ve Windows se přímé otvírání odkazů zapíná v nastavení hry.</p>
<p><a href="https://github.com/iLoopak/trias-fps/releases">Stáhnout TRIAS</a></p></main></html>`;
}
