# TRIAS · Fractured Ceramics

Keramické pláty přemosťují malé prostorové zlomy. Grafit nese mechaniku, kartáčovaná slitina spojuje namáhané části a jantar označuje servisní uzly. Světlo vystupuje ze štěrbin a činných vodičů. Na dálku je vidět celkový tvar, na střední vzdálenost účel a zblízka opotřebení.

## Tři měřítka

- **Dominanta** má rozpoznatelnou siluetu a volný prostor kolem sebe. Relay používá velké rozpojené prstence, Foundry nízkou pec s tepelnými štěrbinami, Basin vysunuté čelisti těžebních ramen.
- **Herní prostor** tvoří souvislé, dobře čitelné objemy krytů a průchodů. Keramické fasády se dělí na tmavý nosník, světlý plát a krátké značení. Zásahy a obrysy pilotů si zachovávají vizuální přednost.
- **Detail** patří na hrany, do spár a k součástem v pohybu. Široké matné plochy ponechávají prostor pro siluety a značky. Jemné mapy opotřebení mají být čitelné zblízka.

## Piloti a výzbroj

| Pilot | Rozpoznávací znaky |
| --- | --- |
| Vektor | Střední profil, dvě oddělené desky hrudi a šikmý průzor |
| Bašta | Široká ramena, tlusté izolované desky, tři senzory a dvojitý zádový modul |
| Přízrak | Úzký asymetrický trup, svislý senzor a vysoká jednostranná anténa |
| Poštolka | Dělený šípový průzor, šikmé desky hrudi a dozadu stažené letové ploutve |

Mužská a ženská varianta vycházejí ze stávajících rigů a sdílejí pohybové klipy a body úchopu. Profilované sítě tvoří zvlášť helmy, trup, končetiny, boty a rukavice. Detaily na jedné kosti se sdružují podle materiálu a způsobu vrhání stínu. Siluety doplňují hydraulické vzpěry Bašty, zadní stabilizátory Poštolky, jednostranné pouzdro Přízraku a komunikační modul Vektora. Ramenní a holenní pláty se pohybují spolu s kostmi. Herní hitbox zůstává stejný. Hangár zobrazuje stejnou geometrii pilota a zbraně jako zápas.

| Výzbroj | Konstrukce |
| --- | --- |
| Pulse | Krátké tělo se samostatnou dělenou přední komorou a otevřeným hledím |
| Scatter | Dvě široké oddělené komory, světlý plášť nad dvěma tmavými čely |
| Lance | Široké prázdné místo mezi dvěma úzkými lištami a podélná optika |
| Arc | Krátké asymetrické dvojče článků s viditelnými kontakty |
| Rift | Jádro uprostřed tří keramických segmentů a vnitřních kovových lamel |

Přebití dál probíhá přes stávající animaci servisu a odpovídající body úchopu. Přední komora je lehce pohyblivá při výstřelu, přebití a aktivním Momentum Chain.

## Frakce a efekty

| Frakce | Tvar a barva |
| --- | --- |
| ION | Azurová ramena a břicho, trojice šikmých segmentů, pravidelné dělení hran ozvěny |
| EMBER | Oranžové sekce a pruh na helmě, dvojité klíny, úhlové dělení hran ozvěny |
| VIOLET | Fialové nakloněné pláty a vysoký zádový díl, dutý kosočtverec, zvlněné dělení hran ozvěny |

Combat Echo využívá normály a lokální výšku pro převážně celistvý povrch s úzkými štěrbinami, hranovým nábojem a dělením podle frakce. Poslední část životnosti používá dither. Všechny varianty drží stejnou velikost mezer a stejný průběh životnosti. Barva a vzor hrany se mezi frakcemi liší. Fáze vzniku stahuje díly k tělu, výstřel vytváří krátký výboj a rozbití rozevírá segmenty. Zásahy do štítu kreslí lokální šestičlennou reakci v místě kontaktu. Vzdálený respawn skládá tři nízké vrstvy směrem k tělu. Keramické pláty a prach se uvolní po dopadu pilota a jeho ustálení na zemi.

## Arény

- **Relay Station** kombinuje trojici vystředěných rozpojených stupňů, axiální světelný sloup, odstupňované fasády, vegetaci na servisních pylonech a obří osvětlenou planetu za obzorem.
- **Vanta Foundry** má nízkou tepelnou komoru se šesti vrstvenými bloky a teplým světlem ze spojů. Zatmění, měděné odstíny a těžké výměníky určují atmosféru.
- **Meridian Basin** má otevřený prostor mezi cíli, vytěžené žíly a ramena s rozpojenou tříčelisťovou hlavou. V pozadí zůstává nákladní trať a planeta s prstenci.
- **Kordilera** staví na terénu, vodě a zeleném obzoru. Drobné keramické přepážky označují místa, kde lidé dřív regulovali tok.

## Původ a implementace

- Nové povrchy těží ze sdílených Standard materiálů Built-in pipeline. Textura keramického zrna je původní procedurální asset TRIAS generovaný z krátkých úbytků v glazuře a jemné plošné změny hodnot. Různé rozměry zbraní, zaoblené pancířové díly a prstencové segmenty používají sdílené sítě. Vizuální doplňky vznikají bez kolizí.
- Pozadí Relay používá původní vykreslení atmosféry, souše a oblačnosti v existujícím skybox shaderu. Vzdálené lodě, krajina a vegetace využívají stávající obsah projektu. Licence jsou v adresářích [Pilots](Assets/Resources/TRIAS/Kit/Pilots/License.txt), [Weapons](Assets/Resources/TRIAS/Kit/Weapons/License.txt), [Nature](Assets/Resources/TRIAS/Kit/Nature/License.txt) a [Space](Assets/Resources/TRIAS/Kit/Space/License.txt).
- VFX události recyklují existující fond 16 objektů a respektují posuvník vizuálních efektů. Momentum používá vlastní posuvník. Světlo prostředí zůstává v existujícím systému bloom, mlhy a paprsků.

## Relay Station · světlo, materiál a kompozice

Relay je ověřovací mapa pro druhou vizuální iteraci. Profil vzniká v `TriasWorld.RelayLight.cs` při sestavení mapy. Běží v Built-in Forward, v Gamma a se stávajícím MSAA. Směr hvězdy sdílí světlo, stíny a procedurální planeta. Teplé přímé světlo doplňuje chladný tříbarevný ambient. Soft shadows na Relay končí nejpozději v 74 m a nižší nastavení kvality jim může ponechat kratší dosah. Středový reaktor má vlastní slabý lokální zdroj světla. Po opuštění mapy se obnoví předchozí nastavení kamery, stínů a odrazů.

- **Keramika** má slabou mikrostrukturu a širší odlesk. **Grafit** je tmavý neutrální materiál s póry a kratším odleskem. **Slitina** lokálně používá směrovou kresbu. Architektonická slitina má klidnější odezvu kvůli měřítku velkých fasád. Původní mapy `RelaySurfaces` mají linear data mapu s metalicitou v R a hladkostí v A a samostatnou importovanou normálovou mapu. Síly normál najdeš v `ConfigureRelaySurfaces`.
- Jedna sonda 64 px snímá při vstupu jen oblohu. Statický kontaktní stín pod kryty je jeden sdružený mesh. Post-processing používá stávající čtvrtinové buffery. Žádné SSR, SSAO ani další kamera zbraně. Posuvník efektů omezuje atmosférické průchody a strop jasu zůstává neutrální i při hodnotě nula.
- V popředí se střídají tmavé nosné plochy, samostatné technické obklady a čitelné hrany. Nižší článek reaktoru je vidět z více přístupů. Vnější orbitální límec a klimatizace stanice leží vizuálně za hratelnými stěnami. Reaktor, hvězda a planetární okraj se podle polohy hráče různě překrývají.
- Pulse na Relay drží keramickou přední objímku ve volném prostoru kolem kovového vývrtu. Pole je vidět na krátkých kontaktech. Stávající úchop, animace, recoil i síťové chování se používají dál. Vektor dostal vrstvená předloktí a hlubší límec s kovovými vložkami.

### Kritika iterací

Výchozí záběr z úrovně očí měl přepálené pláty a málo detailu pod gantry. Samotná expozice vrátila hrany na světle, ale prvotní stín u Vektora příliš potlačoval grafit. Jemnější normály s odrazy přidaly kresbu. Směrový povrch působil na velkých mostních hranách pruhovaně, proto je na nich hladší architektonická slitina. Menší počet opakovaných fasád a nová silueta v pozadí pomohly oddělit hloubkové vrstvy. První změna planetárního směru stále působila pruhovaně, finální mapa planety používá lokální projekci bez podélného švu v záběru.

Nejčitelnější část je osvětlený bok krytu vedle zastíněné fasády. Vhodně funguje i odstup matné keramiky a tmavého vývrtu Pulse. Pod prvním mostem zůstává v pohledu z výšky očí z reaktoru vidět hlavně horní část a pohled proti hvězdě dál zjednodušuje tmavý grafit na vzdáleném pilotovi. Delší hra proti všem frakcím je další podklad pro schválení širšího nasazení. Paletu a techniku lze znovu použít v samostatných řezech.

Snímky po jednotlivých krocích, srovnání stejné kamery, herní záběr a lokální buildy jsou v `output/relay-art-direction/`. Skilly projektu jsou v [Unity projektu](.agents/skills/). Technické podklady [Directional Lights](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting-light-sources/components/lighting) a [Reflection Probes](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/reflections/using-reflection-probes).

## Piloti · modely z Blenderu

Editovatelné modely jsou v [Art/](Art/). Obsahují jednotlivé pláty, tlakové obleky, materiálové sloty a scénu pro náhled. [build-vektor.py](../tools/blender/build-vektor.py) vytváří Vektora a [build-pilots.py](../tools/blender/build-pilots.py) Baštu, Přízraka a Poštolku. Spouští se přes `Blender --background --python`. Export dává pro každou postavu jeden FBX se 17 částmi. Unity očekává metry, Y nahoru a +Z vpřed. Převod je zapečený ve FBX.

`BlenderPilotSuit` používá skutečné délky končetin původní kostry. `BlenderParts` dosazuje sdílené povrchy mapy včetně frakčních barev. Modely se používají v zápase, hangáru a při vykreslení Combat Echo. Bašta má široké břemenné pláty a zdvojený zádový modul, Přízrak jednostrannou anténu a diagonální hrudní rám, Poštolka šípové průzory a letové stabilizátory. Mužská a ženská varianta se liší stavbou původního rigu a mírným přizpůsobením dílů.

## Zbrojnice · komory, závěry a manipulace

[Arsenal.blend](Art/Arsenal.blend) obsahuje pět zbraní. [build-armory.py](../tools/blender/build-armory.py) exportuje čtyři samostatné skupiny pro každou zbraň. Tělo je pevnou částí, komora se odsouvá, závěr se pohybuje podle zbraně a vyměnitelný zásobník nebo článek používá stávající servisní bod. Unity sdílí stejné sítě v pohledu hráče a ve třetí osobě. Barva kontaktů vychází z herního materiálu.

| Zbraň | Detail |
| --- | --- |
| Pulse | Otevřený vývrt ve dvou oddělených keramických objímkách, průmyslová žebra a vysouvací kazeta |
| Scatter | Dvě vybrané hlavně, přerušované vedení a odklopný uzávěr s dvojicí nábojů |
| Lance | Dlouhé izolované kolejnice, úzká optika, závěr a vyjímatelná jehlicová kazeta |
| Arc | Dvě nestejně dlouhé elektrody, kontaktní žebra a výsuvný článek |
| Rift | Jádro mezi třemi keramickými segmenty, vnitřní vložky a odklápěcí uzávěr |

Ramena třetí osoby počítají úchop v souřadnicích nadřazené kosti. Rozdílné měřítko rigů a zmenšení výšky při skrčení tak nedeformují délky použité při řešení úchopu. Body výstřelu, úchopu a servisní časy zůstávají v existujícím systému.

## Rozšířené arény a modely prostředí

Kordilera má v průměru 95,6 m. Tři dřevěné přechody propojují údolí pod hřebenem. Řeka se rozšiřuje do dvou vodopádů. Povrch kombinuje vlhký břeh, trávu, půdu a odkrytou horninu podle sklonu. Siluetu ostrova tvoří více vrstev útesu, různé skalní výchozy a skupiny jehličnanů. Dekorace používají stejný výškopis jako herní pohyb.

Relay Station má 68 m na šířku, Vanta Foundry 76 m a Meridian Basin 128 m. Okrajové trasy dostaly tlakové přepážky, výpusti pecí, chladicí lože nebo bloky rudy. Klíčové body a krytí uprostřed hřišť vycházejí ze stávajícího rozvržení. `Art/MapProps.blend` obsahuje editovatelné modely a `tools/blender/build-map-props.py` reprodukuje FBX import. V Unity modely vycházejí ze stejných rozměrů krytí jako autoritativní kolize.

Po změně souřadnic, terénu nebo zbraní se katalog Unity obnovuje příkazem `node tools/export-unity-world.mjs`. Režim `--check` ověřuje, že katalog odpovídá sdíleným pravidlům.

Dřevěné mosty mají různě sesazené fošny a samostatné kovové spoje. Útes používá světově promítanou texturu zvětralé horniny a skály mají odlomené vrstvy. Boční stěny Relay, Foundry a Meridianu mají tlakové uzávěry, výměníky a regály pro rudu. Čtyři opakovatelné povrchy reprodukuje `python3 tools/make-map-surfaces.py`.

## Relay Station · materiály a prostor pod konstrukcí

Osvětlení nasměruje teplý klíč na keramiku, zatímco odraz oblohy a měkké stíny drží chladnější tmavé tóny. Nové albedo keramiky doplňuje existující mapu normál a odezvy povrchu. Kompozici mění atmosférická textura planety, úzký zářící sloup reaktoru, členité fasády a plavidlo se samostatnými tepelnými štíty. Dvě prosklené sekce odhalují vnější příhradovinu a zavěšené servisní prvky. Původní kolizní stěny zároveň vymezují pevné zasklení. Podlahové spáry a mříže jsou sdružené do statických dávek.

Šest lávek, vysoké pilíře a pěší kryty používají ručně tvarované souvislé sítě z `Art/RelayStructure.blend`. Model obsahuje profil paluby, zapuštěný servisní žlab a stupňovaný přechod z patky do pláště pilíře a souvislou tlakopřepážku pro pěší kryt. Export reprodukuje `tools/blender/build-relay-structure.py`.

## Relay Station · provozní trasy a šachta

Soutěžní Relay tvoří tři výškové úrovně kolem nosné páteře reaktoru. Servisní paluba v 0 m obepíná otevřenou šachtu. Galerie v 3,3 m vede kolem otvoru a drží tři stanice pro Dominanci. Ve výšce 6,6 m se čtyři lávky sbíhají na ochozu u reaktoru. Ze startu je možné projít mezi všemi úrovněmi po schodech bez skoku. Čtyři boční vrhače nabízejí rychlejší nástup na lávky. Zbraně na galerii a Rift s Overcharge na lávkách dávají důvod postupovat nahoru.

Pod palubami navazují příhrady na stěny šachty a nosnou páteř. Průchozí povrchy a zábrany se exportují ze stejných dat, z nichž vychází serverový pohyb, střelba a trasy botů. Delší pád končí standardním respawnem. Zisk bodu v Dominanci je vázán také na výšku stanice. Horda a výcvik používají servisní variantu Relay s původními staveništi a cvičnou drahou.

## Living Megastructures

Vanta odvozuje půdorys od výroby fázové keramiky. Chladicí kanál míří přes licí mosty na přijímací závod. Nad ním drží dva stávající výměníky patu výhně. Samostatné prstence tváří polotovar a přepravní závěs ho po tepelném cyklu přesune k přijímači. Ve spodním kanálu se mění odstíny proudícího materiálu od tmavé krusty po světlé proudnice. Poloha kanálu a jeho poškození vycházejí ze stávající autoritativní mapy.

Relay používá v pozadí dvě odlišně skloněné základny orbitálního interferometru. Meridian odkrývá jednu souvislou zlomovou rovinu, z níž se při těžbě oddělují vrstvy se stejnou geologickou kresbou. Na Kordileře bývalé zařízení pro práci s počasím vystupuje až za hřebenem a udržuje u sebe několik zavěšených vodních čoček.

- Sestavy se sdružují do jedné sítě na materiál a nevytvářejí kolize. Vzdálené kulisy nevrhají stíny. Perioda jednotlivých provozů je 78, 107, 94 nebo 121 sekund. Kde server poskytuje čas a seed režie, pracuje klient s nimi. Jinak používá svůj čas od načtení arény. Posuvníky pohybu, světelných efektů a atmosféry omezují příslušné složky.
- Pedestaly a značky dostupnosti jsou prostorové a respektují zákryt. Na podstavci jsou buď stávající zbraně se zvětšenou siluetou, schránka s křížem, tříplátový štít nebo zavěšené jádro Overcharge. Jejich dostupnost řídí stejný stav zápasu.
- Zvuk výhně je autorská procedurální smyčka. Dva krátké zvuky sebrání a hudbu hangáru dodal ElevenLabs. Smyčka hangáru má lokální střih na 23 sekund s půlsekundovým přechodem. Hudba používá vlastní hlasitost, ztišuje se pod rádiem a při přechodu do zápasu odezní. [ElevenLabs Sound Effects](https://elevenlabs.io/docs/api-reference/text-to-sound-effects/convert) · [ElevenLabs Music](https://elevenlabs.io/docs/api-reference/music/compose/)

### Čitelnost cílů režimů

- Kapacitor Obléhání má trojici keramických clon. Po odkrytí zmizí prstence štítu a clony se odsunou, aby byl střed vidět. Menší jádro Hordy používá trvale přístupnou variantu. Nízký stav energie tlumí frakční barvu do měděného tónu.
- Vlajku tvoří přenosný keramický praporec se znakem na obou stranách a samostatná základnová objímka. Při nesení drží menší siluetu za ramenem pilota.
- Fázový terminál vyznačuje hranici stanice. Vnitřní oblouk ukazuje průběh obsazování a jantar zvýrazní spor i bez čtení nápisu. Při vypnutém pohybu zůstávají tyto informace k dispozici.

### Přechody Kordilery a krátké strany podpěr

Na archivním záběru `output/architectural-dna/player-after/kordilera-crossing.jpg` končí lávka v kamenném bloku a oba břehy lemuje světlá linka. Vstup na lávku má číst hráč už z protějšího břehu. Dvojici deflektorů proto umísťujeme po diagonále mimo šířku mostovky. Mokrá zemina přechází do olivové mělčiny a hlubšího zelenomodrého proudu. Sluneční odlesk používá stejný směr jako ostrovní obloha. Ve studii `output/art-direction-pass/water-study.jpg` lze porovnat původní a novou kresbu na plochém vzorku. První tmavší varianta potlačila i střed proudu, proto má finální vzorek vyšší odraz na hřbetech vlnek.

Zblízka má být poznat, jak zařízení přenáší sílu. Ve Vantě krátkou stranu podpěry zatěžuje nízká žáruvzdorná čelist, v Meridianu ji protíná vysoká měřicí konzola. Na Kordileře stejný princip vztahu dvou povrchů vyjadřuje šikmo odštípnuté čelo odkryté vrstvy. Při přejímce zkontrolovat krátkou stranu z výšky očí, volný průhled přes lávku a odlesk při otočení kamery po i proti slunci. Nové materiály, Rift a umístění opěr dosud čekají na ověření v nativním přehrávači.

### Meridian Basin — rozvázaný řez

Čtyři zavěšené výbrusy pocházejí z jediného průřezu. Souřadnice minerálních vrstev vycházejí ze vzorku před jeho rozdělením. Při pohybu desek tak lze sledovat stejnou žílu přes mezeru. Každá deska má odlomené rohy, tmavý kamenný bok a úzké zkosené rameno. V aréně se stejný leštěný kámen objevuje v osazených panelech krytů. Procedurální zdroj atlasu je v `tools/make-meridian-section.py`.

V první nativní zkoušce se pruhy obří planety překryly s žilami kamene. Ve druhé verzi patří severní horizont geodě, menší planeta leží na opačné straně a jádro výbrusu má krystalické plošky. Porovnej `output/fault-observatory/after/meridian-shelf.jpg` a `output/fault-observatory/final/meridian-shelf.jpg` ze stejné pozice. Obě jsou z nativního klienta s 12 účastníky včetně botů.

Přejímka v nativní macOS verzi při 1280 × 720 ověřila všech 135 aktivních kolizí Meridianu proti výchozímu exportu. Na přístupové kameře byl při 179 snímcích medián času snímku 14,91 ms před změnou a 15,58 ms po ní, p95 16,82 a 19,97 ms. Medián GPU byl 5,39 a 6,75 ms. Obě zkoušky běžely s 12 účastníky, pořadí načítání map a stav živého zápasu se lišily. Snímky i surová data jsou v `output/fault-observatory/repeat-before/` a `output/fault-observatory/final/`.

## Vizuální přestavba · vertikální řez Relay (listopad 2026)

**Kritika výchozího stavu.** Hra působila jako procedurálně složený prototyp z pěti hlavních důvodů:
1. **Hračkové proporce pilotů.** Kostra Kenney má nohy na 35 % výšky a hlavu na 27 %. Končetiny z Blenderu modelované pro stehno 0,35 m kostra mačkala na 0,24 m. Helma končila kolem 1,5 m, zatímco hitbox hlavy na serveru je mezi 1,53 a 1,78 m.
2. **Pět variant jedné pušky.** Všechny zbraně sdílely zadní blok s pažbou a lišily se jen předkem. Hmotu tvořily osmiúhelníkové kvádry.
3. **Plošný rastr spár.** Atlas plátů se 4 m periodou a černými spárami byl na každé stěně, podlaze i podhledu. Krabice tím čtou jako obkladačky a jejich krabicovost se ještě zdůraznila.
4. **Sytě modrý stín.** V Linear měl modrý ambient a odraz oblohy na podhledech hodnotu námořnické modři. Tvary se v něm ztrácely.
5. **Samolepková barevnost.** Frakční barva byla na každé končetině a botě. Hračkovitě působily i růžové koule Riftu a oranžové hlavně Scatteru.

**Hypotéza.** Kvalitu nejvíc ničí nesoulad měřítek a hodnot: lidské tělo v dětských proporcích, ruční zbraně z kvádrů a velké plochy rozdrobené jedním rastrem. Oprava proporcí a hierarchie hodnot dá víc než přidávání detailu.

**Co platí (ověřeno v `tools/unity/art-review.sh`, záběry v `output/visual-overhaul/`):**
- **Monolitní keramika.** Fasády mají periodu 8,8 m a spáru v tónu plátu (`SeamTone`, `SeamStrength` v `TRIAS/Architecture`). Paluba má desky 5,2 m s proměnnou hladkostí, takže odraz oblohy čte jako opotřebená glazura. Varianta r1 porazila původní rastr i variantu se samotnou desaturací.
- **Neutrální stín s teplým odrazem.** Ambient Relay je `#a2adb9 / #646c75 / #6f675d` a odrazová sonda má intenzitu 0,58. Podhledy čtou jako zastíněná keramika, ne jako modrá plocha. Snížení `reflectionIntensity` samo nic nezměnilo, protože rozhoduje intenzita sondy.
- **Zbraně z profilů.** `slab()` v `build-armory.py` vytahuje navrženou boční siluetu se zkosenými boky. Sdílený zadní blok je volitelný (`grip(stock=False)`).
  - Pulse je nízká klínová karabina se třemi keramickými „údery“ a svítícími mezerami.
  - Scatter je vysoký hranatý blok se štítem a hlavněmi nad sebou.
  - Lance má jehlové tělo a kostrovou pažbu.
  - Arc má rozeklaný keramický hřbet.
  - Rift má tělo roztržené na dvě poloviny s úlomky v trhlině a tmavé fasetované jádro.
- **Keramika na zbraních** má hodnotu `#cfc8ba` a hladkost 0,58. Původní `#e5e0d6` s hladkostí 0,80 se v první osobě přepalovala do bílé.
- **Proporce pilotů.** `PilotProportions` po animaci prodlužuje stehno a holeň (×1,41), paži (×1,30) a předloktí (×1,15) a zkracuje krk (×0,45). Výška se normalizuje podle ramen (81,8 % postavy), postava měří 1,76–1,84 m. Zbraň ve třetí osobě je ve výšce 1,36 m v měřítku 0,9. Hitbox se nemění.
- **Hierarchie barvy pilota.** Frakční barva zůstává na helmě, hrudi a ramenou, na končetinách se mění v grafit. Bašta má kopulovitou helmu s jedním hledím. Všechny obleky mají břišní segment.

**Zamítnuto.**
- Pulse s pružinovými prstenci na hlavni působil jako hračka.
- Štíhlý Pulse s dlouhou holou hlavní vypadal jako „puška na tyči“.
- Rastr 4 m s černými spárami.
- Samotné snížení sytosti ambientu bez teplého odrazu zespodu.
- Svítící koule jako jádro Riftu.
- Měřítko zbraně 0,62 ve třetí osobě, které vycházelo z malých pilotů.

**Druhá vlna (Relay, helmy, další arény).**
- **Paluby a lávky** nesou palubní desky jen na pochozí ploše (`_SideSeams`). Normála, okluze i spáry atlasu na bocích slábnou, takže hrana čte jako jeden litý blok.
- **Grafit architektury** má hodnotu `#353a40`, aby zastíněný parapet nečetl jako černá díra.
- **Bašta** má místo tří oranžových „očí“ jeden boční dálkoměr. **Přízrak** má zaoblenou čepici a nízký přijímač místo krabice s anténou.
- **Vanta a Meridian** dostaly stejný konstrukční jazyk jako Relay (`ArenaArchitecture`), ale v barvách svých dosavadních povrchů.
  - Zrnitou `refractory` texturu na keramice kitu ve Vantě (četla jako korek) nahradila matná žáruvzdorná keramika s usazenou špínou.
  - Podlahy a lité paluby tras jsou desky. Meridian má podlahu ztmavenou na `#6a5d56`, protože čistý tint bez textury byl příliš bledý.
  - Achátové výbrusy Meridianu, kámen a dřevo zůstávají beze změny.
- **Kordilera:** textura kamene je jemnější (×3,2) a má teplejší šedý tón. Pomohlo to jen málo.
- Sonda `TRIAS_ART_PICK=1` zapíše do `review.txt` objekt a materiál pod mřížkou 5×4 bodů. Rychle tak zjistíš, co v záběru opravit.

**Třetí vlna (vozidla, power-upy, Horda).** Kontrolní záběry: `relay-vehicles`, `relay-pickup-a/b` a `relay-horde`.
- **Vozidla** (`tools/blender/build-vehicles.py` → `Vehicles/Vehicles.fbx`, svazuje `TriasModels.Vehicle`):
  - Skimmer je nízký grafitový klín s keramickým zadním krytem, kokpitem a podstavcem věže ve fázové mezeře.
  - Bastion je šestikolový transportér s rozlámanými bočními pláty a keramickým čelním pancířem.
  - Týmová barva je jen jako pruh. Montážní bod zbraně se nemění.
  - Původní syté krabice na válcích zůstávají jako záloha.
- **Power-upy** (`build-powerups.py` → `PowerCells/PowerCells.fbx`):
  - Lékárnička je keramický kanystr se svítícím průzorem a křížem.
  - Štít tvoří tři zakřivené keramické lístky kolem šestibokého jádra.
  - Přebití je rozštípnutý krystal ve třech kovových sponách.
  - `Glow` dostává barvu podle katalogu.
- **Horda jsou porouchaní servisní roboti** (`build-horde.py` → `Horde/Horde.fbx`, `HordeBody.Sculpt`). Díly sedí na původních kloubech, takže chůze, nápřah i úder zůstávají.
  - Tělo tvoří grafitová kostra, popraskané keramické kryty, kovové písty a klepeta. Mají jedno živé a jedno mrtvé oko.
  - Crusher nese demoliční pancíř ramen se žhnoucími prasklinami, Spitter protržený kanystr s chladivem.
  - Jiskry jsou protáhlé záblesky v kloubech. Bliká deterministicky podle času a pořadí robota.
- **Zamítnuto:** organičtí „nakažení“ s anatomií z loftů. Světlá kůže splývala s keramikou pilotů a postavy působily jako dřevění manekýni (`output/visual-overhaul/z1`, `z2`). Zamítnutá je i původní plastelína z koulí.

**Zbývající slabiny.**
- Pozadí Relay a Vanty tvoří hranaté bloky (megastruktury, komíny).
- Zastíněná strana krytů v Meridianu je skoro černá.
- Kordilera potřebuje nové modely skal a terénu: fasetované skály s pruhy mechu, hladké kopce a krabicová bouda s nápisem.
- Plochá maska Přízraka je nejslabší hlavou mezi piloty.
- Sestava pilotů v náhledu nemá úchop zbraně (sonda nevolá `PlaceHands`).
- Herní texty mohou Hordu dál nazývat „nakažení“.
- Vozidla jsou tvarově jednodušší než zbraně. Kanystr lékárničky je z boku hladký.
- Nativní build, hra s boty a měření snímkové frekvence po přestavbě nebyly ověřeny.
