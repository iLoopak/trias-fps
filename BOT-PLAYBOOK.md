# TRIAS · Boti W5

| Osobnost | Co je vidět v zápase |
| --- | --- |
| Útočník | Zkracuje odstup, tlačí přes krátké úskoky a drží boj i při nízkém zdraví. |
| Lovec | Vyhledává osamělé soupeře, mění úhel a častěji využívá Combat Echo. |
| Strážce | Drží obsazený bod, kryje spoluhráče a dřív se stáhne do krytu. |
| Ostrostřelec | Udržuje odstup, mění pozici po střelbě a využívá výškové trasy. |
| Akrobat | Častěji skáče do boje, používá dvojskoky, úskoky a odrazové podložky. |
| Oportunista | Častěji vyhledává zásoby a použije dostupné vozidlo nebo kulometné hnízdo. |

Osobnost vychází ze slotu a drobné odchylky ze jména místnosti a identity bota. Vydrží po respawnu. Obtížnost mění reakci, stabilitu míření, frekvenci rozhodování a ochotu použít složitější pohyb. Zdraví, poškození, munice i kolize počítají stejná pravidla jako u člověka. Při ztrátě výhledu si bot krátce pamatuje poslední pozorovanou polohu a směr. Slyšený zvuk dává přibližný bod k prohledání. Před výstřelem se znovu ověřuje viditelnost. Ozvěna střílí přes stávající serverová pravidla.

## Nastavení serveru

- TRIAS_BOT_DIFFICULTY přijímá hodnoty recruit, regular, veteran, elite a mixed. Výchozí obtížnost je regular.
- TRIAS_HORDE_BOTS=1 zapíná automatické doplnění obránců v Hordě. W5 klient nabízí také volbu **Boti brání jádro** při zakládání. Člověk při vstupu převezme volný slot bota. Stavby platí a umisťují hráči.
- TRIAS_BOT_DEBUG=1 zapisuje do stderr v rozestupu čtyř sekund na bota jeho úmysl, pozorovaný cíl, paměť, směrování a rozhodnutí o ozvěně. Diagnostika je výchozím nastavením vypnutá.
- Příkaz **node tools/bot-scrimmage.mjs** simuluje zápasy na všech mapách a ve všech režimech. **TRIAS_SCRIMMAGE_SECONDS** nastavuje horní hranici herního času. Výstup obsahuje ukončení zápasu, rozdělení vyřazení, pohyb, ozvěny, průběh cílů, výskyt uváznutí a průměrný čas serverového ticku.

## Týmové rádio

Šest původních českých replik má ženskou i mužskou verzi. Vznikly v ElevenLabs s modelem Flash v2.5 a dvěma standardními hlasy. Každý bot používá konzistentní hlas podle své varianty pilota. Ve hře se přehrávají importované klipy z adresáře Radio. Server posílá kód hlášky, volací znak a variantu hlasu živým spoluhráčům s podporou rádia. FFA rádio nepoužívá. Jeden tým má mezi hláškami odstup alespoň 8,5 sekundy, jeden bot alespoň 19 sekund. Posuvník **Týmové rádio** řídí hlasitost a titulky zůstávají dostupné i při ztlumení.
