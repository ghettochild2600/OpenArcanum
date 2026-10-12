# Technology and Schematic Matrix

Source basis: official manual technology appendix, Prima schematic tables pp. 54–62, retail schematic/MES/prototype data, `arcanum-ce` technology code, and current OpenArcanum catalog/crafting tests.

## Discipline rules

Source order is Herbology, Chemistry, Electrical, Explosives, Gun Smithy, Mechanical, Smithy, Therapeutics. Each discipline progresses Layman → Novice → Assistant → Associate → Technician → Engineer → Professor → Doctorate. The seven learned degrees require INT/level `5/10, 8/20, 11/35, 13/50, 15/65, 17/80, 19/100`. Degrees are sequential, cost one character point, and add one technology point. Effective degree falls when Intelligence no longer meets its threshold; learned ownership remains.

Built-in source schematic ID is `1990 + 200 × discipline index + 10 × degree`. Generic ingredient consumption/output creation exists in OpenArcanum. Product behavior is separate and is mostly incomplete.

## Learned schematic catalog

| Discipline | Degree / source ID | Schematic | Ingredient A | Ingredient B | OpenArcanum boundary |
|---|---|---|---|---|---|
| Herbology | Novice / 2000 | Healing Salve | Ginka Root | Kadura Stem | craftable; usable-salve prototype identity is ambiguous |
| Herbology | Assistant / 2010 | Fatigue Restorer | Coca Leaves | Tobacco Leaves | craftable; product-specific effect not certified |
| Herbology | Associate / 2020 | Poison Cure | Venom | Kadura Stem | craftable; poison runtime incomplete |
| Herbology | Technician / 2030 | Fatigue Limiter | Fatigue Restorer | Witchbane | craftable; product-specific effect not certified |
| Herbology | Engineer / 2040 | Accelerate Healing | Healing Salve | Coca Leaf | craftable; timed effect incomplete |
| Herbology | Professor / 2050 | Wonder Drug | Accelerate Healing | Poppy Flowers | craftable; product-specific effect not certified |
| Herbology | Doctorate / 2060 | Cure All | Wonder Drug | Big Chief Snake Oil | craftable; condition-cure coverage incomplete |
| Chemistry | Novice / 2200 | Strong Poison | Monroe Cleaner | Varham Aqua Vitae | craftable; poison application/runtime incomplete |
| Chemistry | Assistant / 2210 | Charges | Electrolyte Solution | Metal Plates | craftable; charge/ammunition integration not certified |
| Chemistry | Associate / 2220 | Animal Scent | Bromide | Morning Star Perfume | craftable; creature-reaction effect missing |
| Chemistry | Technician / 2230 | Corrosive Acid | Pete Carbolic Acid | Sulphur Pills | craftable; thrown/armor effect missing |
| Chemistry | Engineer / 2240 | Hallucinite | Mushrooms | Varham Aqua Vitae | craftable; mental condition missing |
| Chemistry | Professor / 2250 | Paralyzer | Potassium Chloride | Strong Poison | craftable; paralysis condition missing |
| Chemistry | Doctorate / 2260 | Anaesthesizer | Hallucinite | Bromide | craftable; condition effect missing |
| Electrical | Novice / 2400 | Electric Light | Filament | Lantern | craftable; light/equipment behavior not certified |
| Electrical | Assistant / 2410 | Charged Ring | Copper Ring | Capacitor | craftable; equipment effect incomplete |
| Electrical | Associate / 2420 | Flow Specktrometer | Compass | Small Electrical Parts | craftable; detection behavior missing |
| Electrical | Technician / 2430 | Shocking Staff | Large Capacitor | Staff | craftable; weapon effect incomplete |
| Electrical | Engineer / 2440 | Chapeau of Magnetic Inversion | Top Hat | Electrical Coil | craftable; projectile defense missing |
| Electrical | Professor / 2450 | Healing Jacket | Electrical Harness | Leather Armour | craftable; equipment healing effect missing |
| Electrical | Doctorate / 2460 | Tesla Rod | Tesla Coil | Shocking Staff | craftable; weapon/projectile effect missing |
| Explosives | Novice / 2600 | Molotov Cocktail | Fuel | Rag | craftable; thrown ground-fire behavior missing |
| Explosives | Assistant / 2610 | Flash Grenade | Magnesium | Wine | craftable; area blindness integration incomplete |
| Explosives | Associate / 2620 | Smoke Grenade | Tom's Fertilizer | CK Sugar | craftable; smoke/visibility field missing |
| Explosives | Technician / 2630 | Stun Grenade | Stearic Acid | Saltpeter | craftable; area stun missing |
| Explosives | Engineer / 2640 | Explosive Grenade | Black Powder | Metal Can | craftable; full thrown/AOE behavior missing |
| Explosives | Professor / 2650 | Fire Obstruction | Liquid Soap | Kerosene | craftable; persistent fire obstruction missing |
| Explosives | Doctorate / 2660 | Dynamite | Nitroglycerin | Saltpeter | craftable; placement/timer/AOE behavior missing |
| Gun Smithy | Novice / 2800 | Hand Crafted Flintlock | Broken Flintlock | Small Metal Tube | craftable; weapon properties not fully certified |
| Gun Smithy | Assistant / 2810 | Fine Revolver | Revolver Parts | Revolver Chamber | craftable; weapon properties not fully certified |
| Gun Smithy | Associate / 2820 | Repeater Rifle | Hunting Rifle | Revolver Chamber | craftable; weapon properties not fully certified |
| Gun Smithy | Technician / 2830 | Hushed Revolver | Fine Revolver | Engine Muffler | craftable; noise/AI behavior missing |
| Gun Smithy | Engineer / 2840 | Looking-Glass Rifle | Marksman Rifle | Looking Glass | craftable; range/accuracy behavior not certified |
| Gun Smithy | Professor / 2850 | Hand Cannon | Clarington Rifle | Fancy Pistol | craftable; weapon properties not fully certified |
| Gun Smithy | Doctorate / 2860 | Elephant Gun | Hunting Rifle | Large Pipe | craftable; weapon properties not fully certified |
| Mechanical | Novice / 3000 | Spike Trap | Railroad Spike | Large Spring | craftable; trap placement/trigger missing |
| Mechanical | Assistant / 3010 | Trap Springer | Metal Casing | Small Spring | craftable; canonical alias/runtime identity ambiguous |
| Mechanical | Associate / 3020 | Auto Skeleton Key | Lock Picks | Small Spring | craftable; lock behavior not certified |
| Mechanical | Technician / 3030 | Eye Gear | Pocket Watch Parts | Eye Glasses | craftable; equipment Perception effect incomplete |
| Mechanical | Engineer / 3040 | Bear Trap | Metal Clamp | Large Spring | craftable; trap placement/trigger missing |
| Mechanical | Professor / 3050 | Clockwork Decoy | Clock Parts | Small Spring | craftable; summoned-decoy AI missing |
| Mechanical | Doctorate / 3060 | Mechanized Arachnid | Small Steam Engine | Large Gears | craftable; follower/summon AI missing |
| Smithy | Novice / 3200 | Pure Ore | Iron Ore | Steel | craftable; material identity present |
| Smithy | Assistant / 3210 | Balanced Sword | Pure Ore | Fine Hilt and Guard | craftable; weapon properties not fully certified |
| Smithy | Associate / 3220 | Feather-Weight Axe | Dwarven Ore | Oak Axe Handle | craftable; weapon properties not fully certified |
| Smithy | Technician / 3230 | Dwarven Gauntlets | Sheet Metal | Leather Gloves | craftable; equipment effect incomplete |
| Smithy | Engineer / 3240 | Helmet of Vision | Great Helm | Leather Straps | craftable; vision effect incomplete |
| Smithy | Professor / 3250 | Feather-Weight Chainmail | Leather Armor | Spool of Heavy Wire | craftable; armor properties not fully certified |
| Smithy | Doctorate / 3260 | Elite Platemail | Refined Dwarven Steel | Feather-Weight Chainmail | craftable; armor properties not fully certified |
| Therapeutics | Novice / 3400 | Elixir of Persuasion | Migraine Cure | Thermometer | craftable; timed skill effect missing |
| Therapeutics | Assistant / 3410 | Physical Prowess | Famous Blood Pills | Spirit of Camphor | craftable; timed attribute effect missing |
| Therapeutics | Associate / 3420 | Liquid Awareness | Migraine Cure | Doolittle's Glyceride | craftable; timed attribute effect missing |
| Therapeutics | Technician / 3430 | Increased Reflexes | Tincture of Arnica | Rheumatism Cure | craftable; timed Speed/DX effect missing |
| Therapeutics | Engineer / 3440 | Mind Marvel | Nerve Pills | Elixir of Persuasion | craftable; timed mental effect missing |
| Therapeutics | Professor / 3450 | Energizer | Quinine | Physical Prowess | craftable; timed attribute effect missing |
| Therapeutics | Doctorate / 3460 | Revitalizer | Mind Marvel | Energizer | craftable; combined timed effect missing |

## Found schematics and expertise

The current data catalog includes learned and found recipes—118 definitions in the audited loader. Found schematics must be acquired and learned independently of discipline degree. Their recipe may require cross-discipline expertise. A Technical Manual held in inventory contributes the character's Intelligence to one discipline's expertise; this source rule is `MISSING`.

Expertise controls whether a schematic can be executed; it does not grant the schematic. Aptitude also affects technological item behavior, so merely producing an item is not proof that using it is correct.

## Current implementation assessment

- Discipline ownership, sequential purchase, INT/level gates, effective-rank downgrade, aptitude contribution, catalog parsing, ingredient matching, consumption, output, and persistence: `COMPLETE` at the bounded generic-crafting layer.
- Source UI, all found-schematic acquisition paths, Technical Manuals, product-specific effects, traps, grenades, electrical devices, therapeutic duration, creature/summon products, and broad weapon/armor property integration: `PARTIAL` or `MISSING`.
- Healing Salve prototype 10059 versus the supported usable salve path 10079: `SOURCE AMBIGUITY`.
- Trap Springer aliases/identity: `SOURCE AMBIGUITY` pending runtime proof.

## Regression standard

Tests must derive degree prerequisites and schematic IDs from source rules; prove learned versus effective degree; prevent skipping degrees; preserve learned/found ownership; consume exact components once; create the source output; apply aptitude/expertise; and separately prove the product's runtime effect. A crafting-success test cannot certify an inert product.
