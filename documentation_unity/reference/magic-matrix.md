# Magic Matrix

Source basis: official manual magic appendix, Prima spell tables/details pp. 62–74, retail spell/MES data, `arcanum-ce` spell rules, and the current OpenArcanum production spell catalog.

Current-branch reconciliation: 2026-10-12 against `5926bb42ff0f1fe261ba8deb71716d149733c9d3`. Character Creation now projects all source spell rows, but presentation catalog coverage is not runtime implementation: `PhaseOneSpellCatalog` still contains exactly the four spells identified below.

All colleges have five sequential ranks. Rank prerequisites are WP/level `6/1, 9/1, 12/5, 15/10, 18/15`, with minimum INT 5. A spell costs one character point and contributes one magic point. Maintained-spell capacity is `INT / 4`. “Upkeep” below is fatigue per source upkeep interval. OpenArcanum production support is `Yes` only for IDs 15, 55, 60, and 66; all other rows are `MISSING` even if a parser, enum, icon, or older audit knows the name.

| ID | College / rank | Spell | Cast / upkeep | Source behavior summary | Production |
|---:|---|---|---|---|---|
| 0 | Conveyance 1 | Disarm | 5 / — | target drops equipped weapon | No |
| 1 | Conveyance 2 | Unlocking Cantrip | 10 / — | attempts to unlock portal or container | No |
| 2 | Conveyance 3 | Unseen Force | 15 / — | projects force and moves/knocks target | No |
| 3 | Conveyance 4 | Spatial Distortion | 25 / — | relocates caster to a valid nearby tile | No |
| 4 | Conveyance 5 | Teleportation | 50 / — | transports party to a world-map destination | No |
| 5 | Divination 1 | Sense Alignment | 5 / 1 | reveals target alignment while maintained | No |
| 6 | Divination 2 | See Contents | 10 / — | reveals container/creature inventory | No |
| 7 | Divination 3 | Read Aura | 15 / — | reveals target attributes/properties | No |
| 8 | Divination 4 | Sense Hidden | 25 / 5 | reveals hidden objects/creatures while maintained | No |
| 9 | Divination 5 | Divine Magick | 30 / — | reveals magical properties/effects | No |
| 10 | Air 1 | Vitality of Air | 5 / 1 | maintained Constitution increase | No |
| 11 | Air 2 | Poison Vapours | 10 / 5 | creates poisonous area cloud | No |
| 12 | Air 3 | Call Winds | 15 / — | directional wind/knockback area effect | No |
| 13 | Air 4 | Body of Air | 25 / 2 | transforms target into air form while maintained | No |
| 14 | Air 5 | Call Air Elemental | 50 / 5 | summons maintained air elemental | No |
| 15 | Earth 1 | Strength of Earth | 5 / 1 | maintained Strength increase | Yes |
| 16 | Earth 2 | Stone Throw | 10 / — | launches damaging stone projectile | No |
| 17 | Earth 3 | Wall of Stone | 15 / 1 | creates maintained blocking wall | No |
| 18 | Earth 4 | Body of Stone | 25 / 2 | transforms target into stone form while maintained | No |
| 19 | Earth 5 | Call Earth Elemental | 50 / 5 | summons maintained earth elemental | No |
| 20 | Fire 1 | Agility of Fire | 5 / 1 | maintained Dexterity increase | No |
| 21 | Fire 2 | Wall of Fire | 10 / 1 | creates maintained damaging fire wall | No |
| 22 | Fire 3 | Fireflash | 15 / — | area fire damage | No |
| 23 | Fire 4 | Body of Fire | 25 / 10 | transforms target into fire form while maintained | No |
| 24 | Fire 5 | Call Fire Elemental | 50 / 5 | summons maintained fire elemental | No |
| 25 | Water 1 | Purity of Water | 5 / 1 | maintained Beauty increase | No |
| 26 | Water 2 | Call Fog | 10 / 1 | creates maintained visibility-obscuring fog | No |
| 27 | Water 3 | Squall of Ice | 15 / 5 | creates damaging/freezing ice area | No |
| 28 | Water 4 | Body of Water | 25 / 2 | transforms target into water form while maintained | No |
| 29 | Water 5 | Call Water Elemental | 50 / 5 | summons maintained water elemental | No |
| 30 | Force 1 | Shield of Protection | 5 / 1 | maintained defensive shield | No |
| 31 | Force 2 | Jolt | 10 / — | direct electrical damage | No |
| 32 | Force 3 | Wall of Force | 15 / 2 | creates maintained blocking force wall | No |
| 33 | Force 4 | Bolt of Lightning | 25 / — | line/projectile electrical damage | No |
| 34 | Force 5 | Disintegrate | 50 / — | destroys target on successful resolution | No |
| 35 | Mental 1 | Charm | 5 / 1 | improves target reaction while maintained | No |
| 36 | Mental 2 | Stun | 10 / — | applies temporary stun | No |
| 37 | Mental 3 | Drain Will | 15 / 1 | maintained Willpower reduction | No |
| 38 | Mental 4 | Nightmare | 20 / — | fear/mental attack | No |
| 39 | Mental 5 | Dominate Will | 25 / 5 | controls target while maintained | No |
| 40 | Meta 1 | Resist Magick | 5 / 1 | maintained magic resistance | No |
| 41 | Meta 2 | Disperse Magick | 10 / — | removes a magical effect | No |
| 42 | Meta 3 | Dweomer Shield | 15 / 5 | suppresses incoming/outgoing magic while maintained | No |
| 43 | Meta 4 | Bonds of Magick | 35 / 5 | prevents target spellcasting while maintained | No |
| 44 | Meta 5 | Reflection Shield | 30 / 5 | reflects qualifying spells while maintained | No |
| 45 | Morph 1 | Hardened Hands | 5 / 1 | maintained unarmed damage/hand transformation | No |
| 46 | Morph 2 | Weaken | 15 / 2 | maintained Strength reduction | No |
| 47 | Morph 3 | Shrink | 20 / 2 | maintained size/stat transformation | No |
| 48 | Morph 4 | Flesh to Stone | 20 / duration | temporarily petrifies target | No |
| 49 | Morph 5 | Polymorph | 30 / 5 | maintained creature transformation | No |
| 50 | Nature 1 | Charm Beast | 8 / — | pacifies/improves reaction of an animal | No |
| 51 | Nature 2 | Entangle | 5 / 2 | maintained movement restraint | No |
| 52 | Nature 3 | Control Beast | 15 / 2 | controls an animal while maintained | No |
| 53 | Nature 4 | Succour Beast | 20 / 4 | summons/assists with a beast while maintained | No |
| 54 | Nature 5 | Regenerate | 25 / 5 | maintained health regeneration | No |
| 55 | Necromantic Black 1 | Harm | 5 / — | direct magical damage | Yes |
| 56 | Necromantic Black 2 | Conjure Spirit | 10 / 3 | calls corpse spirit for interaction while maintained | No |
| 57 | Necromantic Black 3 | Summon Undead | 15 / 2 | summons maintained undead | No |
| 58 | Necromantic Black 4 | Create Undead | 20 / 3 | animates corpse as maintained undead | No |
| 59 | Necromantic Black 5 | Quench Life | 50 / — | lethal life-draining attack | No |
| 60 | Necromantic White 1 | Minor Healing | 5 / — | restores hit points | Yes |
| 61 | Necromantic White 2 | Halt Poison | 8 / — | stops poison progression | No |
| 62 | Necromantic White 3 | Major Healing | 20 / — | restores greater hit points | No |
| 63 | Necromantic White 4 | Sanctuary | 15 / 5 | maintained protection from hostile targeting | No |
| 64 | Necromantic White 5 | Resurrect | 50 / — | returns valid dead target to life | No |
| 65 | Phantasm 1 | Illuminate | 3 / 1 | creates maintained light | No |
| 66 | Phantasm 2 | Flash | 10 / duration | applies temporary blindness | Yes |
| 67 | Phantasm 3 | Blur Sight | 15 / 2 | maintained visual/attack penalty | No |
| 68 | Phantasm 4 | Phantasmal Fiend | 20 / 4 | maintained illusionary attacker/fear effect | No |
| 69 | Phantasm 5 | Invisibility | 30 / 6 | maintained invisibility | No |
| 70 | Summoning 1 | Plague of Insects | 3 / 1 | maintained insect damage/distraction area | No |
| 71 | Summoning 2 | Orcish Champion | 10 / 2 | summons maintained orc champion | No |
| 72 | Summoning 3 | Guardian Ogre | 15 / 3 | summons maintained ogre | No |
| 73 | Summoning 4 | Hellgate | 30 / 6 | opens maintained demon-summoning gate | No |
| 74 | Summoning 5 | Familiar | 60 / — | permanently creates caster's familiar under source limits | No |
| 75 | Temporal 1 | Magelock | 5 / 1 | magically locks portal/container while maintained | No |
| 76 | Temporal 2 | Congeal Time | 10 / 2 | slows target while maintained | No |
| 77 | Temporal 3 | Hasten | 20 / 3 | increases target Speed while maintained | No |
| 78 | Temporal 4 | Stasis | 25 / 4 | freezes target in time while maintained | No |
| 79 | Temporal 5 | Tempus Fugit | 40 / 5 | accelerates caster's side relative to opponents | No |

## Implemented kernel versus missing families

The existing four-spell catalog proves several reusable mechanics: immediate damage, immediate healing, maintained attribute modification, finite blindness, target validation, resistance, fatigue, upkeep/end, and persistence. It does not implement content by implication.

Missing families include portal/container operations, knockback and spatial placement, world teleportation, divination/inspection, ground areas and walls, projectiles, transformations, elementals and other summons, charm/control/fear, anti-magic/dispel/reflection, corpse dialogue/animation/resurrection, invisibility/detection, poison control, regeneration, temporal scheduling, and familiar ownership.

## Regression standard

Every spell needs tests for learn prerequisites, sequential ownership, point/aptitude changes, target legality, range/line of sight where applicable, cast fatigue, resistance, exact effect, upkeep, cancellation, death/map-transition behavior, AI use where source permits, and save/load. A catalog row or icon is not implementation evidence.
