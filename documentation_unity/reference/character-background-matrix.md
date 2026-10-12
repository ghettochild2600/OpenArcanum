# Character Creation and Background Matrix

Source basis: retail `background.mes`, `backgroundlf.mes`, `gameback.mes`, `effect.mes`, prototype/item data, `arcanum-ce/src/game/background.c`, the official manual, and the Prima guide. Source order is numeric Background ID order.

Current-branch reconciliation: 2026-10-12 against `5926bb42ff0f1fe261ba8deb71716d149733c9d3`; the latest production change is `d0740332afeef46637d298063693371861f6cb07`.

## Eligibility rule

`arcanum-ce` walks numeric IDs forward or backward and accepts a record when its condition is empty/`ANY`, or its condition contains the character's exact three-letter race/sex identity. `NPC` records are excluded from custom player creation. Playable identities are:

`HUM HUF DWM ELM ELF HEM HEF GNM HAM HOM HOF HGM`

No female Dwarf, Gnome, Halfling, or Half-Ogre is offered by source creation even where a Background mask contains the corresponding code. Cycling wraps across the valid filtered list. Race or sex changes require rebuilding that list and removing the previous effect before applying the new selection.

OpenArcanum now follows the numeric enumeration/filter rule for all 79 records. Eligibility and visibility are complete and independent of effect support. The remaining systemic defect is effect execution: a visible, source-legal record with an unsupported token fails closed at final validation. `EFFECT UNSUPPORTED` below describes that separate runtime gap; it does not mean the record is hidden or incorrectly filtered. `EFFECT MISSING` means source behavior lives in time/environment/stat code and has no implementation.

## Creation invariants

- Initial attributes: 8 each; five unspent character points.
- Female modifier: ST -1, CN +1. Male modifier: none.
- Race modifiers and maxima are recorded in [OPENARCANUM_SOURCE_OF_TRUTH.md](OPENARCANUM_SOURCE_OF_TRUTH.md).
- Only one Background owner may contribute at a time. Cycling, race change, and sex change must remove the former effect before applying the next.
- Description/help follows the selected numeric record.
- Empty name remains invalid with `You must choose a name.`
- Portrait legality is race/sex-specific and independent from Background ordering.

## Portrait matrix

Retail `portrait/gameport.mes` defines the player portrait order below. NPC portrait entries 1035–1048 and 1050–1065 are not legal custom-player choices. OpenArcanum parses this source table, filters exact race/sex identity, and cycles ascending source ID.

| Identity | Legal portrait IDs / source tokens |
|---|---|
| Human Male | 1005 HUM1; 1018 HUM2; 1019 HUM3; 1072 HUM4 |
| Human Female | 1008 HUF1; 1025 HUF2; 1049 HUF3; 1075 HUF4 |
| Dwarf Male | 1001 DWM1; 1020 DWM2; 1028 DWM3; 1066 DWM4 |
| Elf Male | 1000 ELM1; 1015 ELM2; 1033 ELM3; 1073 ELM4 |
| Elf Female | 1006 ELF1; 1024 ELF2; 1034 ELF3; 1077 ELF4 |
| Half-Elf Male | 1010 HEM1; 1012 HEM2; 1032 HEM3; 1069 HEM4 |
| Half-Elf Female | 1011 HEF1; 1026 HEF2; 1029 HEF3; 1074 HEF4 |
| Gnome Male | 1002 GNM1; 1014 GNM2; 1022 GNM3; 1068 GNM4 |
| Halfling Male | 1003 HAM1; 1017 HAM2; 1023 HAM3; 1071 HAM4 |
| Half-Orc Male | 1004 HOM1; 1016 HOM2; 1030 HOM3; 1067 HOM4 |
| Half-Orc Female | 1007 HOF1; 1013 HOF2; 1031 HOF3; 1076 HOF4 |
| Half-Ogre Male | 1009 HGM1; 1021 HGM2; 1027 HGM3; 1070 HGM4 |

## Auto-Level Schemes and pregenerated characters

Retail `gamelevelname.mes`/`gamelevel.mes` contains source-ordered target lists. OpenArcanum loads and applies those rules after progression changes rather than inventing a build. Player-facing generic schemes are:

| IDs | Schemes |
|---|---|
| 0 | No Scheme |
| 50–55 | Necromancer; Gun Technologist; Air, Earth, Fire, and Water Elementalist |
| 56–60 | Guard; Shopkeeper; Priest; Druid; Summoner |
| 61–65 | Force Mage; Conveyance Mage; Temporal Thaumaturgist; Morph Mage; Gunfighter-Doctor |
| 66–69 | Warrior; Warrior Mage Good; Warrior Mage Evil; Pure Melee-Dodge |

IDs 70–75 are generic NPC schemes; IDs 76–87 are the twelve pregenerated-player schemes; later IDs are authored NPC/follower schemes. Raw rule order is authoritative because repeated targets describe a spending sequence, not a final unordered build. OpenArcanum implements every source target family, but its comma-segment parser requires exactly `token value`. CE tokenizes scheme 76's malformed `dx 20 pickpocket 20` segment as `dx 20` and consumes the rest; OpenArcanum rejects the four-token segment and stops the rule. That exact tokenization mismatch is the remaining confirmed auto-level defect.

| Source index | Pregenerated character | Identity | Portrait | Inventory source | Background / scheme |
|---:|---|---|---:|---:|---|
| 1 | Merwin Tumblebrook | Halfling Male | 1017 | 108 | 39 / 76 |
| 2 | Godfrey Castleburger | Gnome Male | 1002 | 24 | 40 / 77 |
| 3 | Horace McGinley | Half-Elf Male | 1032 | 20 | 41 / 78 |
| 4 | Muck Evil Eye | Half-Ogre Male | 1027 | 25 | 42 / 79 |
| 5 | D'ren L'elor | Elf Male | 1033 | 20 | 43 / 80 |
| 6 | Solomon Doone | Human Male | 1005 | 22 | 44 / 81 |
| 7 | Merik Luggerton | Half-Orc Male | 1030 | 20 | 45 / 82 |
| 8 | Lugard Bloodstone | Dwarf Male | 1020 | 24 | 46 / 83 |
| 9 | Victoria Warrington | Human Female | 1025 | 23 | 47 / 84 |
| 10 | Bel'an T'Seraa | Elf Female | 1024 | 22 | 48 / 85 |
| 11 | Clarisse Vorak | Half-Orc Female | 1031 | 26 | 49 / 86 |
| 12 | Ke'ree Melange | Half-Elf Female | 1029 | 26 | 50 / 87 |

Custom characters proceed to the source Human Shopkeeper shopping map with Background starting gold/items plus default clothing. Purchases transfer real objects and money; Continue equips source-compatible best items and enters the start map. Pregenerated characters bypass this shop because their authored inventory source already supplies possessions. This bounded creation/shop route is implemented and physically validated; general in-game merchant presentation remains a separate audit boundary.

## Complete source Background catalog

Eligibility `Any` means every playable identity. Effect strings are normalized for readability but retain the source operation. Gold is source starting money. Items are source prototype IDs.

| ID | Name | Eligibility | Source effect | Gold / items | OpenArcanum |
|---:|---|---|---|---|---|
| 0 | No significant background | Any | none | 400 | `COMPLETE` |
| 1 | Raised by Snake Handlers | Any | poison resistance +20; BE -1 | 400 | `COMPLETE` |
| 2 | Apprenticed to a Blacksmith | Any | ST +1; Repair +2; DX -2 | 400 | `COMPLETE` |
| 3 | Raised by Elves | HUF HUM | Repair, Firearms, Pick Locks, Disarm Traps -1; magic points +1 | 400; 8157 | `COMPLETE` |
| 4 | Raised by Monks | Any | PE +1 | 100 | `COMPLETE` |
| 5 | Child of a Hero | Any | negative reaction adjustment ×2 | 400; 6042 | `EFFECT UNSUPPORTED`: reaction multiplier |
| 6 | Inheritance | Any | WP -1 | 800 | `COMPLETE` |
| 7 | Foppish Elf | ELF ELM | DX -1; Persuasion +2 | 400 | `COMPLETE` |
| 8 | Clanless Dwarf | DWF DWM | Repair, Firearms, Pick Locks, Disarm Traps -2 | 400; 6076 | `COMPLETE` for playable DWM |
| 9 | Disenfranchised Gnome | GNF GNM | Haggle -2; ST +1 | 400 | `COMPLETE` for playable GNM |
| 10 | Halfling Orphan | HAF HAM | Pick Pocket +2; IN -1 | 400 | `COMPLETE` for playable HAM |
| 11 | Special Person | Any | IN -2; positive reaction adjustment ×2 | 400 | `EFFECT UNSUPPORTED`: reaction multiplier |
| 12 | Professional Knife Tosser | Any | PE -1; Throwing +3 | 400 | `COMPLETE` |
| 13 | Apprenticed to a Shopkeeper | Any | Haggle +3; DX -1 | 400 | `COMPLETE` |
| 14 | Sent to Charm School | HUF GNF | BE +1; CH +2; IN -1; ST -2 | 400 | `COMPLETE` for playable HUF |
| 15 | Born Under a Sign | Any | critical hit chance -10; critical hit effect +10; critical failure chance -10; critical failure effect +10 | 400 | `EFFECT UNSUPPORTED`: critical modifiers |
| 16 | Extreme Personality | Any | negative reaction -30; positive reaction +30 | 400 | `EFFECT UNSUPPORTED`: reaction modifiers |
| 17 | Factory Escapee | HOM HOF | ST +1 | 50 | `COMPLETE` |
| 18 | Freed Bodyguard | HGM HGF | IN -1 | 500 | `COMPLETE` for playable HGM |
| 19 | Bookworm | HUF HUM DWF DWM ELF ELM HEF HEM GNF GNM HAF HAM | IN +1; PE -1 | 400 | `COMPLETE` |
| 20 | Bully | same as 19 | IN -1; ST +1 | 400 | `COMPLETE` |
| 21 | Wild Half-Ogre | HGM HGF | WP +1; CH -1 | 400 | `COMPLETE` for playable HGM |
| 22 | Rare Half-Ogre Birth | HGM HGF | IN +2; ST -1; CN -1 | 400 | `COMPLETE` for playable HGM |
| 23 | Suppressed Orcish Looks | HOM HOF | BE +2; CH -2 | 400 | `COMPLETE` |
| 24 | Elven Blood | HUF HUM GNF GNM HAF HAM HGM HGF | Repair, Firearms, Pick Locks, Disarm Traps -1; magic points +1 | 400 | `COMPLETE` |
| 25 | Hyperactive | Any | Speed +1; CH -1 | 400 | `EFFECT UNSUPPORTED`: Speed modifier |
| 26 | Army Training | all players except Half-Ogre | ST +1; Bow +1; Melee +1; DX -1 | 400 | `COMPLETE` |
| 27 | Tough Hide | HOM HOF HGM HGF | damage resistance +10; BE -1 | 400 | `COMPLETE` |
| 28 | Sheltered Childhood | HUF HUM HEF HEM HOF HOM GNF GNM HAF HAM ELF ELM | ST -6; IN +2; WP +2; PE +1 | 400 | `COMPLETE` |
| 29 | Troll Offspring | Any | BE -4; CH -4; ST +2; CN +2; DX +1 | 400 | `SOURCE AMBIGUITY`: missing separator in retail effect text |
| 30 | Ran Away with the Circus | Any | ST +6; IN -2; WP -3; PE -1 | 400 | `COMPLETE` |
| 31 | Raised in the Pits | Any | IN -6; ST +2; DX +2; Melee, Dodge, Bow, Throwing +1 | 100; 6066 | `COMPLETE` |
| 32 | Feral Child | HUF HUM HEF HEM HOF HOM GNF GNM HGF HGM | IN -6; Persuasion -1; Haggle -1; DX +3; Prowling +1; Pick Pocket +1 | 200 | `COMPLETE` |
| 33 | Sickly | Any | IN +6; ST -2; CN -4; DX -1 | 400 | `COMPLETE` |
| 34 | Beat with an Ugly Stick | Any | BE -6; ST +2; DX +2; Melee, Dodge, Bow, Throwing +1 | 400 | `COMPLETE` |
| 35 | Lady's Man | all playable male identities | BE +6; CN -2; ST -2; DX -2 | 400 | `COMPLETE` |
| 36 | Débutante | all female identity codes | BE +3; CH +3; ST -1; DX -1; combat skills -2 | 400 | `COMPLETE` for playable females |
| 37 | Raised by Orcs | HUF HUM HOF HOM GNF GNM HAF HAM HGF HGM | CH -6; ST +2; PE +2; combat skills +1 | 400 | `COMPLETE` |
| 38 | Charlatan's Protégé | HUF HUM DWF DWM ELF ELM HEF HEM GNF GNM HAF HAM | CH +6; ST -2; CN -2; combat skills -1 | 400 | `COMPLETE` |
| 39 | Merwin Tumblebrook | NPC | pre-generated prototype | 400; 15178 | excluded from custom creation |
| 40 | Godfrey Castleburger | NPC | pre-generated prototype | 400 | excluded from custom creation |
| 41 | Horace McGinley | NPC | pre-generated prototype | 400 | excluded from custom creation |
| 42 | Muck Evil Eye | NPC | pre-generated prototype | 400 | excluded from custom creation |
| 43 | D'ren Le'lor | NPC | pre-generated prototype | 400 | excluded from custom creation |
| 44 | Solomon Doone | NPC | pre-generated prototype | 400 | excluded from custom creation |
| 45 | Merik Luggerton | NPC | pre-generated prototype | 400 | excluded from custom creation |
| 46 | Lugard Bloodstone | NPC | pre-generated prototype | 400 | excluded from custom creation |
| 47 | Victoria Warrington | NPC | pre-generated prototype | 400 | excluded from custom creation |
| 48 | Bel'an T'Seraa | NPC | pre-generated prototype | 400 | excluded from custom creation |
| 49 | Clarisse Vorak | NPC | pre-generated prototype | 400 | excluded from custom creation |
| 50 | Ke'ree Melange | NPC | pre-generated prototype | 400 | excluded from custom creation |
| 51 | Educator | Any | maximum followers -1 | 400 | `EFFECT UNSUPPORTED`: follower-cap modifier |
| 52 | Super Model | all female identity codes | BE +6; ST -2; IN -4 | 400 | `COMPLETE` for playable females |
| 53 | Escaped Lunatic | HUM | damage/fire/electrical/poison resistance +25; reaction -25 | 0; 8117 | `EFFECT UNSUPPORTED`: reaction modifier |
| 54 | Escaped Lunatic | HUF | same as 53 | 0; 8142 | `EFFECT UNSUPPORTED`: reaction modifier |
| 55 | Idiot Savant | HUF HUM HEF HEM HOF HOM HGF HGM | IN +6; ST -1; CN -1; DX -2; WP -2; Gambling +3 | 0 | `COMPLETE` |
| 56 | Tomboy | all female identity codes | ST +1; CN -1 | 400 | `COMPLETE` for playable females |
| 57 | Barbarian | HUF HUM HEF HEM HOF HOM | ST +2; CN +1; IN -1; CH -2; Melee +1; Haggle -2 | 100; 8096 | `COMPLETE` |
| 58 | Barbarian | DWF DWM GNF GNM HAF HAM | same as 57 | 100; 8104 | `COMPLETE` for playable male identities |
| 59 | Nietzsche Poster Child | Any | experience gain +10%; critical failure chance +20 | 400 | `EFFECT UNSUPPORTED`: experience/critical modifiers |
| 60 | Dark Elf Follower | ELF ELM | WP +2; CH -2 | 400 | `COMPLETE` |
| 61 | Sold Your Soul | Any | magic points +4; reaction -20; alignment -200; maximum alignment -200 | 400 | `EFFECT UNSUPPORTED`: reaction/alignment modifiers |
| 62 | Day Mage | all players | time-of-day magic aptitude rule | 400 | `EFFECT MISSING` |
| 63 | Night Mage | all players | time-of-day magic aptitude rule | 400 | `EFFECT MISSING` |
| 64 | Sky Mage | all players | environment magic aptitude rule | 400 | `EFFECT MISSING` |
| 65 | Nature Mage | all players | environment magic aptitude rule | 400 | `EFFECT MISSING` |
| 66 | Agoraphobic | HUF HUM DWF DWM GNF GNM HAF HAM | exterior/interior rule | 400 | `EFFECT MISSING` |
| 67 | Hydrophobic | HUF HUM DWF DWM GNF GNM HAF HAM HOF HOM | Persuasion +2 plus water/rain behavior | 400 | `PARTIAL`: skill only |
| 68 | Afraid of the Dark | HUF HUM HAF HAM | PE +2 plus darkness behavior | 400 | `PARTIAL`: attribute only |
| 69 | Magick Allergy | HUF HUM DWF DWM GNF GNM HAF HAM HOF HOM HGF HGM | technology points +2 | 400 | `COMPLETE` |
| 70 | Arsonist | HUF HUM GNF GNM HAF HAM HOF HOM | Explosives expertise +20; ST -1; CN -1 | 400 | `EFFECT UNSUPPORTED`: expertise modifier |
| 71 | Bandit | HUF HUM HOF HOM HEF HEM | Firearms +2; CH -1 | 0; 6083, five × 7039 | `COMPLETE` |
| 72 | Technophobia | HUF HUM HEF HEM HAF HAM HOF HOM | ST +1; CN +1; source technology restriction | 400 | `PARTIAL`: restriction not certified |
| 73 | Only Child | Any | WP +6; CH -4; Haggle -2; Persuasion -2 | 400 | `COMPLETE` |
| 74 | Frankenstein Monster | HUM HOM | ST +4; CN +4; DX -6; electrical resistance +30; poison +20; fire -20 | 0 | `COMPLETE` |
| 75 | Bride of Frankenstein | HUF HOF | BE +4; CN +4; DX -4; electrical resistance +20; poison +10; fire -10 | 0 | `COMPLETE` |
| 76 | Mad Doctor | Any | IN +2; PE +2; electrical/poison resistance +20; Heal +4; BE -1; CH -1; DX -2; CN -3 | 400 | `COMPLETE` |
| 77 | Miracle Operation | Any | IN +3; CH +3; PE +5; ST -3; DX -3; CN -5 | 400 | `COMPLETE` |
| 78 | Dark Sight | Any | darkness/vision rule | 400 | `EFFECT MISSING` |

## Representative ordered lists

These lists follow numeric source order after filtering; they are useful regression anchors, not a replacement for deriving eligibility from data.

- Human Male begins: No significant background → Raised by Snake Handlers → Apprenticed to a Blacksmith → Raised by Elves → Raised by Monks → Child of a Hero → Inheritance → Special Person → Professional Knife Tosser → Apprenticed to a Shopkeeper.
- Human Female additionally includes Sent to Charm School at its numeric position and excludes male-only Lady's Man while including Débutante, Super Model, Tomboy, and Bride of Frankenstein where legal.
- Elf combinations include Foppish Elf and Dark Elf Follower, exclude Human-only Raised by Elves, and otherwise retain every `Any`/matching source record in numeric order.
- Dwarf Male includes Clanless Dwarf and Dwarf-qualified broad masks; it excludes Army Training only when the exact source mask does so (Half-Ogre is the excluded playable race there).
- Half-Ogre Male includes Freed Bodyguard, Wild Half-Ogre, Rare Half-Ogre Birth, Tough Hide, and the broad HGM masks, but does not receive records whose condition omits HGM.

## Required regression properties

1. The expected list is derived from numeric retail records plus exact identity-mask matching.
2. Forward and backward cycling wrap within the filtered list.
3. Switching race or sex rebuilds eligibility and preserves only a still-legal selection.
4. Applying the new record removes all former Background-owned contributions first.
5. Unsupported effects never silently create a partially effective character.
6. Starting gold/items use the selected record and do not accumulate while cycling.
7. NPC records 39–50 never enter custom character cycling.
8. Time/environment Backgrounds are not considered complete because their generic effect text is empty.
