# TRIAS W7 — Dynamická režie zápasu

Režie dvakrát za sekundu vyhodnocuje aktivitu střelby, způsobené poškození, vyřazení, dobu od posledního kontaktu, stav pilotů, pohyb, shluky botů a stav cílů. Vyhlazená intenzita je v rozsahu 0–1. Při nástupu musí překročit 0,29 pro bojovou fázi a 0,67 pro vrchol. Vrchol končí po odeznění boje nebo nejpozději po 8 sekundách. Následuje nejméně 6,5 sekundy zklidnění.

## Jak zapnout režii

- **Trénink offline** v lobby otevře Relay Station. V úvodním panelu můžeš nezávisle nastavit **Dynamické hlídky** a **Živou arénu**. Číselný seed do devíti míst vytvoří předvídatelnou posloupnost kol. Prázdné pole volí nový seed. Změna seedu v průběhu rozehraného kola platí od dalšího kola. Pohyb hráče i profil zbraní vychází z běžných pravidel tréninku.
- **Online** jsou volby serveru ve výchozím stavu vypnuté. Režie hlídek je určena pro casual zápasy s boty a nejvýše jedním člověkem. Připojení druhého člověka ji vypne do konce kola. Stav režie lze zkontrolovat v herním menu po stisku **Esc**.
- **Horda** má samostatný přepínač a funguje i v kooperaci. Nepřátelé přicházejí z existujících nástupních míst v kratších sledech s malou mezerou na přeskupení. Počet nepřátel, typy, život, síla útoku, limity souběhu, délka přípravy i vítězné podmínky používají původní pravidla.

| Přepínač serveru | Výsledek |
| --- | --- |
| `TRIAS_DIRECTOR_CASUAL=1` | Hlídky, rozestupy a volba taktického dosahu v sólových zápasech s boty. |
| `TRIAS_DIRECTOR_HORDE=1` | Seedované rozestupy příchodu nepřátel v Hordě. |
| `TRIAS_DIRECTOR_PRESENTATION=1` | Kosmetický rytmus pro připojené Unity klienty. |
| `TRIAS_DIRECTOR_SEED=271828` | Základ pro reprodukovatelné složení a rozhodování kol. Nastavená obtížnost botů platí dál. |
| `TRIAS_DIRECTOR_DEBUG=1` | Server loguje fázi, intenzitu, hranice, rozložení rozkazů, největší shluk a aktivní volby. V Unity Editoru a vývojovém buildu zobrazí **F4** diagnostiku offline režie. |

```sh
HOST=127.0.0.1 TRIAS_DIRECTOR_CASUAL=1 TRIAS_DIRECTOR_HORDE=1 \
TRIAS_DIRECTOR_PRESENTATION=1 TRIAS_DIRECTOR_SEED=271828 npm start
```

## Co režie ovlivňuje

Taktické volby pracují s existujícími body mapy, last-known pozicemi z vlastní percepce botů a s rozestupy spojenců. Hra v klidnějších fázích dává přednost bezpečnému postavení a odstupu, při kontaktu se hlídky přesouvají blíž. Osobnost ovlivňuje individuální odezvu. Obtížnost, reakční časy, viditelnost, balistika a rozhodčí pravidla zápasu se řídí původními systémy.

Kosmetické události mají krátké upozornění na HUD. Relay rozsvítí reaktor, Vanta zesílí tavicí komoru, Meridian zvýrazní těžební ramena a přelet konvoje a v Kordileře se zvýrazní odlesky hladiny. Online se dekorace a atmosféra odvozují z veřejného času, seedu a veřejné přípravy Hordy. Intenzita boje, informace z percepce a souřadnice cílů do kosmetického paketu nevstupují. Lokálně se atmosféra jemně promíchá i podle vlastních potvrzených zásahů.

## Zvuk a čitelnost frakcí

Úskok, dvojskok, plošiny, ozvěna, granát a potvrzení zásahů používají tlumené mechanické rázy, vzduch a kovové přechody. Rádio má vlastní prioritní kanál. Po dobu repliky se palba a atmosféra mírně stáhnou. V nastavení lobby tlačítko **Vyzkoušet rádio** střídá ženský a mužský hlas. Týmové hlášky se ozývají od spojeneckých botů, včetně offline tréninku. Hlasitost rádia lze ovládat samostatně.

ION má tyrkysové pláty a dvojici šikmých lamel, EMBER oranžové pláty a širší ramena, VIOLET fialové pláty a vysoký profil batohu. Při přímém namíření Unity ukáže jméno a frakci viditelného pilota nebo označení spojence. Kontrola trasy pohledu končí o kryt.

## Ověření a playtest

```sh
npm test
node tools/bot-scrimmage.mjs
TRIAS_DIRECTOR_CASUAL=1 TRIAS_DIRECTOR_HORDE=1 TRIAS_DIRECTOR_SEED=271828 node tools/bot-scrimmage.mjs
```

Ve výpisu ze scrimmage porovnej počet vyřazení, nejdelší mezeru mezi zásahy, dobu v jednotlivých fázích a vzorky se shlukem nejméně pěti botů ve stejné buňce. V Unity vyzkoušej obě varianty rádia během souvislé palby. Porovnej stejně vyzbrojené piloty z různých frakcí na krátkou i střední vzdálenost, potom zkontroluj zhasnutí označení za krytem. Délku zklidnění a srozumitelnost zvuku ještě posuď při několikakolové hře s lidmi a ve vyšších vlnách Hordy.
