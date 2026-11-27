# TRIAS v Unity

Otevři tuto složku v **Unity 6.3 LTS (6000.3.29f1)**, vyber **TRIAS → Open full game** a spusť **Play**.

Pro online zápas spusť z kořene repozitáře `npm ci` a `npm start`. V Unity nastav adresu `ws://127.0.0.1:3000` pro lokální server. Na dalších strojích v síti použij jeho LAN adresu. Veřejný server za TLS přijímá adresu ve tvaru `wss://…`. Klienti ve stejné místnosti používají stejný kód. Nastavení arény a režimu určí klient, který místnost založí.

Postup pro server přes internet je v [Multiplayer přes internet](INTERNET.md).

Online klient používá autoritativní server pro pět soutěžních režimů, čtyři arény a kooperativní Obranu jádra. Pěší pohyb se predikuje lokálně, vozidla přebírají polohu ze serveru. Tlačítko **Trénink offline** spouští samostatný místní TDM na Relay Station. Začneš se zbraní vybraného pilota a můžeš rovnou vyzkoušet všech pět zbraní a jejich sekundární střelbu.

- **WASD** a myš ovládají pohyb a míření. **Shift** sprintuje, **C** krčí, **mezerník** skáče a **Q** provádí úskok.
- Levé tlačítko střílí, **R** přebíjí, **1–5** volí zbraň, **pravé tlačítko** nebo **F** spustí sekundární střelbu a **G** hodí granát.
- **E** obsluhuje vozidla a věže. Při čekání na respawn přepíná spoluhráče. **Tab** zobrazí skóre a **Esc** uvolní kurzor.

Mířidlo mění tvar podle zbraně, zobrazuje postup přebití a odlišuje běžný zásah, zásah do štítu a vyřazení. Červený oblouk ukazuje směr příchozího zásahu. Radar se otáčí spolu s pohledem a značky ukazují spoluhráče a cíle režimu.

Sluchátka pomáhají určit směr kroků a výstřelů. Kryt je tlumí. Každá aréna má vlastní zvuk prostředí. V Hordě hlásí samostatné signály nástup a odražení vlny.

V nabídce **Esc** můžeš upravit zorné pole, citlivost myši, pohyb kamery, celkovou hlasitost, hlasitost prostředí a intenzitu světelných efektů. Nastavení se ukládá na daném počítači. Zpětný ráz se liší podle zbraně. Tvrdý dopad doprovází tlumený zvuk magnetických bot.

Na aktuálním serveru reagují pěchotní zbraně okamžitě lokálním výšlehem, zvukem a pohybem zbraně. Server potvrzuje munici, trajektorii, zásah a poškození. **Pulse** a **Arc** střílí držením tlačítka, **Scatter**, **Lance** a **Rift** po jednotlivých stiscích. Sekundární střelba se spouští jednotlivými stisky. HUD u zbraně ukazuje její název a spotřebu munice.

| Zbraň | Sekundární střelba | Spotřeba |
| --- | --- | ---: |
| Pulse | Trojimpulz, tři paprsky s malým rozptylem | 3 |
| Scatter | Slug, soustředěný projektil na střední vzdálenost | 2 |
| Lance | Průstřel, zeslabený druhý zásah za prvním nechráněným pilotem | 2 |
| Arc | Obloukový vějíř, sedm paprsků do šířky | 8 |
| Rift | Těžká koule, pomalejší projektil s větším výbuchem | 2 |

Bašta, Přízrak a Poštolka startují s plným zásobníkem, jedním přebitím a rezervním Pulsem. Vektor má Pulse s plným zásobníkem a neomezenou rezervou. Další zbraně a munice jsou na stanovištích v aréně. Unity klient s rozšířenými mapami vyžaduje současný server se stejnou geometrií.

Přebití animuje obě ruce a mechanismus zbraně. Pulse mění zásobník, Scatter otevírá komoru, Lance zatahuje závěr, Arc vysune článek a Rift odklopí uzávěr energetického jádra. Křivky a kontaktní body lze ladit v `WeaponHandling.cs`. Pohyb kostí je vizuální a nezasahuje do serverových hitboxů ani délky přebití.

Úspěšný **Q** úskok za sebou na 2,5 s nechá ozvěnu. **X** vystřelí aktuální pěchotní zbraní z její pozice, **Shift+X** použije soustředěný Scatter. Platí běžná munice, kadence a kolize. Protivník může ozvěnu zničit. **Momentum Chain** boduje platné pohybové akce a potvrzené zásahy a vyřazení. Má vlastní výsledky v souhrnu kola a jeho intenzitu najdeš v nastavení. Klávesu ozvěny lze změnit na objektu **TRIAS client**. Serverové hodnoty a tabulka akcí jsou v `signature.js`. Volby `TRIAS_COMBAT_ECHO=0` a `TRIAS_MOMENTUM=0` vypínají systémy nezávisle.

Světelné efekty ovládají přízemní mlhu, bloom a paprsky rozptýleného světla. Využívají hloubku obrazu a mají vlastní barvy a intenzitu pro každou arénu. Vantu rámují tavicí komora a vysoké výduchy, Meridian Basin pohyblivé těžební stroje a Kordilera má chladný opar v nížinách.

Relay Station má pás galaxie, Vanta Foundry zatmění s oranžovou korónou a Meridian Basin planetu s prstenci. Textura `Assets/Resources/TRIAS/relay-galaxy.jpg` vznikla v ImageGen podle obrazové reference a zadání vytvořit široký pás hvězd s kobaltovým a fialovým prachem, jemným teplým jádrem a tmavými okraji pro přechod do oblohy.

Za **3, 5, 7 a 9 osobních vyřazení v jednom životě** se postupně odemykají odměny. **6** zobrazí na 8 sekund soupeře na radaru. **7** spustí polní modul pro okolní spoluhráče. Vektor léčí, Bašta přidává štít, Přízrak doplňuje munici a Poštolka přidává granát a menší štít. **8** označí zem pro orbitální úder s dvousekundovým varováním. **9** přidá štít a na 12 sekund zvýší poškození. Vyřazení ruší nevyužité odměny.

Rozhraní používá [Chakra Petch](https://github.com/google/fonts/tree/main/ofl/chakrapetch) a [Barlow Semi Condensed](https://github.com/google/fonts/tree/main/ofl/barlowsemicondensed) pod licencí SIL Open Font License. Licenční soubory jsou v `Assets/Resources/TRIAS/Fonts`.

Obraz lobby `Assets/Resources/TRIAS/relay-eclipse.jpg` vznikl vestavěným nástrojem ImageGen. Zadání:

> Use case: stylized-concept. Asset type: wide 16:9 native Unity game lobby background plate for TRIAS, a fictional arena shooter. Render a severe orbital relay station in dark ink-blue space; three colossal offset antenna fins and a single slim amber eclipse cut an unmistakable silhouette on the far right. Warm amber navigation pinpricks, subtle blue and muted teal metal, off-black navy negative space on the left and lower two-thirds where a live settings menu will overlay. Camera is outside the station, slightly above the armored structure, with layered scale and a thin horizon arc only in the upper right. Look like an optical-instrument photograph printed on matte technical stock: precise crisp silhouette, quiet fine film grain, believable surfaces, strongly restrained color, huge quiet darkness, no generic glowing panel patterns. Keep hot highlights away from the central controls area. Full-bleed art only with no characters, titles, letters, labels, HUD, text or watermarks.

Pro společný vývoj verzuj `Assets` včetně `.meta`, `Packages` a `ProjectSettings`. Cache Unity a sestavení v `output/` ignoruje Git.

[Vizuální směr W3 a původ assetů](ART-DIRECTION.md) popisuje nové modely, frakční značení a výtvarné odlišení arén.


**Orbitální lobby.** Rychlá hra používá společný kód pro zvolenou arénu a režim na nastaveném serveru. **Vytvořit zápas** rezervuje samostatný kód a **Připojit se** vyžaduje kód existující místnosti. Adresu vlastního serveru a přezdívku najdeš v **Nastavení → Pokročilé připojení**. Pro striktní vytvoření a připojení používej s klientem W4 stejnou verzi serveru.

Pilotem otočíš tažením v hangáru. Změna náhledového znaku ukazuje výtvarnou podobu frakce. Tým do zápasu přiděluje server. Mapové karty používají snímky přímo z herních arén. **Tab**, **Shift+Tab** a **Enter** ovládají menu, šipky ladí vybraný posuvník. Posuvník **Pohyb kamery** omezuje pohyb kamery hangáru a při vypnutí **Světelných efektů** se zobrazí statické pozadí s portrétem. Zvuky rozhraní a atmosféra jsou syntetizované přímo ve hře.

Po skončení kola lze zůstat na automaticky spuštěné další kolo nebo se vrátit do lobby. Offline trénink zůstává dostupný z hlavní nabídky.

## Modely a animace

- Pět modelů zbraní včetně Riftu pochází z [Kenney Blaster Kit](https://kenney.nl/assets/blaster-kit). Sdílejí atlas s doplněnými svítícími prvky pro výbavu TRIAS.
- Čtyři třídy pilotů vycházejí z rigovaného kyborga a animačních klipů [Kenney Animated Characters Protagonists](https://kenney.nl/assets/animated-characters-protagonists). Třídy rozlišují siluety, výzbroj a úpravy obleku. Tým určují barevné prvky brnění.
- Kordilera používá stromy, skály, keře a květiny z [Kenney Nature Kit](https://kenney.nl/assets/nature-kit). Terén a poloha překážek vycházejí z herní mapy.
- Mateřská loď, doprovodné stíhačky a těžební čluny používají modely z [Kenney Space Kit](https://kenney.nl/assets/space-kit) s materiály a doplňky pro TRIAS. Ze stejné sady jsou servisní hangáry a sledovací anténa na Relay Station, chladicí moduly ve Vanta Foundry a nákladní monorail v Meridian Basin. Stavby stojí za hranou arén.

Všechny čtyři balíčky mají licenci CC0. Původní licenční soubory jsou u vybraných modelů v `Assets/Resources/TRIAS/Kit`.

## Obrana jádra

V lobby vyber **HORDA**. Až čtyři piloti spolu brání jádro na Relay Station během osmi vln. Nakažení se snaží dostat k jádru. Běžní nakažení a drtiči útočí zblízka holýma rukama. Od třetí vlny se objevuje plivač a od páté vrhač trosek. Každý útok má čitelný nápřah. Úderu lze uhnout a plivanec nebo trosku zastaví kryt. Mezi vlnami má posádka čas přeskupit se. V průběhu zápasu přibývá munice, zbraně, granáty, štíty a Overcharge. Výhra vyžaduje ubránit jádro do konce osmé vlny.

Během přípravy stiskni **B**. Klávesou **1** vybereš barikádu za 30 šrotu a **2** automatickou věž za 65. Namiř na volné stavební místo a klikni levým tlačítkem. Posádka začíná se 40 kusy šrotu a další získává za vyřazené nepřátele. Postavená obrana přetrvává mezi vlnami a nepřátelé ji mohou zničit. Modely obrany využívají bedny a blaster z Kenney Blaster Kit.

Nastavení dynamických hlídek, živé arény, rytmu Hordy a zkoušky týmového rádia najdeš v [návodu k W7](W7-DIRECTOR.md).

## Desktopové sestavení

Skript `Trias.Preview.BuildTrias` nabízí metody `BuildMac`, `BuildWindows` a `BuildLinux` pro dávkový režim Unity. Vybranou metodu předej jako `-executeMethod` a cestu k výstupu jako proměnnou `TRIAS_NATIVE_BUILD`. Windows a Linux potřebují příslušný modul Build Support v Unity Hubu. macOS build používá univerzální binárku pro Apple Silicon a Intel. Steam je cílová distribuční platforma.

## Hratelný prototyp Relay Station

Otevři projekt v Unity 6.3 LTS a vyber **TRIAS → Open Relay Station**. Při prvním spuštění nabídka vytvoří scénu v `Assets/Scenes/Relay Station.unity`. V režimu **Play** klikni do herního okna nebo stiskni **Enter**.

Prototyp běží lokálně jako Team Deathmatch proti jedenácti botům rozděleným do tří týmů. Kolo končí na 35 vyřazeních nebo po 6 minutách. Trasy, kryty, lávky, skokové podložky a výchozí pozice vycházejí z dat webové Relay Station.

- **WASD** a myš ovládají pohyb a míření. **Shift** sprintuje, **mezerník** skáče a opakovaný stisk ve vzduchu udělá dvojskok.
- Levé tlačítko střílí a **R** přebíjí Pulse. **1** vybaví Pulse a **2** vybaví Rift. Pro snadné porovnání efektů má hráč po každém respawnu tři střely Riftu.
- **Esc** uvolní myš a pozastaví zápas. Po vyřazení kamera sleduje živého spoluhráče, pokud je k dispozici.

Výstřely, výbuch Riftu a respawn používají stejné typy částic a syntetizované zvuky jako srovnávací scéna níže. Emitery a světla lze vybrat přímo v hierarchii scény.

## Srovnávací scéna VFX Lab

Pro ladění plné hry spusť v kořenu repozitáře `npm run lab:feel` a v Unity vyber **TRIAS → Play Game Feel Lab**. Laboratoř používá vlastní server na lokální smyčce a stejnou střelbu i pohyb jako zápas. Čísly **1–5** vybereš zbraň. Každá má neomezenou rezervu a standardní kapacitu zásobníku. Na Relay jsou pevný, pohyblivý a štítový terč. Zásahy do podlahy a do krytů lze ověřit v okolí.

**F5** porovná lokální a serverovou odezvu výstřelu, **F6** přepne simulovaných 160 ms RTT a **F7** doplní terče a munici. **F8** přepne Combat Echo a **F9** Momentum Chain. Panel ukazuje dobu snímku, RTT, polohovou korekci a časy lokálního spuštění efektu a serverového potvrzení. Lokální čas měří práci v klientu po zpracování vstupu. V Inspectoru objektu **TRIAS client** lze za běhu ladit sílu a zotavení zpětného rázu, setrvačnost zbraně a amplitudu kroku. Nové zvuky i shader ozvěny jsou původní procedurální tvorba.

Nastavení systémových interakcí W6 a cvičení se sestřelováním granátů popisuje [návod k W6](W6-INTERACTIONS.md).

Samostatná srovnávací scéna pro Unity 6.3 LTS. Obsahuje výstřel Pulse, nárazové jiskry, explozi Riftu a návrat pilota. Účelem je porovnat vizuální a zvukovou odezvu s webovou verzí TRIAS.

Otevři tuto složku v Unity 6.3 LTS. V nabídce vyber **TRIAS → Open VFX Lab**. Při prvním otevření se vytvoří scéna v `Assets/Scenes/Trias VFX Lab.unity`. V režimu Play spustíš klávesou **1** Pulse, **2** Rift, **3** návrat a **mezerníkem** výstřel s následným návratem.

Ve scéně jsou pojmenované emitery s nastavením částic v Inspectoru. Na objektu **Effects controls** jsou barvy a časování výšlehu a návratu. Projekt používá procedurální geometrii, vestavěný renderovací pipeline a zvuk vytvářený za běhu.
