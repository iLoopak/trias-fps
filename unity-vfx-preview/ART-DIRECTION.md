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

**Čtvrtá vlna (zvuk a další kola zdokonalení).** Rendery jsou v `output/visual-refine/`, spektrogramy v `output/audio-revamp/spectra/`.
- **Zvuk.** `tools/audio/sfx-manifest.json` popisuje 71 zvuků:
  - 40 zvuků zbraní: výstřel, alternativní výstřel, otevření, vložení, zavření, nasazení, prázdná komora a zásah.
  - Pohyb, zásahy, události fáze, Horda jako roboti a UI.
  - Čtyři smyčky ambience aren.

  `tools/audio/generate-sfx.mjs` je generuje přes ElevenLabs Sound Effects. Klíč čte jen z proměnné `ELEVENLABS_API_KEY`. Krátké zvuky ořízne na začátku (výstřel zazní v prvním snímku), přidá dozvuk a normalizuje je na −1 dBFS. Smyčky aren normalizuje na −24 LUFS a nastaví na streamování.

  `RelayEffects.Sfx` srovná každý klip na RMS procedurálního klipu, který nahrazuje. Původní mix tak zůstává a procedurální zvuk slouží jako záloha. `Trias.Preview.AudioReview` ověří import a načtení všech klipů.

  Zvuky byly zkontrolovány technicky a podle spektrogramů: Pulse má krátký chirp, Scatter subbas, Lance harmonický hvizd, Arc široké praskání a Rift zvlněný tónový oblouk. **Poslechem je nikdo neschválil.**
- **Skály Kordilery.**
  - `weather()` v `trias_environment_forms.py` přidává deterministické fraktální zvětrání. LOD0 má jednu subdivizi navíc a sítě se po posunu rozdělí na trojúhelníky.
  - Materiály se řídí geologií místo řad polygonů: mech na plochách obrácených nahoru, tmavý kámen na převisech a vlhké patě a jedna rezavá vrstva.
  - Rozpočet skal je 2600 trojúhelníků, skutečnost 412–692.
- **Meridian.** Výplň stínu ×1,28 a odrazy oblohy 1,6 nastavené absolutně podle arény, takže se nesčítají. Kryty s achátem ve stínu teď čtou jako břidlicová konstrukce.
- **Zamítnuto:**
  - Výškový gradient trávy na Kordileře: v šedi nepřinesl změnu.
  - Globální zvýšení kontrastu a snížení ambientu ve Vantě a Meridianu (`l1`, `l2`): stíny zčernaly a achát zmizel.
  - Násobení `reflectionIntensity`: hodnota by se při opakovaném načítání arén sčítala.

**Pátá vlna (pozadí, útulna, Přízrak, podhledy).** Záběry jsou v `output/beyond/`: výchozí stav `a-baseline`, finální `final`, srovnání `final/before-after.png` a `final/grayscale.png`.
- **Megastruktury pozadí** (`tools/blender/build-megastructures.py`, FBX `TRIAS/Megastructures`) nahrazují kvádry, které z dálky prozrazovaly prototyp. Nemají kolizi. `TriasWorld.Megastructure()` je usadí s návětrnou stranou na lokální −z a jednotným měřítkem. Když asset chybí, zůstanou původní bloky.
  - **HabitatPier (Relay):** sloup se třemi keramickými plášti posunutými mimo osu a oddělenými fázovou spárou. Nese vodorovný ložný trám, nad ním je mřížový stožár s majákem.
  - **KilnStack a KilnHall (Vanta):** osmiboké komíny s obručemi, žebříkovou klecí, stoupačkou a rozšířenou korunou, k tomu hala s pilovou střechou, jejíž svislá zasklení žhnou. Nový záběr `vanta-works` míří na ně.
  - Zamítnuto: první verze pilíře s oblými plášti a hřibovou hlavou četla jako raketa. Pilová střecha z šikmých desek nad kvádry četla jako solární panely, správně je to hranol s jednou svislou a jednou šikmou plochou.
- **Horní útulna (Kordilera):** kolize se nemění. Na kamenné podezdívce s krycí vrstvou leží srubové trámy různé hloubky a každý pátý je tmavší, vyměněný kus. Rohové sloupky jsou tesané, střecha má drnový povrch a vyčnívající konce krokví. Cedule „HORNÍ ÚTULNA“ s diakritikou visí na tesané desce místo velkého plovoucího nápisu. Chakra Petch má Í i Ú.
- **Přízrak:** `tools/blender/explore-wraith.py` vyrenderoval tři skutečně odlišné hlavy, rozdělenou masku, čočkovou optiku a kápi. Vyhrála kápě s fasetovanou keramickou čepelí (kýlem) a jednou svislou optikou mimo střed. Rozdělená maska četla jako otevřená kniha, čočky zanikly ve velikosti hlavy. Lemovaný okraj kápě se zamítl, četl jako béžové prkno.
- **Podhledy Relay:** zvýšené desky šachty (`service`, `gallery`) bez podlahy do 2,4 m pod sebou dostaly mělká příčná žebra s roztečí 2,4 m, okrajové nosníky a zapuštěná světla (`DressRelaySoffit`). Nic nevisí víc než 0,18 m pod kolizí. Strop z dolní paluby tak už není prázdná deska.
- **Měření** (`TRIAS_ART_PERF=60 tools/unity/art-review.sh …`) uvádí medián a p95 CPU času `Camera.Render` včetně kompozice visoru v editoru, počet draw callů, SetPass a trojúhelníků. Neměří GPU čas, ani snímkový čas přehrávače.
  - Relay: +0,08 až +0,16 ms (≈3 %), +20 až 23 draw callů a +5 tis. trojúhelníků.
  - Vanta: +0,04 ms a +12 draw callů. Kordilera: +0,05 ms a +8 draw callů.
  - Hodnoty p95 jsou v editoru šumivé (až 7,8 ms), mediány jsou stabilní.
- `TRIAS_ART_PICK=2` přijme i velké vzdálené stavby a k objektu vypíše jeho hranice. Pomohl odhalit, že strop nad dolní palubou je deska šachty, ne kryt.

**Šestá vlna (kryty sektorů, skály, události, vozidla).** Záběry jsou v `output/beyond/` (`i-*` terasy, `j-plinth-compare.png`, `k-rock`, `l-fx*` události, `m-veh` vozidla).
- **Kryty teras ve Vantě a Meridianu** (`terrace`, bez profilu) dostaly z kitu tepelný a tlakový kontejner místo holých kvádrů. Na obou dlouhých stranách si nechávají sektorový pruh barvy týmu.
  - Zamítnuto: `MachineExtraction` vypadal o metr nižší než kolize 1,56 m, takže by šlo o klamavý kryt.
- **Středový podstavec Meridianu** (`relay` box 5,8 × 5,8 × 3,6 m) je `MachineDistribution` s achátovými panely.
  - Zamítnuto: otevřený rám `MachineStabilizer` (mezerami prosvítá neviditelná kolize) a `MachineProcessor`, který četl jako krabice s víkem.
- **Skály Kordilery:** materiál se volí podle normál vyhlazených přes sousední plochy. Mech tvoří souvislé polštáře na římsách, žádné kosočtverce.
- **Události** se teď dají revidovat. `ArtReview` vytvoří herní `RelayEffects` přes `BuildTrias.CreateEffects` a záběry `fx-*` spustí událost a vyfotí ji po zadaném čase.
  - **Likvidace** je fázový lom: tři vrstvy odtržených keramických plátů v tónu frakce se rozepnou a rozjedou vertikálně (horní stoupají, spodní sedá) a na podlaze zůstane otisk. Dřív to byl záblesk a pár střepů, které zmizely za 0,4 s.
  - **Prasknutí Echa** jsou dvě skořepiny odtlačené do stran místo hvězdice částic. Prstence mají volitelný drift s doběhem.
  - Oživení (sbíhající se pláty) a úskok (glyf frakce) zůstaly beze změny.
- **Vozidla:**
  - **Bastion:** střecha ze tří keramických plátů se spárou, velitelský poklop, zadní úložný box, dvě prutové antény, kouřové granátomety, světla a tažná oka.
  - **Skimmer:** šikmé ocasní ploutve, boční sání a světla.
- **Měření proti stavu PR #7** (`TRIAS_ART_PERF=60`, CPU `Camera.Render` v editoru):
  - Terasa ve Vantě: 2,99 → 3,11 ms, 607 → 644 draw callů.
  - Střed Meridianu: 2,53 → 2,63 ms, 648 → 712 draw callů.
  - Vozidla: 3,60 → 3,63 ms, +34 tis. trojúhelníků.
  - Kordilera: beze změny.

**Sedmá vlna (pohyb zbraně z první osoby).** Časové řady jsou v `output/beyond/o-motion/` (`equip-sheet.png`, `land-sheet.png` a CSV křivky).
- `WeaponHandling.Temperament` dává každé zbrani vlastní povahu pohybu, takže pět nástrojů už nesdílí jeden profil.
  - Zvednutí po výměně trvá 0,18–0,26 s. Delší být nesmí: server povolí výstřel 0,24 s po výměně a zbraň nesmí střílet, když se ještě zvedá. Váhu proto nese dojezd v klidu.
  - Dojezd (největší výchylka náklonu): Scatter 1,9° (těžké kývnutí), Arc 1,5° (rychlé nervózní chvění), Pulse 1,0°, Rift 0,8° (pomalé fázové usazení), Lance 0,45° (skoro klidná).
  - Kývání při rozhlížení: Rift a Scatter se zpožďují, Arc reaguje okamžitě, Lance se kývá nejméně.
- Při doskoku zbraň klesne až za tělem (pružina 0,055 s) a ústí se skloní. Váhu doskoku určuje zbraň: Scatter ×1,35, Lance a Arc ×0,9.
- Úchop při chůzi opisuje osmičku: do strany s poloviční frekvencí kroku, svisle s frekvencí kroku. Dřív se pohyboval po diagonále. Kamera se nemění.
- `WeaponHandling.Grip` skládá pózu úchopu pro hru i pro sondu stejným kódem. Záběry `motion-*` simulují časovou řadu výměny a doskoku na 240 Hz a vyrenderují zvolené okamžiky.
- Neověřeno: pocit v ruce při hře. Výchylky jsou záměrně malé, aby nerušily míření, a před vydáním je musí posoudit člověk ve Feel Lab.

**Osmá vlna (odpalovací rampa, límce pilotů, web).** Záběry jsou v `output/beyond/` (`p-pad*`, `q-pick`, `r-site*`, `site-final`).
- **Odpalovací rampa** (`tools/blender/build-launchpad.py`, FBX `TRIAS/LaunchPad`) nahrazuje tmavý kotouč se dvěma kroužky. Má grafitový podstavec, šest keramických segmentů límce se zlatými spárami, zapuštěnou studnu, radiální urychlovací cívky a zlaté jádro. Průměr 1,85 m zůstal, výška je pod 8 cm, aby nečetla jako schod. Vnější zlatý kroužek zůstává jako signál na dálku. Rampa je jen vizuál, spouštění řídí server.
- **Límec pilotů:** portréty pro web ukázaly, že helmy všech pilotů sedí na dlouhém tenkém krku. Polstrovaný límec s keramickou deskou, vyšší vzadu (`build-pilots.py`, `build-vektor.py`), je teď spojuje s rameny.
- **Lékárnička:** kříž je na všech čtyřech stranách a přibyla svislá upínací žebra, takže čte i z boku.
- **Texty Hordy:** kill feed dřív ukazoval surové klíče `claw`, `spore` a `rubble`. Teď zobrazí „Úder robota“, „Výstřik chladiva“ a „Troska“ (`TriasClient.CauseLabel`).
- **Web:** galerie arén má nové záběry z enginu bez HUD v rozlišení 1280×720 s popiskem „In-engine capture“. Portréty pilotů (`pilots.webp`) jsou rendery skutečných modelů s jejich zbraněmi. Původ obrázků popisuje `website/DESIGN.md`.
  - Záběr Meridianu míří k prstencové planetě (na západ, ~32° nad obzorem), protože ta je hlavním poznávacím znakem arény.

**Devátá vlna (cíle a terén Kordilery).** Záběry jsou v `output/beyond/` (`s-obj*` a `t-*`, k tomu srovnání v odstínech šedi).
- **Bod nadvlády** byl 30 cm vysoký terminál s kostkou, který u schodů zanikal. Teď nese štíhlý reléový stožár (`tools/blender/build-objectives.py`, FBX `TRIAS/Objectives`) s grafitovým stěžněm, třemi keramickými žebry a kolébkou. Fázová čočka v barvě vlastníka se vznáší ve 2,35 m a je vidět přes arénu. Stožár je mnohem užší než pilot, takže nepůsobí jako kryt. Původní nízký terminál zůstává jako záloha.
- **Terén Kordilery:** svahy obrácené k nízkému slunci jsou sušší a slámové, závětrné svěží a tmavší. Velké nepravidelné skvrny z nízkofrekvenčního vzorku přidávají krajině proměnlivost a narušují pravidelné sinusové louky.
  - První síla změny (zesvětlení 0,5) byla vidět jen v barvě. Přijatá síla (0,72 se ztmavením závětří na 0,74) mění i hodnotu, takže se tvary kopců oddělí i v šedi. Dřívější výškový gradient trávy se zamítl, protože v šedi nic neměnil.
- **Vlajka CTF** je čitelná a zůstala beze změny.

**Desátá vlna (prapor CTF a uzemnění).** Záběry jsou v `output/beyond/` (`u-flag*`, `v-*`).
- **Prapor CTF** (`FactionStandard` v `build-objectives.py`) nahrazuje kvádry. Grafitová žerď má úchyt, hlavici a příčník. Keramický prapor tvoří tři desky oddělené fázovou spárou: hlavička se světelnou linkou, pole frakce s erbem a vlaštovčí ocas.
  - Desky jsou 6–7 cm silné. Při 2,8 cm prapor při pohledu z boku zmizel, což u cíle CTF nejde.
- **Export Blender → Unity zrcadlí lokální osu x.** U souměrných modelů to nevadí. Prapor proto má v Unity otočení o 180° kolem svislé osy. Platí to i pro asymetrické prvky dřívějších modelů (např. optika Přízraka sedí na jeho levé straně).
- **Kontaktní stíny** (`BuildGroundContacts`) teď měkce uzemňují i kryty stojící na podlaze ve Vantě a Meridianu, v tónu stínu každé arény.
- **Zamítnuto:** prachové koleje těžebních vozidel z hald rudy ke stroji v Meridianu. V rozumné síle na světlé podlaze nebyly vidět, v trojnásobné působily jako škrábance.

**Jedenáctá vlna (nativní playtest s boty).** Výstupy jsou v `output/playtest*/` (logy snímků, snímky obrazovky s HUD).
- `tools/playtest/run.sh` spustí skutečný zápas s boty v nativním macOS přehrávači a měří ho. Hráč stojí bez vstupu, boti bojují kolem. Postup popisuje skill playtest.
- **Bez vsync, Apple M2, 1600×900, každá aréna samostatně a po vychladnutí:**
  - Vanta: medián 5,7–6,9 ms, p99 9–10 ms.
  - Relay: medián 3–8 ms, p99 ≤ 13 ms.
  - Meridian: medián ≈ 13 ms, p99 ≈ 20 ms.
  - Kordilera: medián ≈ 12 ms, p99 17–24 ms.
  - Všude je s rezervou nad 60 snímků/s. S vsync drží Relay stabilních 16,67 ms.
- Počet rendererů (≈ 1000–1400) ani paměť GC (5–7 MB) během zápasu nerostou.
- **Neprokázáno:** druhý běh za sebou na M2 bez ventilátoru zpomalil až 2× (Vanta v sérii z 6 na 15 ms, Relay z 8 na 17 ms). Samostatný běh Vanty po vychladnutí byl stabilní. Nejpravděpodobněji jde o tepelné omezení, macOS ale varování nezaznamenal.
- Nativní snímky z boje odpovídají revizním záběrům. Sonda tedy ukazuje to, co hra.

**Dvanáctá vlna (Kordilera, paluba Meridianu, HUD, úchop pilotů).** Výstupy jsou v `output/beyond/` (`w-*`, `x-*`, `y-hands`) a `output/playtest5/`.
- **Kopce Kordilery – zamítnuto:**
  - Nízké skalní desky na svazích (vyčnívaly nejvýš 0,25 m, aby netvořily klamavý kryt) z herní vzdálenosti zanikly.
  - Shaderové erozní rýhy se neprojevily ani natvrdo zapnuté na celé strmé ploše. Kopce jsou na to příliš mírné.
  - Skalní žebra na útesech pod okrajem ostrova nejsou ze hry vidět.
  - Hladkost tedy plyne přímo z tvaru autoritativní výškové mřížky. Skutečné zdrsnění by změnilo pohyb, výhledy i navigaci botů, a to je herní rozhodnutí, ne vizuální.
- **Paluba Meridianu:** na volné podlaze vystupuje jedna achátová žíla. Jsou to čočkovité broušené výbrusy podél společné zlomové osy, s matným materiálem (lesk 0,18) a tmavým grafitovým lemem.
  - Leštěný achát pod nízkým úhlem zrcadlil oblohu a splýval s podlahou.
  - Úseky po souvislé trase zakrývaly kryty a byly příliš úzké.
  - Pozor: pás vrcholů po směru cesty musí jít při pohledu shora po směru hodinových ručiček, jinak je Unity ořízne jako odvrácený.
- **HUD v nativní hře:** `-trias-autopilot` nechá pilota při playtestu běhat, kličkovat, skákat a uskakovat normální vstupní cestou.
  - Panel řetězce hybnosti (Surge ×2, Kinetic ×1,5, Overdrive ×2,5 s popisem akce) i ztráta zdraví jsou čitelné a nepřekrývají se s municí.
  - Prolomení štítu se nezachytilo, protože pilot nesebral štít.
  - Výkon s autopilotem v Relay: se vsync 16,67 ms, bez vsync medián 2–3 ms a p99 10–13 ms.
- **Revizní piloti** jsou teď herní `Avatar` sestavený z řádku hráče jako v hangáru. Drží svou zbraň přes stejné IK (`PlaceHands`) jako v zápase, sestava i portréty. Portréty na webu jsou obnovené.

**Zbývající slabiny.**
- Přebíjení a výstřel mají společné časování. Jejich pohyb ze sedmé vlny posoudil jen člověk ve Feel Lab, ne sonda.
- Prolomení štítu ve visoru zatím žádný záběr nezachytil (při playtestu chybí sebrání štítu).
- Kopce Kordilery mají hladký tvar autoritativní výškové mřížky; změna je herní rozhodnutí. 
- Windows build se nespouštěl. Měření snímků proběhlo jen na Apple M2. Hráč při playtestu nehrál, takže pohyb a střelba z první osoby nebyly v nativním měření zatížené.

**Třináctá vlna (řemeslo prostředí, blikání, plynulost).** Záběry jsou v `output/craft/` (`a-audit` výchozí stav, `b-floor*`, `c-satin`, `d-mineral*`, `e-colliders*`, `f-lava*`, `g-*` blikání) a měření v `output/pt-*`.
- **Podlahy jako konstrukce:** `TRIAS/Architecture` má na vodorovných plochách mřížku polí (`_BayPeriod`). Pole mají spáry s obrobeným lemem, mírně odlišný tón a lesk a místy servisní poklop 1,3 m se čtyřmi šrouby (`_HatchRate`).
  - Arény mají pole 12,8 m, paluba Relay 10,4 m. Vanta má hrubší rozptyl tónů a méně poklopů.
  - První verze s hustší mřížkou rozdrobila plochu na dlaždice. Platí pravidlo monolitních polí (≥ 7 m).
- **Grafitový satén – zamítnuto:** změna lesku grafitových dílů nebyla z herní kamery vidět.
- **Minerál jako hmota:** nový `TRIAS/Mineral Solid` počítá pruhy achátu ze světové polohy, od rozházených jader deformovaných šumem. Hrany, plochy i sousední kusy proto ukazují jeden souvislý řez kamenem, ne obrázek na panelu. Barvy čte po poloměru z obrázku `meridian-agate`.
  - Meridian má ve slotu Secondary tento minerál.
  - Ceramic na Meridianu je prašná těžební keramika (`#9b8576`, refractory).
  - První pokus ji zesvětlil do slonovinové barvy jako Relay, a mapa ztratila identitu (Meridian těží, Vanta zpracovává, Relay stabilizuje).
- **Blikání textur (hlavně Relay):** příčinou byla keramická vložka madla zábradlí, která ležela v jedné rovině s nosníkem. Doklad je `g-zfight` (rozdíl dvou snímků posunutých o 7 mm) a `g-zfix`. Vložka je teď o 12 mm vystouplá.
  - Plátové textury architektury mají trilineární filtrování.
  - Kontaktní a palubní obtisky (`RelayContact`, `DeckStencil`) mají depth offset.
- **Neviditelná plocha nad lávou ve Vantě:** šlo o kontaktní lemy z PR #12, které přesahovaly okraj kanálu a visely nad lávou. `AddContactSkirt` teď paprskem ověří podlahu pod každým vnějším rohem; bez podlahy roh stáhne dovnitř.
  - Rekvizity s colliderem nad viditelným tvarem řeší čtrnáctá vlna.
- **Plynulost (nativní playtest s autopilotem, 60 Hz vsync):**
  - Parser paketů (`WireValue`) sdílí krátké řetězce, malá celá čísla a `true` a pole staví v přesné velikosti. Alokace parseru klesly z ≈ 1300 na 70–120 KB/s a GC běhů je ≈ 20–24 místo 40–46 za 10 s.
  - Texty HUD se skládají jen při změně hodnoty (`TextMemo`).
  - **Cukání ostatních pilotů:** interpolace začínala v okamžiku, kdy dorazil paket. Při snapshotech 30 Hz a 60 fps tak pilot každý druhý snímek stál a pak poskočil (`remote_judder` ≈ 190 %).
    - Úsek teď začíná časem předchozího snímku, takže `remote_judder` je 3–15 % (nejhorší běh 24 %).
    - Hermitova interpolace s rychlostí ze serveru vyšla hůř (8–29 %) a je zamítnutá.
  - **Kamera vlastního pilota:** sledovala predikci krokovanou po 30 Hz a v sousedních snímcích se posouvala o 0,66 a 0,34 kroku (`local_judder` 60–72 %).
    - Teď jde po přímce mezi posledními dvěma kroky, časované podle plánu kroků. `local_judder` je 6–20 %; běh s mnoha propady snímků měl 43 %.
    - Cenou je zpoždění pohybu (ne míření) zhruba o půl kroku.
  - Simulace, síťový protokol ani pravidla se nezměnila.

**Zbývající slabiny.**
- Občasné snímky přes 25 ms (0–5 za 10 s, v jednom běhu 24) většinou nesouvisejí s GC a měřené části klienta je nevysvětlují. Zbývá je profilovat (vykreslování, fyzika, kompozitor).
- `remote_judder` ještě závisí na fázi příchodu paketů vůči snímkům.

**Čtrnáctá vlna (neviditelné plochy, zbraně a vedlejší rekvizity zblízka).** Záběry jsou v `output/craft/` (`h-colliders*` sonda, `i-review*` rekvizity a zbraně, `i-review3/scatter-compare.png`).
- **Sonda colliderů lhala:** přeskakovala meshe bez Read/Write (MapProps), takže hlásila celý kvádr každé rekvizity z MapProps jako neviditelný a skutečné mezery skryla.
  - V editoru teď používá i tyto meshe.
  - Hlásí i `gap_m`, tedy jak hluboko pod herní plochou leží ta viditelná: centimetry znamenají nepřesné usazení, metry chybějící plochu.
- **Pravidlo:** `Fit` napasuje bounding box modelu na herní kvádr. Model proto musí kvádr vyplnit, hlavně horní plochu a rohy. Kulatý nebo zúžený tvar nechá okraje kvádru neviditelné.
  - **Pec a struska (Vanta):** komora a chladicí lože stojí v nosném rámu se sloupky v rozích, příčkami a plošinou přes celý půdorys. Sonda: z 70 a 33 zásahů na 0.
  - **Ruda (Meridian):** místo zaobleného balvanu je to řezaný těžební blok. Achátová čepice a žíla jsou z `TRIAS/Mineral Solid` (nový slot `Mineral` v MapProps), na stěnách jsou vrtné stopy a zvedací pásy. Z 63–77 na 3–5 zásahů po hranách.
  - **Středový podstavec Meridianu:** distribuční stroj má servisní plošinu přes celý půdorys na čtyřech sloupcích. Z 32 na 0.
- **Skály Kordilery – zamítnuto:** skály z `Nature.fbx` mají ukloněné vrstvy a vrchol ustupující na stranu. Horní plochu kvádru tak pokrývají asi z třetiny (21–76 zásahů, mezera 1–2 m).
  - Krycí deska přes celý půdorys snížila zásahy na polovinu, ale ze skal udělala hřiby na tenkých nohách a z blízké skály obří zelenou plochu.
  - **Řešení v pravidlech:** skály mají stupňovité collidery.
    - Sonda `TRIAS_ART_ROCKS=1` naměřila u čtyř tvarů z kitu výškovou mapu viditelného vrchu (16 × 16) a `ROCK_TOPS` v `public/rules.js` ji nese.
    - Každá skála má v `cover` tři kvádry `collider: true`: základ asi 0,5 m nad terénem, střed a hřeben. Každý je největší obdélník, nad kterým kámen sahá do jeho výšky (tolerance 5 % výšky skály).
    - Celá skála zůstává v novém seznamu `scenery`, ze kterého ji kreslí Unity, web i taktická mapa. Vzhled se nezměnil.
    - Zbraně týmů leží na nejvyšším volném stupni, ne nad středem skály. U chaty Horní útulna je to nižší stupeň u svahu: zbraň Arc dřív visela 0,69 m nad kamenem.
    - Sonda: z 21–76 zásahů na skálu na 1–2 na hranách.
- **Zbraně zblízka:** pět zbraní ve stejné klidové póze v chladném světle Relay a teplém světle Vanty (`weapon-*`).
  - Lance, Arc a Rift jsou jednoznačné. Pulse a Scatter měly z pohledu hráče stejnou světlou keramickou střechu s jedním černým podélným prvkem.
  - Scatter má teď čtyři příčná chladicí žebra a jeho jediná oranžová fázová mezera vede přes celou šířku střechy. Dřív byla zapuštěná a z oka neviditelná.
- **Vedlejší rekvizity:** zdravotní kanystr, odpalovací rampa, vozidla, roboti Hordy i pilot jsou čitelní. Nejslabší je power-up štítu, plochá šedá deska s jednou tenkou azurovou linkou.

**Zbývající slabiny.**
- Kmeny stromů v Kordileře mají kvádr 4,9 m, který končí pod korunou nebo vedle ní (5–9 zásahů na strom). Zábradlí mostu u x = 16,3 hlásí 9 zásahů a pod ním nic viditelného není.
- Občasné snímky přes 25 ms zatím nikdo neprofiloval (vykreslování, fyzika, kompozitor).
- Power-up štítu je ze všech sběratelných předmětů nejméně výrazný.

**Patnáctá vlna (stromy, zábradlí, štít, propady snímků).** Sonda je v `output/craft/k-trees*` a `m-verify`, měření v `output/ws-*`.
- **Kmeny stromů:** sonda `TRIAS_ART_TREES=1` naměřila průřez kmene po 0,25 m. Kmeny jsou zahnuté a posunuté od středu a do 1,75 m se rozvětvují do koruny.
  - Celý strom je teď ve `scenery`. V `cover` zůstal jen kvádr jádra kmene (`TREE_TRUNKS` v `public/rules.js`) od země do 1,75 m, tedy největší obdélník, který kmen vyplňuje ve všech výškách.
  - Koruna už není neviditelný sloup 0,64 × 0,59 × 3,6 m.
- **Zábradlí lávek:** kolize je jeden plný panel, ale vizuál tvořily jen sloupky a dvě madla, takže těsně nad mostovkou stála neviditelná stěna. Přibyla okopová lišta.
  - Sonda na Kordileře: stromy z 5–9 zásahů a zábradlí z 9 na 0. Zbývá nejvýš 2 zásahy na hranách skal.
- **Power-up štítu:** každý plátek má zářící šestiúhelník (znak štítu, obdoba kříže na lékárničce) a širší světelný šev. Jádro je větší a plátky se nahoře rozevírají.
- **Propady snímků:** log je přiřazuje přes `FrameTimingManager` (řádek `spike_causes`; v Player Settings je zapnuté Frame Timing Stats).
  - Bez vsync (bez dalšího zatížení stroje) je medián snímku 2,5–3,7 ms na Relay a ~4,6 ms na Meridianu. Meridian má p95 11–14 ms a GPU p95 8–9 ms, takže práce hry má při 60 Hz velkou rezervu.
  - Při vsync je propadů 0–7 za 10 s (do 1 % snímků), vždy jeden snímek 25–34 ms, a CPU ani GPU v nich budget nepřekračují. Hlavní vlákno při vsync čeká na prezentaci renderovacího vlákna, takže jeho čas vychází ~17 ms a dřív se mylně hlásil jako `main`; teď se takový snímek hlásí jako `present`.
  - Nejpravděpodobnější příčina je časování prezentace okna (kompozitor macOS v okenním režimu). Neověřeno ve fullscreenu.
  - Zkrácení stínů na 90 m se 2 kaskádami ani vypnutí AO počet propadů měřitelně nezměnilo (rozptyl mezi stejnými běhy je větší než rozdíl). MSAA 0 vyšlo lépe, ale za cenu hran, proto zůstává.
  - GC: texty HUDu (alternativní akce, panel hybnosti) se skládají jen při změně. Alokace HUDu klesly zhruba na polovinu a propady souběžné s GC na 0–1 za 10 s. Zbytek alokací je uvnitř Unity GUI (`GUI.matrix`, `DrawTexture`) a parseru paketů (~120 KB/s).

**Zbývající slabiny.**
- Občasné propady o jeden snímek při vsync (0–7 za 10 s) pocházejí z prezentace, ne z práce hry. Ověřit je fullscreen (exkluzivní nebo bez rámečku) a případně Metal display link.
- Parser paketů stále alokuje ~120 KB/s, protože každý snapshot staví nové `WireValue`. Recyklace by vyžadovala vlastnictví snapshotů (předchozí stav drží reference).
- Sonda hlásí okraje galerijních desek na Relay (4–16 zásahů přesně na hraně collideru, x = ±20,5). Vizuální hrana je o centimetry zúžená; herně nepodstatné, ale ve výpisu sondy to ruší.
