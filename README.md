# TRIAS

Původní 3D multiplayerová FPS na orbitální stanici Relay. Zápas se odehrává pod prasklým reaktorem mezi tyrkysovou turbínou ION, oranžovými chladiči EMBER a fialovými anténami VIOLET. V týmových režimech proti sobě hrají ION, EMBER a VIOLET, každý až se čtyřmi hráči. Místnost pojme 12 lidí. Ve výchozím nastavení doplňují volná místa boti, kteří je automaticky uvolňují příchozím hráčům. Mapu, režim a nastavení botů určuje zakladatel místnosti. Každý hráč si sám vybírá pilota.

## Spuštění

### Hra

TRIAS se hraje v nativním klientu Unity. Otevři projekt `unity-vfx-preview` v Unity 6.3 LTS a zvol **TRIAS → Open full game**, nebo spusť sestavenou aplikaci. Ovládání, nastavení a pozvánky popisuje [návod pro Unity](unity-vfx-preview/README.md).

### Server

Online zápas řídí samostatný server Node.js 22 nebo novější.

```sh
npm ci
npm start
```

Server poslouchá na portu 3000 a vypíše svou LAN adresu. Na stejné síti ji hráči zadají v klientu nebo použijí odkaz z tlačítka **Pozvat**. Port lze změnit příkazem `PORT=8080 npm start`. Server neobsluhuje žádnou webovou hru: WebSocket `/play` přijímá jen nativního klienta, `/join/<kód>` otevře pozvánku v aplikaci a `/health` hlásí stav.

Pro hraní přes internet spusť server na hostingu, který umí provozovat Node.js a WebSockety, a nasměruj na něj cesty `/play` a `/join/`. Při použití reverzní proxy zachovej původní hlavičku `Host` a předávej WebSocket upgrade. Repo na GitHubu slouží k uložení zdrojáků.

Server simuluje 30 kroků za sekundu a po každém kroku pošle kompaktní snímek stavu. Klient vlastní pohyb předpovídá lokálně; server provede každý jeho pohybový příkaz právě jednou a ve stejném pořadí. Pravidla pohybu, map a sítě sdílí server s nástroji ve složce `shared/`. Zobrazený ping měří cestu zprávy na server a zpět. Výstřel vyhodnocuje server ve svém aktuálním kroku.

Vercel Functions podporují WebSockety, ale dvě připojení mohou skončit v různých instancích a spojení mají časový limit. Současné místnosti existují v paměti jednoho procesu. Pro samostatný deploy na Vercel by hra potřebovala sdílený stav, jednoho koordinátora simulace na místnost a obnovení spojení. [Dokumentace Vercelu](https://vercel.com/kb/guide/do-vercel-serverless-functions-support-websocket-connections)

## Mapy a piloti

- **Relay Station** má tři paluby kolem otevřené šachty. Servisní trasy vedou ke schodištím, prostřední galerie spojuje body Dominance a horní lávky nesou Rift a Overcharge.
- **Vanta Foundry** je slévárna s tavicími kanály, bočními mosty a nízkými servisními průchody pro přikrčené piloty. Po obvodu jsou tři pevná energetická hnízda. Mají omezený úhel míření a při dlouhé střelbě se přehřívají. Klávesa **E** obsluhu přepíná.
- **Meridian Basin** měří 112 × 112 herních metrů. Široký okruh a tři paprsky mezi doky spojují tři sektory. Barevné servisní pruhy, odpalovací podložky a horní lávky dávají pěchotě kratší cestu a přístup ke zbraním. Antény v Dominanci a vlajky v CTF řeší pěchota. Zelené opravárenské plochy opravují obsazené vozidlo po zastavení. Minimapa ukazuje okruh, cíle a spojence.
- **Kordilera** je kulatá pěší aréna na malé plovoucí planetě. Písková pěšina vede přes jižní hřeben k vyhlídkám nad dvěma vodopády. Na hřebeni leží Rift. V údolí vede přes řeku trojice dřevěných mostků a brod, který zpomaluje běh.

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

Po vyřazení **kill cam** krátce zrekonstruuje zásah ze záznamu serveru. **Mezerníkem** kameru přeskočíš. V týmových režimech pak sleduješ živého spoluhráče a klávesou **E** mezi nimi přepínáš. Respawn dál určuje server.

Skok stisknutý nejvýš 0,1 s před dopadem se provede hned po dopadu a úhyb stisknutý těsně před koncem obnovy se provede, jakmile je k dispozici. Po sejití z hrany můžeš ještě 0,1 s skočit ze země a dvojskok ti zůstane. Ve vzduchu tě řízení nebrzdí víc, než kdybys pustil klávesy; tah proti směru letu brzdí dál.

Oranžové odpalovací podložky tě vynesou na vnější lávky. Můžeš je podběhnout nebo pokračovat dvojskokem na vnitřní lávky pod reaktorem. Uvolněný stojan ukazuje odpočet v posledních devíti sekundách před návratem předmětu.

V nižších patrech najdeš léčení a štít. Na vrcholu centrálního relé je **Overcharge**, který na 18 sekund zvýší poškození. K relé vede cesta přes nižší plošiny u jeho paty. Zbraně a bonusy se po sebrání po čase obnoví.

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


## Ověření

`npm test` ověřuje pohyb pod lávkami, přikrčení, úhyb, granát, Rift, hnízda, vlajky na vyšším patře a provoz vozidel v údolí. `node tools/kinetic-lab.mjs` měří zrychlení, skoky, úhyby, toleranci vstupu a korekce predikce při simulovaném zpoždění sítě. `node tools/kinetic-parity/run.mjs` porovná pohyb Unity klienta krok po kroku s pravidly serveru (vyžaduje .NET 8 SDK).
