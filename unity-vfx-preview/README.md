# TRIAS v Unity

Otevři tuto složku v **Unity 6.3 LTS (6000.3.29f1)**, vyber **TRIAS → Open full game** a spusť **Play**.

Pro online zápas spusť z kořene repozitáře `npm ci` a `npm start`. V Unity nastav adresu `ws://127.0.0.1:3000` pro lokální server. Na dalších strojích v síti použij jeho LAN adresu. Veřejný server za TLS přijímá adresu ve tvaru `wss://…`. Klienti ve stejné místnosti používají stejný kód. Nastavení arény a režimu určí klient, který místnost založí.

Online klient používá autoritativní server pro pět soutěžních režimů, čtyři arény a kooperativní Obranu jádra. Pěší pohyb se predikuje lokálně, vozidla přebírají polohu ze serveru. Tlačítko **Trénink offline · Relay Station** spouští samostatný místní TDM na Relay Station.

- **WASD** a myš ovládají pohyb a míření. **Shift** sprintuje, **C** krčí, **mezerník** skáče a **Q** provádí úskok.
- Levé tlačítko střílí, **R** přebíjí, **1–5** volí zbraň, **F** použije alternativní střelbu Scatteru a **G** hodí granát.
- **E** obsluhuje vozidla a věže. Při čekání na respawn přepíná spoluhráče. **Tab** zobrazí skóre a **Esc** uvolní kurzor.

Pro společný vývoj verzuj `Assets` včetně `.meta`, `Packages` a `ProjectSettings`. Cache Unity a sestavení v `output/` ignoruje Git.

## Modely a animace

- Pět modelů zbraní včetně Riftu pochází z [Kenney Blaster Kit](https://kenney.nl/assets/blaster-kit). Sdílejí atlas s doplněnými svítícími prvky pro výbavu TRIAS.
- Čtyři třídy pilotů vycházejí z rigovaného kyborga a animačních klipů [Kenney Animated Characters Protagonists](https://kenney.nl/assets/animated-characters-protagonists). Třídy rozlišují siluety, výzbroj a úpravy obleku. Tým určují barevné prvky brnění.
- Mechový ostrov používá stromy, skály, keře a květiny z [Kenney Nature Kit](https://kenney.nl/assets/nature-kit). Terén a poloha překážek vycházejí z herní mapy.

Všechny tři balíčky mají licenci CC0. Původní licenční soubory jsou u vybraných modelů v `Assets/Resources/TRIAS/Kit`.

## Obrana jádra

V lobby vyber **HORDA**. Až čtyři piloti spolu brání jádro na Relay Station během osmi vln. Lovci sprintují do boje zblízka, střelci kryjí nástup, drtiči ohrožují jádro silnými údery a strážci míří z dálky. Mezi vlnami má posádka čas přeskupit se. V průběhu zápasu přibývá munice, zbraně, granáty, štíty a Overcharge. Výhra vyžaduje ubránit jádro do konce osmé vlny.

Během přípravy stiskni **B**. Klávesou **1** vybereš barikádu za 30 šrotu a **2** automatickou věž za 65. Namiř na volné stavební místo a klikni levým tlačítkem. Posádka začíná se 40 kusy šrotu a další získává za vyřazené nepřátele. Postavená obrana přetrvává mezi vlnami a nepřátelé ji mohou zničit. Modely obrany využívají bedny a blaster z Kenney Blaster Kit.

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

Samostatná srovnávací scéna pro Unity 6.3 LTS. Obsahuje výstřel Pulse, nárazové jiskry, explozi Riftu a návrat pilota. Účelem je porovnat vizuální a zvukovou odezvu s webovou verzí TRIAS.

Otevři tuto složku v Unity 6.3 LTS. V nabídce vyber **TRIAS → Open VFX Lab**. Při prvním otevření se vytvoří scéna v `Assets/Scenes/Trias VFX Lab.unity`. V režimu Play spustíš klávesou **1** Pulse, **2** Rift, **3** návrat a **mezerníkem** výstřel s následným návratem.

Ve scéně jsou pojmenované emitery s nastavením částic v Inspectoru. Na objektu **Effects controls** jsou barvy a časování výšlehu a návratu. Projekt používá procedurální geometrii, vestavěný renderovací pipeline a zvuk vytvářený za běhu.
