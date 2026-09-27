# M9A NPC Combat AI Closure Audit

Date: 2026-09-27

## Result

M9A is complete. The post-Phase-2 source review found no genuine gap in the bounded NPC-combat-AI kernel that can be
closed without crossing into a separately owned gameplay system.

The completed kernel covers stable combat focus, deterministic eligible-target discovery, approach/attack/yield
intent, supported unarmed/Bow/melee-weapon selection, ammunition requirements and fallback, representable danger
reaction, repeated turn-based actions, and real-time READY/BUSY scheduling. Every mutation still routes through the
existing M8 combat, inventory, equipment, vitality, defeat, consequence, navigation, and save authorities.

## Source Boundary

The closure pass compared the implemented production path with the relevant Arcanum CE behavior in `ai.c`,
`combat.c`, `critter.c`, `anim.c`, `party.c`, `teleport.c`, `level.c`, `object.c`, `script.c`, and
`sector_object_list.c`.

The remaining source-supported behavior is outside M9A:

- follower membership, leader relationships, ally classification, follower protection, travel, and follower combat
  participation belong to M9B;
- spells, technology, healing items, grenades, and richer consumable selection require their future authoritative
  systems;
- fleeing, surrender, backoff, world-item scavenging, general schedules, stealth/concealment, decoys, social guards,
  and campaign-specific protection are not necessary to the bounded combat kernel and require independent audits;
- source party-tail ordering details are not reliable enough in the available port to justify invented ordering
  semantics beyond the deterministic order already required by OpenArcanum;
- perception, concealment, reaction, alignment, and campaign-script conditions that are not yet authoritative remain
  unsupported rather than approximated.

No rejected or unsupported branch was silently mapped to an existing action. Unsupported behavior remains fail
closed, and the completed M9A path does not acquire follower, dialogue, magic, technology, or general-simulation
authority.

## Acceptance Evidence

The validated M9A baseline remains:

- Phase 2 focused tests: **12/12**;
- Phase 1 focused tests: **15/15**;
- M8A-M8I combat regressions: **185/185**;
- Phase 1 plus M8 regressions: **200/200**;
- complete EditMode suite: **869/869**;
- failed/skipped/inconclusive: **0/0/0**;
- Unity compilation: clean;
- final cleared Unity Console: **0 logs, 0 warnings, 0 errors**;
- `git diff --check`: clean.

Physical Play Mode proofs already recorded by the Phase 1 and Phase 2 audits cover the production unarmed, Bow,
melee-weapon, pursuit, hard-obstruction, ammunition, fallback, real-time scheduling, defeat, termination, presentation,
and Save V1 paths. Repeating those proofs would add no closure evidence.

## Closure

M9A is formally closed. Work may proceed to an independently audited M9B follower/party foundation without reopening
or broadening M9A.
