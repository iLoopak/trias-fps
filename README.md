# TRIAS

Původní 3D multiplayerová FPS na orbitální stanici Relay. Zápas se odehrává pod prasklým reaktorem mezi tyrkysovou turbínou ION, oranžovými chladiči EMBER a fialovými anténami VIOLET. V týmových režimech proti sobě hrají ION, EMBER a VIOLET, každý až se čtyřmi hráči. Místnost pojme 12 lidí. Ve výchozím nastavení doplňují volná místa boti, kteří je automaticky uvolňují příchozím hráčům. Mapu, režim a nastavení botů určuje zakladatel místnosti. Každý hráč si sám vybírá pilota.

## Spuštění

### Unity

Unity je hlavní klient TRIAS. Otevři projekt `unity-vfx-preview` v Unity 6.3 LTS a zvol **TRIAS → Open full game**. Online zápas řídí samostatný server Node.js. [Návod pro Unity](unity-vfx-preview/README.md)

### Webová verze

Potřebuješ Node.js 22 nebo novější a desktopový prohlížeč s WebGL 2 a podporou zámku myši.

```sh
npm ci
npm start
```

Otevři `http://localhost:3000`, vyber režim a vstup do arény. Klikni na **Hrát**, aby prohlížeč zachytil myš. Pohyb je **WASD**, míření myší, střelba levým tlačítkem, sprint **Shift**, skok **mezerníkem** a přebití **R**. Dalším stiskem mezerníku ve vzduchu uděláš dvojskok. Úhyb spustíš klávesou **Q** nebo dvojstiskem směru. Ve vzduchu ho lze použít jednou za let. Sebrané zbraně přepínáš čísly **1–5** nebo kolečkem. Podržením **C** se přikrčíš. Klávesou **G** hodíš granát, **E** obsloužíš hnízdo nebo z něj vystoupíš a klávesou **F** spustíš sekundární střelbu aktuální zbraně. Se zámkem myši funguje i pravé tlačítko. Citlivost myši nastavíš v nabídce **Ovládání**. Klávesou **Tab** otevřeš skóre a **Esc** uvolní myš. Pokud prohlížeč zámek nepovolí, vyber **Hrát bez zámku myši** a miř tažením pravého tlačítka.

Na stejné síti použijte LAN adresu, kterou vypíše server. Zakladatel otevře hru přes tuto adresu a tlačítkem **Pozvat** zkopíruje odkaz. Ostatní otevřou stejný odkaz, zadají přezdívku a vstoupí do arény. Port lze změnit příkazem `PORT=8080 npm start`.

Pro hraní přes internet spusť projekt na hostingu, který umí provozovat Node.js a WebSockety. HTTP i WebSocket cesta `/play` musí směřovat na stejný běžící server. Při použití reverzní proxy zachovej původní hlavičku `Host` a předávej WebSocket upgrade. Repo na GitHubu slouží k uložení zdrojáků. Multiplayer vyžaduje běžící Node.js server.

Server simuluje 30 kroků za sekundu. Aktuální klient přijímá kompaktní snímek stavu po každém kroku. Vlastní pohyb předpovídá lokálně, vzdálené piloty vykresluje podle snímků se zásobou, která se přizpůsobuje kolísání doručení. Zobrazený ping měří cestu zprávy na server a zpět. Výstřel vyhodnocuje server ve svém aktuálním kroku.

Vercel Functions podporují WebSockety, ale dvě připojení mohou skončit v různých instancích a spojení mají časový limit. Současné místnosti existují v paměti jednoho procesu. Pro samostatný deploy na Vercel by hra potřebovala sdílený stav, jednoho koordinátora simulace na místnost a obnovení spojení. [Dokumentace Vercelu](https://vercel.com/kb/guide/do-vercel-serverless-functions-support-websocket-connections)

## Mapy a piloti

- **Relay Station** je orbitální aréna s prasklým reaktorem, zavěšenými lávkami a otevřeným bojem o střed.
- **Vanta Foundry** je slévárna s tavicími kanály, bočními mosty a nízkými servisními průchody pro přikrčené piloty. Po obvodu jsou tři pevná energetická hnízda. Mají omezený úhel míření a při dlouhé střelbě se přehřívají. Klávesa **E** obsluhu přepíná.
- **Meridian Basin** měří 112 × 112 herních metrů. Široký okruh a tři paprsky mezi doky spojují tři sektory. Barevné servisní pruhy, odpalovací podložky a horní lávky dávají pěchotě kratší cestu a přístup ke zbraním. Antény v Dominanci a vlajky v CTF řeší pěchota. Zelené opravárenské plochy opravují obsazené vozidlo po zastavení. Minimapa ukazuje okruh, cíle a spojence.
- **Mechový ostrov** je kulatá pěší aréna na malé plovoucí planetě. Písková pěšina vede přes jižní hřeben k vyhlídkám nad dvěma vodopády. Na hřebeni leží Rift. V údolí vede přes řeku trojice dřevěných mostků a brod, který zpomaluje běh. Na březích poskakují zajíci a po proudu plavou kachny.

Výběr pilota určuje vzhled, animace a výzbroj po každém oživení. Týmy mají stejné herní parametry pohybu, zdraví a hitboxy.

| Pilot | Výchozí zbraň | Startovní munice |
| --- | --- | --- |
| Vektor | Pulse | 24 nabitých, neomezená rezerva |
| Bašta | Scatter | 4 náboje |
| Přízrak | Lance | 2 náboje |
| Poštolka | Arc | 24 nábojů |

Specialisté mají **Pulse** jako záložní zbraň na **1**. Munici do výchozí speciální zbraně doplní jejím sebráním v aréně. Ve volných slotech každého týmu se střídají čtyři sestavy botů. Vyřazená postava po dopadu chvíli zůstane ležet.

## Vozidla Meridian Basin

- **Skimmer** je lehký stroj s rychlou energetickou střelbou a dobíjejícím se turbem na **Shift**.
- **Bastion** je pomalejší obrněnec s dělem. Jeho projektil potřebuje čas na dolet a po dopadu zasáhne okolí. Kryt tlumí účinek výbuchu.

Klávesa **E** nastupuje a vystupuje. **W/S** ovládá jízdu vpřed a vzad, **A/D** zatáčí, mezerník brzdí a myš míří zbraní nezávisle na směru jízdy. Před výstupem zpomal. Po zničení se stroj po krátké prodlevě vrátí do doku. Pohon obou vozidel má lokálně syntetizovaný zvuk.

## Pohyb a výbava

V arénách můžeš bez ohledu na pilota získat **Scatter**, **Lance**, **Arc** i **Rift**. Jeho energetická koule má dobu letu. Dopad odhodí blízké soupeře a kryt zachytí účinek výbuchu. První sebrání zbraň rovnou vybaví. Další sebrání doplní munici. Po vyřazení se obnoví tvoje startovní výzbroj. Scatter může místo rozptylu vystřelit soustředěnou dávku na delší vzdálenost za dva náboje. Každý pilot má na jedno oživení jeden impulsní granát s krátkou pojistkou a odrazy od stěn.

Zbraně mají vlastní zpětný ráz, výšleh, animaci podavače a zvuk manipulace. Diodový ukazatel na modelu odpovídá stavu nabité munice. Po vyčerpání nábojů specialistu HUD připomene přepnutí na Pulse.

Oranžové odpalovací podložky tě vynesou na vnější lávky. Můžeš je podběhnout nebo pokračovat dvojskokem na vnitřní lávky pod reaktorem. Uvolněný stojan ukazuje odpočet v posledních devíti sekundách před návratem předmětu.

V nižších patrech najdeš léčení a štít. Na vrcholu centrálního relé je **Overcharge**, který na 18 sekund zvýší poškození. K relé vede cesta přes nižší plošiny u jeho paty. Zbraně a bonusy se po sebrání po čase obnoví. Čtyři krátké výstřely a jeden zvuk sebrání výbavy pocházejí z ElevenLabs a jsou uložené v `public/sfx`. Prohlížeč při hraní přehrává soubory lokálně a směruje střelbu podle pozice soupeřů. Pohyb, potvrzení zásahu a Rift mají lokálně syntetizované efekty.

## Režimy

| Režim | Pravidla | Cíl | Čas |
| --- | --- | --- | --- |
| Team Deathmatch | Tým získá bod za vyřazení soupeře. | 35 bodů | 6 min |
| Deathmatch | Každý hraje za sebe. | 20 vyřazení | 5 min |
| Dominance | Obsazujte stanice A, B a C. Každá držená stanice dá týmu bod za sekundu. | 150 bodů | 7 min |
| Capture the Flag | Dones soupeřovu vlajku na vlastní základnu, zatímco je tvoje vlajka doma. | 5 vlajek | 8 min |
| Obléhání | Propoj uzly od vlastní základny až k soupeřovu jádru. Hraje se v Meridian Basin. | Poslední přeživší jádro | 12 min |

Po vypršení času rozhoduje skóre. Další kolo začne automaticky. Prázdné místnosti se zavřou a výsledky zůstávají v paměti pouze po dobu zápasu.

## Obléhání v Meridian Basin

Každý tým začíná s vlastním jádrem a propojeným domácím uzlem A, B nebo C. Pěšáci obsazují relé D, E a F a postupují po souvislé trase z domácího uzlu. Propojená trasa až na soupeřův domácí uzel otevře jeho jádro palbě útočícího týmu. Přerušení spojení vrátí štít. Dva týmy v kruhu zastaví obsazování. Vozidla mohou krýt obsazování a střílet na odkryté jádro.

Jádro má 720 HP. Tým se zničeným jádrem se vrací v dalším kole. Vyhrává poslední přeživší tým. Po 12 minutách rozhoduje stav jader, při shodě počet propojených uzlů.

Pilot má 150 HP. Pulse vyřadí nechráněného pilota sedmi zásahy, léčení doplní až 65 HP a štít přidá až 50 bodů.

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

Ilustrace lobby `public/orbit.jpg`, textura krytů `public/bulkhead.jpg`, textura paluby `public/deck.jpg`, povrch slévárny `public/foundry-deck.jpg`, keramická textura `public/armor.jpg`, terén údolí `public/basin.jpg` a louka `public/meadow.jpg` vznikly vestavěným nástrojem ImageGen. Modely pilotů a zbraní se skládají z 3D geometrie za běhu.

**Ilustrace lobby**

> Use case: stylized-concept. Asset type: wide lobby background for TRIAS, an original competitive sci-fi browser arena game. A massive triangular industrial orbital station floating in deep space over the far curved limb of a moody charcoal planet. An asymmetric Y-shaped ring of layered matte gunmetal trusses and ribbed armored industrial modules, with three distinct sparse light circuits in ice turquoise, burnt amber and lilac. Main station occupies the right two-thirds of a cinematic landscape image; keep the left third dark with ample negative space for live HTML text, no text in image. Realistic hard-surface 3D concept art with precise bevels, strong silhouette, hard rim light, subtle tungsten window lights, pitch-dark starfield and hazy planet horizon low in frame. High contrast, cinematic, premium competitive game art. No people, no weapons, no typography, no symbols, no logos, no watermarks.

**Textura krytů**

> Use case: stylized-concept. Asset type: one square front-facing color map texture for reusable hard-surface wall panels of the TRIAS orbital FPS arena. Perfectly orthographic close-up of one flat brushed gunmetal ship bulkhead panel, edge to edge, aligned to square image, cool blue-gray slate hues, recognizable trapezoidal recessed plates and clean panel seams with a few tiny pale hazard-orange paint dashes and bolt heads. Modern realistic spacecraft material, matte mid-dark but legible, coarse features that remain clear at small scale. Uniform diffuse neutral lighting suitable for a color map, minimal baked shadow, no perspective, no vignette, no background, no letters, no numbers, no brand marks, no glowing lights.

**Textura paluby**

> Use case: stylized-concept. Asset type: square tiling color texture for the playable metal floor of the original TRIAS orbiting industrial sci-fi arena. True flat orthographic downward view, no perspective, seamless repeatable square image, large clean desaturated charcoal-blue graphite alloy deck plates arranged as one broad panel with very thin seams near its edges, tactile matte ceramic-metal grip, restrained fine micro-dot traction and weathering, recessed corner bolts, some subtle linear brush scratches. Attractive worn industrial realism, mostly medium dark gray-blue, vary roughness visually without very dark patches. The middle should be calm and legible beneath players and neon UI, no objects, no background, no text, no footstep marks, no symbols, no decals, no colored stripes, no strong shadows, no glowing elements.

**Povrch Vanta Foundry**

> Use case: stylized-concept. Asset type: square repeatable albedo texture for the hard floor of Vanta Foundry, an original sci-fi arena game. Perfectly perpendicular top down orthographic view, a cast gunmetal slab with heat oxidized satin steel, muted brown charcoal, brushed scuff marks and subtle amber copper staining in thin seams, understated shallow rows of square perforations and ground-down rivets along the borders, clear central surface. Calm integrated PBR-like dark warm neutral texture, even diffuse illumination, no perspective or shadows or white glare, no typography, no luminous colors, no environment or objects.

**Keramická textura**

> Use case: stylized-concept. Asset type: seamless square grayscale albedo for repaintable storm-worn ceramic sci-fi pilot armor. Flat orthographic capture of a single broad pale gray ceramic surface, extremely subtle woven micropattern, directional dry wear, faint abrasion and minute graphite speckling with a few small subdued hairline scratches, clean modern sci-fi wear rather than random dirt. Uniform average gray and even diffuse light, no panel layout, no cracks, no bolts, no seams, no borders, no symbols, no markings, no characters.

**Terén Meridian Basin**

> Use case: stylized-concept. Asset type: square seamless terrain albedo texture for Meridian Basin, a fictional orbital mining yard arena. Orthographic top-down fine compressed basalt regolith with slate-gray volcanic grit and thin layered desaturated rust-brown abrasion in the dust. Hard-packed and traversable, dry and matte, compact quiet middle tones, subtle mineral granules, a few faint abrasion streaks, no rocks casting shadows, no boulders, no gravel patches large enough to look like obstacles, no graphic markings, no text, absolutely no environment or perspective. Uniform soft diffuse light for a repeatable game material.

**Louka Mechového ostrova**

> Use case: stylized-concept. Asset type: seamless square ground albedo for the playable grass of an original whimsical tiny-planet arena. Direct perpendicular orthographic view of soft short meadow grass and velvety emerald moss, warm sunlit olive and fresh spring green, very finely scattered pale clover specks, tiny rust flecks, painterly hand-brushed texture with gentle organic variation and one restrained consistent brightness. No tall grass, no foliage casting shadows, no bushes, no large flowers, no perspective, no environment, no paths, no labels or graphic shapes. A quiet repeatable surface that stays legible in a 3D game.

</details>

## Ověření

`npm test` ověřuje pohyb pod lávkami, přikrčení, úhyb, granát, Rift, hnízda, vlajky na vyšším patře a provoz vozidel v údolí.
