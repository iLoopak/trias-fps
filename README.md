# TRIAS

Původní 3D multiplayerová FPS z orbitální stanice Relay. V týmových režimech proti sobě hrají ION, EMBER a VIOLET, každý až se čtyřmi hráči. Místnost pojme 12 lidí. Ve výchozím nastavení doplňují volná místa boti, kteří je automaticky uvolňují příchozím hráčům. Nastavení botů a režim určuje zakladatel místnosti.

## Spuštění

Potřebuješ Node.js 22 nebo novější a desktopový prohlížeč s WebGL 2 a podporou zámku myši.

```sh
npm ci
npm start
```

Otevři `http://localhost:3000`, vyber režim a vstup do arény. Klikni na **Hrát**, aby prohlížeč zachytil myš. Pohyb je **WASD**, míření myší, střelba levým tlačítkem, sprint **Shift**, skok **mezerníkem** a přebití **R**. Dalším stiskem mezerníku ve vzduchu uděláš dvojskok. Sebrané zbraně přepínáš čísly **1–4**. Klávesou **Tab** otevřeš skóre a **Esc** uvolní myš. Pokud prohlížeč zámek nepovolí, vyber **Hrát bez zámku myši** a miř tažením pravého tlačítka.

Na stejné síti použijte LAN adresu, kterou vypíše server. Zakladatel otevře hru přes tuto adresu a tlačítkem **Pozvat** zkopíruje odkaz. Ostatní otevřou stejný odkaz, zadají přezdívku a vstoupí do arény. Port lze změnit příkazem `PORT=8080 npm start`.

Pro hraní přes internet spusť projekt na hostingu, který umí provozovat Node.js a WebSockety. HTTP i WebSocket cesta `/play` musí směřovat na stejný běžící server. Při použití reverzní proxy zachovej původní hlavičku `Host` a předávej WebSocket upgrade. Repo na GitHubu slouží k uložení zdrojáků. Multiplayer vyžaduje běžící Node.js server.

## Aréna a výbava

Každý startuje s karabinou **Pulse** a neomezenou rezervní municí. Na stupňovitých vyhlídkách jsou **Scatter** pro boj zblízka, **Lance** pro přesnou střelbu a rychlopalný **Arc**. První sebrání zbraň rovnou vybaví. Další sebrání doplní munici. Po vyřazení začneš opět s Pulse.

V nižších patrech najdeš léčení a štít. Na vrcholu centrálního relé je **Overcharge**, který na 18 sekund zvýší poškození. K relé vede cesta přes nižší plošiny u jeho paty. Zbraně a bonusy se po sebrání po čase obnoví. Čtyři krátké výstřely a jeden zvuk sebrání výbavy pocházejí z ElevenLabs a jsou uložené v `public/sfx`. Prohlížeč při hraní přehrává soubory lokálně a směruje střelbu podle pozice soupeřů. Skok a potvrzení zásahu používají krátké syntetické tóny.

## Režimy

| Režim | Pravidla | Cíl | Čas |
| --- | --- | --- | --- |
| Team Deathmatch | Tým získá bod za vyřazení soupeře. | 35 bodů | 6 min |
| Deathmatch | Každý hraje za sebe. | 20 vyřazení | 5 min |
| Dominance | Obsazujte stanice A, B a C. Každá držená stanice dá týmu bod za sekundu. | 150 bodů | 7 min |
| Capture the Flag | Dones soupeřovu vlajku na vlastní základnu, zatímco je tvoje vlajka doma. | 5 vlajek | 8 min |

Po vypršení času rozhoduje skóre. Další kolo začne automaticky. Prázdné místnosti se zavřou a výsledky zůstávají v paměti pouze po dobu zápasu.

## První push do nového repozitáře na GitHubu

1. Na [GitHubu založ nový repozitář](https://github.com/new), třeba `trias-fps`. Vyber viditelnost a ponech ho prázdný. README i `.gitignore` už máš v projektu.
2. Použij adresu z GitHubu. Příklad níže používá SSH a předpokládá, že máš na GitHubu nastavený SSH klíč. Pokud už používáš přihlášení přes HTTPS, nahraď SSH adresu HTTPS adresou z repozitáře.
3. Otevři terminál ve složce hry a spusť příkazy. Nahraď `TVUJ-UCET` a název repozitáře.

```sh
cd /Users/lukassitta/Projects/fps
git init -b main
git add .
git commit -m "Initial TRIAS multiplayer FPS"
git remote add origin git@github.com:TVUJ-UCET/trias-fps.git
git push -u origin main
```

Podrobnosti k importu a přihlášení najdeš v [dokumentaci GitHubu](https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github?platform=mac).

<details>
<summary>Obrazové podklady a prompty</summary>

Ilustrace lobby `public/orbit.jpg` a textura krytů `public/bulkhead.jpg` vznikly vestavěným nástrojem ImageGen. Modely pilotů a zbraní se skládají z 3D geometrie za běhu.

**Ilustrace lobby**

> Use case: stylized-concept. Asset type: wide lobby background for TRIAS, an original competitive sci-fi browser arena game. A massive triangular industrial orbital station floating in deep space over the far curved limb of a moody charcoal planet. An asymmetric Y-shaped ring of layered matte gunmetal trusses and ribbed armored industrial modules, with three distinct sparse light circuits in ice turquoise, burnt amber and lilac. Main station occupies the right two-thirds of a cinematic landscape image; keep the left third dark with ample negative space for live HTML text, no text in image. Realistic hard-surface 3D concept art with precise bevels, strong silhouette, hard rim light, subtle tungsten window lights, pitch-dark starfield and hazy planet horizon low in frame. High contrast, cinematic, premium competitive game art. No people, no weapons, no typography, no symbols, no logos, no watermarks.

**Textura krytů**

> Use case: stylized-concept. Asset type: one square front-facing color map texture for reusable hard-surface wall panels of the TRIAS orbital FPS arena. Perfectly orthographic close-up of one flat brushed gunmetal ship bulkhead panel, edge to edge, aligned to square image, cool blue-gray slate hues, recognizable trapezoidal recessed plates and clean panel seams with a few tiny pale hazard-orange paint dashes and bolt heads. Modern realistic spacecraft material, matte mid-dark but legible, coarse features that remain clear at small scale. Uniform diffuse neutral lighting suitable for a color map, minimal baked shadow, no perspective, no vignette, no background, no letters, no numbers, no brand marks, no glowing lights.

</details>
