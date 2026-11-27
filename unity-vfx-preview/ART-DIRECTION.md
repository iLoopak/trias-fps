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
