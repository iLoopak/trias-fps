# TRIAS W8 — Pozvánky, návrat a výcvik

## Pozvi spoluhráče

1. Spusť aktuální server W8 a v klientu nastav jeho adresu. Lokálně stačí `npm start` v kořeni projektu a adresa `ws://127.0.0.1:3000`. Pro internet použij veřejné `wss://`; na Renderu musí být nasazený server W8.
2. Založ zápas a v menu **Esc → Kopírovat odkaz** zkopíruj pozvánku. Pošli ji kamarádovi. Webová stránka zobrazí kód místnosti a tlačítko **Otevřít a připojit se**. Podržením **Shift** při kopírování získáš přímý odkaz `trias://` pro aplikace, které ho umějí otevřít kliknutím.
3. Na Macu otevře odkaz aplikaci TRIAS W8. Na Windows jednou zvol v lobby **Nastavení → Povolit klikací pozvánky**. Hra zaregistruje schéma jen pro přihlášeného uživatele. Při přesunu rozbalené hry registraci zopakuj. Pokud asociace nefunguje nebo už máš ve Windows otevřenou hru, vlož zkopírovaný odkaz v lobby přes **Připojit se → Z pozvánky**.

Pozvánka obsahuje jen adresu a kód. Ve stanovené místnosti může hostitel dál používat i sdílení samotného kódu. V už rozehraném zápase na Macu čeká příchozí pozvánka na potvrzení přes **Esc**. [Registrace URL v Unity pro macOS](https://docs.unity3d.com/6000.3/Documentation/Manual/deep-linking-macos.html)

## Návrat po výpadku

Klient automaticky obnoví spojení a server drží místo **55 sekund** od odpojení. Vracíš se do stejného zápasu pod stejným pilotem. Výbava, stav a skóre se řídí tím, co se během výpadku stalo. Pilot při ztrátě spojení odloží vlajku, opustí vozidlo nebo věž a zůstává zasažitelný. Uzel během výpadku neobsazuje. Restart serveru ukončí rozběhnuté místnosti. Návrat je určený pro obnovení spojení ve stejné spuštěné instanci hry.

## Výcvik pilota

V lobby zvol **Výcvik pilota**. Klient založí soukromou střelnici na zvoleném serveru W8. Postup hlídá stejná simulace jako v zápase. Úskok přes **Q** vytvoří ozvěnu, přes **X** z ní vystřelíš. Potom na střelnici vyřadíš tři terče a klávesou **6** aktivuješ radarový průzkum. **Enter** po dokončení vrací do lobby. Samostatný **Trénink offline** zůstává dostupný bez serveru.

## Horda a Meridian Basin

- **Rada obrany** probíhá po 2., 4. a 6. vlně. Každý hráč má jeden hlas. Hlasuje klávesou **V**, která přepíná mezi volbami, a hlas může změnit do výjezdu další vlny. První volba přidá 90 HP jádru a opraví stávající stavby až o 35 HP. Druhá dá aktivním obráncům štít a granát. Třetí přidá 45 šrotu do společné zásoby. Při remíze má po druhé vlně přednost oprava, po čtvrté zásoby a po šesté šrot. Když nikdo nehlasuje, tým dostane běžné odměny za vlnu.
- **Krytí na Meridianu** se počítá, když obsazené spojenecké vozidlo vidí na uzel z prostoru mimo kruh obsazování do 13 m. Pěchotě přidá 18 % základní rychlosti obsazování. Vozidlo samo uzel nezabere. Protihráč může poslat pěchotu do kruhu, vozidlo odříznout překážkou nebo ho zničit. Na HUD sleduj popisky **DOBÝT**, **ODPOJENO**, **SPOR** a **KRYTÍ**.

## Odezva a obtížnost

Výchozí obtížnost serverových botů je **Recruit**. Pro návrat k předchozí výchozí obtížnosti použij `TRIAS_BOT_DIFFICULTY=regular`. Klient při krátkém zpoždění použije nejnovější doručený stav a zpracuje mezilehlé události, například potvrzené zásahy a vyřazení.

## První spuštění na Macu

Lokální testovací ZIP má podpis ad hoc. Pokud ZIP pochází z důvěryhodného zdroje a chceš jej vyzkoušet, po prvním pokusu o spuštění najdeš jednorázovou volbu **Přesto otevřít** v **Nastavení systému → Soukromí a zabezpečení**. [Postup Applu](https://support.apple.com/en-us/102445)
