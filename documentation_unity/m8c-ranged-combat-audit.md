# M8C Ranged Combat Source Audit

## Boundary

M8C adds one source-faithful equipped ranged transaction to the existing M8B combat coordinator. The bounded path is a bow using arrows. Firearms, throwing, projectile presentation, reload/jam/condition behavior, cover penalties, called shots, criticals, Fate, animation timing, encounters, and AI weapon selection remain deferred.

## Audited source

- Repository: `D:/OpenArcanum/Research/Repositories/arcanum-ce`
- Commit: `a7ff41b300ef712f0e7d088183a3d08957110cdd`
- Primary files: `src/game/combat.c`, `item.c`, `skill.c`, `anim.c`, `ai.c`, `object.c`, and `location.c`

The implementation follows these source facts:

- `item_weapon_skill` selects Throwing for the boomerang flag, Firearms for range >= 3 with negative technological complexity and bullet/fuel/charge ammunition, Bow for arrow ammunition, and Melee otherwise.
- `combat_attack_cost` costs 1 AP above speed 24, 2 AP above speed 20, otherwise `max(1, 8 - speed / 3)` with integer division.
- Ranged distance is Chebyshev distance (`max(abs(dx), abs(dy))`).
- Bow/Firearms effectiveness is `5 * effective skill + 25`. Weapon bonus-to-hit lowers difficulty; each point of Strength below the weapon minimum adds 5 difficulty; each tile beyond Perception / 2 adds 5 difficulty. Armour difficulty remains `effectiveness * (AC / 2) / 100`.
- Projectile traversal ignores intervening critters, but walls, closed portals, and non-shoot-through blocking scenery stop the shot. Target-tile objects are not treated as intervening blockers.
- Ammo is preflighted before the attack commits. AP is committed before ammo, and ammo is consumed before hit resolution, including on a miss. A depleted stack is destroyed/tombstoned.
- Ranged weapon damage does not receive the melee Strength damage bonus. The bounded path accepts Normal and Fatigue damage only and uses the existing M4B resistance/vitality authority.

## Authentic fixture

The selected source fixture is the real placed bow and arrow stack in `maps/arcanum1-024-fixed/101602821844.sec`:

- Bow ObjectID `G_1575DBCA_4990_C243_8184_524D51F7D533`, prototype 6055
- Arrow stack ObjectID `G_FBFA4631_D97D_D740_9636_F131B2FD9F7B`, prototype 7058, authored quantity 70
- Bow flags `0x0000000E`, arrow ammo type 0, consumption 1, range 15, speed 8, bonus-to-hit 0, minimum Strength 10
- Bow damage: Normal 1-10 and Fatigue 2-5
- Exact AP cost: 6

The target remains the authentic M8B Polar Bear Cub fixture `G_9B807B01_A142_4949_80CE_5A085F3BEEB1` from `maps/arcanum1-024-fixed/47781512457.sec`. Focused EditMode tests use source-shaped state with these exact prototype combat values. The physical validator acquires the bow and arrows from their authentic sector through the production M3A transfer and M3C equipment paths, then transitions to the authentic bear sector for combat.

## Transaction and ownership

`CombatStateService.Attack` remains the single action boundary. The coordinator owns equipment and ammo stack state; Unity presentation is not combat authority. Weapon/ammo facts are resolved from the current prototype source and therefore do not change save format V1. All failure checks (equipment, supported weapon family, compatible ammo, range, line of fire, AP, and damage profile) complete before mutation or RNG. On commit: AP is spent, one compatible deterministic ammo stack is consumed, hit/dodge/damage RNG runs, and vitality mutations apply.

## Deliberate limits

M8C models hard projectile blockers but defers the original engine's partial-cover numeric penalty. It supports the authentic bow/arrow transaction only; other ranged skill families return an explicit unsupported-weapon failure. NPC ammo exemptions are not included in this PC-owned fixture path.

Firearms, throwing, projectile presentation, reload/jam/condition behavior, called shots, critical success/failure and Fate, death/corpses, loot/XP, combat AI, spells, real-time scheduling, and animation timing remain outside M8C. The selected authentic sectors have portals, but none whose open state alone changes the tested projectile line; focused source-grid coverage therefore proves closed-blocks/open-permits semantics without inventing an authentic physical result.

## Physical production validation

Unity 6000.0.71f1 Play Mode validation exercised the production coordinator, authentic source data, M3 inventory/equipment state, M4B vitality, and M8B turn lifecycle:

- The production PC acquired and equipped the authentic bow while the exact authored arrow stack remained authoritative. No combat-local weapon or ammo cache was introduced.
- A seeded hit spent exactly 6 AP, consumed exactly one arrow, rolled 10 Normal/5 Fatigue, and the target's source resistances produced 8 Normal/5 Fatigue through `CharacterVitalityService`. Turn advancement occurred once and no duplicate damage was observed.
- A seeded miss spent the same 6 AP and one arrow before hit resolution, applied zero vitality damage, and advanced the turn once.
- With two compatible stacks, the lower stable ObjectID was selected reproducibly, only that stack changed, and exhausting it caused the next attack to select the remaining authored stack. A quantity-1 stack tombstoned at zero; a subsequent attack returned `NoAmmo` without AP, RNG, damage, or stale identity use.
- Chebyshev range 15 succeeded at the boundary and out-of-range failed atomically. Authentic wall/scenery geometry blocked the shot; moving to clear source-grid geometry permitted it. Intervening critters remained non-blocking.
- Unequipping the bow, equipping an unrelated item, incompatible bullets, insufficient AP, inactive/out-of-turn combat, invalid or unavailable actors/targets, blocked LOS, and out-of-range requests preserved AP, ammo, equipment, vitality, placement, current actor, and round.
- A 1 AP combat move updated authoritative placement; range and LOS recomputed from that new tile and a 6 AP ranged attack completed in the same turn.
- Original -> Enhanced -> Original presentation rebuild preserved combat, participant/current-turn state, AP, equipped bow identity, ammo identity/quantity, and vitality, after which another ranged attack succeeded.
- Ending and restarting combat retained committed equipment, ammo, placement, and vitality. V1 save/load, sector unload, and map transition normalized transient combat to Inactive while retaining those authoritative domains.

The physical run reported `warnings=0; errors=0`. Its exact success evidence was: AP 6; hit raw 10/5; mitigated 8/5; miss damage 0; stable-ID ammo selection; depletion `tombstoned->NoAmmo`; range 15; authentic geometry blocked/clear; movement plus attack 1+6 AP; and graphics Original -> Enhanced -> Original.

## Automated validation

- Focused M8C EditMode: **12 passed, 0 failed, 0 skipped, 0 inconclusive**.
- Required regression matrix: **377 passed, 0 failed, 0 skipped, 0 inconclusive** across M8B, M8A, M7E/M7D/M7C/M7B/M7A, M6C/M6B/M6A, M4B/M4C/M4D, M3C/M3D, PlayerNavigation, and WorldSessionState.
- Complete EditMode: **714 passed, 0 failed, 0 skipped, 0 inconclusive**.
- Final compilation: clean. The complete suite emitted the same 6 known dialogue-compatibility warnings and 0 errors; there were 0 unexpected warnings.

## Next boundary

The recommended separately authorized M8D slice is bounded defeat/death and corpse-state combat resolution: source thresholds and eligibility, deterministic combat exit, persistent defeated-state ownership, and save/reload behavior for one authentic fixture. Loot transfer, XP awards, critical tables/effects, AI, spells, and real-time combat should remain separate.
