# M5C Bounded Production Trainer Dialogue

## Pre-implementation source audit (2026-09-13)

This section was written before production changes. It records the behavior recovered directly from
`arcanum-ce/src/game/dialog.c`, `reaction.c`, and `skill.c`, plus an audit of the retail dialogue and placed-mobile
resources mounted from the user's local original data.

### Exact `t:` grammar and parser representation

- A dialogue response is authored as `t:` followed by a list of source skill IDs, for example the selected authentic
  line `T:11`. Whitespace is accepted. The original `dialog_parse_params` grammar is a comma-separated list of decimal
  integers or inclusive decimal ranges (`start-end`), capped at 100 expanded values.
- Source skill IDs are the unified order already represented by `CharacterSkill`: 0-11 are basic skills and 12-15
  are technical skills. The selected ID 11 is Persuasion.
- The `.dlg` parser stores the response text as authored. `dialog.c::sub_416C10` recognizes the `t:` prefix, resolves
  only its visible top-level label from the PC generated-dialog 500-599 range, stores action kind 5, stores the authored
  response target as the return target, and retains the payload for the special handler.
- The payload does **not** encode a training tier. The `t:` handler always requests Apprentice (source value 1).
  Expert (2) and Master (3) remain real packed M4C states but are not granted by this handler.
- Malformed/unknown skill payloads are not present in the audited retail resources. Production M5C will reject them
  explicitly at the parser/execution boundary instead of relying on source `atoi` coercion.

### Source special-handler flow

1. Selecting the authored `t:` response runs that response's normal dialogue action/effect first.
2. Action 5 expands the skill list and replaces the current dialogue view with the class-specific source prompt keyed
   at 3000. Every listed skill remains visible; an additional source-generated cancel option returns to the authored
   response target.
3. Selecting a skill invokes action 6. Eligibility is evaluated at this point, not while the original `t:` response or
   skill option is being displayed.
4. If current training is anything other than None, class-specific failure key 4000 is shown with one continue option.
5. Otherwise the engine temporarily tries to assign Apprentice through `basic_skill_training_set` or
   `tech_skill_training_set`. A result still equal to None shows insufficient-rank key 5000. A successful probe is
   immediately restored to None before offering payment.
6. The base price is exactly 100 Gold. `reaction.c::sub_4C1150` adjusts it from the trainer's reaction to the PC:
   reaction below 0 costs 200%; 0-49 costs `2 * (100-reaction)%`; otherwise it costs
   `(120 - floor(2*reaction/5))%`, with reaction capped at 100 for this calculation. A computed price of 1 becomes 2.
7. The payment view uses the class-specific source price prompt key 1000 and generic Yes/No options. No leaves training
   unchanged and returns to the authored target. Insufficient funds shows class-specific key 2000 and a continue option.
8. Yes first transfers the exact Gold amount from PC to trainer, then invokes action 7, which assigns Apprentice through
   the ordinary authoritative skill setter. The source implementation does not inspect the return value on this second
   assignment because the eligibility probe already succeeded.
9. Success shows class-specific key 6000 and a class-specific acknowledgement option keyed at 1000, which returns to
   the original authored target.

The handler itself reads no reaction/alignment gate, consumes no character points, mutates no dialogue-local script
state, and checks no separate governing attribute. The existing effective skill rank already incorporates the source
governing-attribute cap. The source skill setter requires rank 1 for Apprentice, rank 9 for Expert, and rank 18 for
Master, and enforces sequential upward transitions; only the Apprentice path is reachable from `t:`. Trainer capability
is the dialogue-authored payload itself: the engine does not compare the trainer's own rank or training tier.

Training and Gold must therefore remain atomic at the Yes action. Any production preflight or later failure must leave
both authoritative domains unchanged. Presentation can select a displayed option but cannot decide eligibility,
payment, or training.

### Response availability and post-training behavior

The authored `t:` response and every listed skill remain visible even when the PC lacks rank, lacks Gold, or is already
trained. The special handler reports those conditions only after the relevant selection. Successful training returns to
the authored dialogue target. Re-entering the same trainer conversation can show `t:` again; choosing the trained skill
produces the already-trained result and cannot charge or train twice.

### Authentic candidate audit

The retail-data audit found 58 dialogue resources containing `t:`, 56 with placed NPCs, and 236 placed candidate NPCs.
Representative classifications:

- **GREEN — selected:** Black Root mayor, dialogue 1009, `T:11` (Persuasion), already reached and physically validated
  by M5B after quest 1005 completion. Its campaign gates, exact NPC, stable sector, generated top-level label, reaction,
  Gold reward, and dialogue vocabulary are already inside the production boundary. M5C adds only the special handler.
- **YELLOW:** Black Root apothecary, dialogue 1012, `T:3,10` (Throwing/Heal), and generic merchant trainers such as
  dialogue 1813, `T:9` (Haggle). Their training payloads are simple, but using them would also widen the currently
  audited production dialogue/script and generated-class-text boundary.
- **RED for this slice:** quest/follower/faction/combat-dependent trainers and broad shared guard/merchant dialogues.
  Their `t:` payload is mechanically compatible, but reaching or disambiguating them requires one or more explicitly
  deferred gameplay domains or broad trainer discovery.

Selected source fixture:

- NPC ObjectID: `G_787AD4AB_9061_2B4E_A691_F582800B2BB3`
- Prototype: 17088
- Map sector: `maps/arcanum1-024-fixed/96636765255.sec`
- Dialogue/SAP_DIALOG resource: 1009
- Training response: line 5 (and equivalent authored follow-ups 474/494), `T:11`, return target 20
- Skill/tier: Persuasion / Apprentice
- Precondition: quest 1005 Completed and mayor reaction at least 41 for the enclosing authored response
- Training eligibility: PC and trainer are the active critters, Persuasion is None, effective rank at least 1
- Character-point cost: none
- Base Gold price: 100; expected selected-fixture price after M5B is 99 at reaction 53
- Post-training result: Apprentice state in `CharacterProgressionService`; re-selection reports already trained and
  performs no payment or mutation

### Smallest proposed production change set

1. Preserve token payload through display-text substitution and strictly parse `t:` into typed source skills.
2. Add a typed dialogue training request/result orchestration layer whose request contains trainee ObjectID, trainer
   ObjectID, `CharacterSkill`, and requested `SkillTrainingLevel.Apprentice`.
3. Route eligibility and assignment exclusively through `CharacterProgressionService`; extend its transaction snapshot
   to include skill/training fields.
4. Add a narrow, atomic Gold transfer operation to the coordinator's existing inventory/stack authority.
5. Model the source training skill/payment/result subviews as transient `ProductionDialogueSession` state and bind
   source-generated prompts/options from the loader. The presenter remains observation/selection only.
6. Admit `t:` only for the already-audited dialogue 1009 compatibility profile, then add focused tests and one physical
   Play Mode validator around the exact mayor fixture.

## Implementation and validation

Completed on 2026-09-13 with Unity 6000.0.71f1 on `feature/inventory-commands`.

### Final ownership and call graph

```text
physical Game-view NPC click
  -> PlayerClickMoveInput / PlayerInteractionController (stable mayor ObjectID)
  -> ProductionDialogueSession (dialogue 1009, authored response 5)
  -> DialogLine.TokenPayload (strict source `11` -> CharacterSkill.Persuasion)
  -> transient SkillSelection / Payment / Result dialogue views
  -> DialogueTrainingService (typed trainee, trainer, skill, Apprentice request)
       -> CharacterProgressionService.PreviewTrainingLevel / SetTrainingLevel
       -> CharacterDerivedStatService.GetReaction (source price input)
       -> WorldMapSessionCoordinator.TryTransferGold
            -> existing persistent Gold stacks / inventory capacity authority
  -> return to authored dialogue target 20

WorldObjectSectorLoader
  -> binds source generated-dialogue and skill MES tables
  -> records effective NPC social class in PersistentObjectState

ProductionDialoguePresenter
  -> observes NpcText and AvailableResponses
  -> submits only the selected response index
```

The authoritative state therefore remains in the session-owned character progression, derived-stat, campaign,
dialogue, and inventory services. `WorldObject`, sprite owners, the loader, and Unity presenters neither own training
nor decide eligibility, price, payment, or assignment.

### Production contract

- `DialogLine` retains both a normalized token code and the authored payload after generated display text replaces the
  visible `t:` label. The production parser accepts the audited decimal/list/range grammar, rejects malformed or
  out-of-range input explicitly, and exposes no partially parsed list after failure.
- M5C admits the special handler only for the already-audited dialogue 1009 profile and only when the loader has bound
  the source training text tables. All other non-generic special tokens retain the prior strict fail-closed behavior.
- `DialogueTrainingRequest` is typed and carries stable trainee/trainer ObjectIDs, `CharacterSkill`, and
  `SkillTrainingLevel`. The `t:` path constructs only `Apprentice`; it does not infer Expert or Master.
- The authored list is the trainer-capability boundary. Eligibility uses the production PC, a real session-owned NPC,
  the exact offered skill, no existing training, and M4C's effective-rank/sequential-training rules. The top-level
  response and skill remain visible when those later checks will fail, matching the source flow.
- Cost is base 100 Gold transformed by the exact audited reaction bands. The selected reaction 53 fixture costs 99.
  Gold transfer and training assignment are one transaction across existing authoritative services. The transaction
  snapshots purchased skills/training as well as progression and inventory values, so any failure restores both
  domains without a partial charge or assignment.
- Skill selection cancel, payment No, insufficient rank, insufficient Gold, and already-trained results all return to
  the authored target without unauthorized mutation. A successful result assigns Persuasion/Apprentice once, moves
  exactly 99 Gold from PC to the real mayor, and cannot be repeated.
- Source prompts/options come from the original normal/dumb, gender-direction, NPC-social-class generated-dialogue
  tables and `mes/skill.mes`. Presentation expands the existing PC/NPC substitutions but owns no source decision.

### Focused and physical validation

- Focused M5C EditMode: **21 passed, 0 failed, 0 skipped, 0 inconclusive**. Coverage includes authentic payload
  retention, strict list/range parsing and malformed rejection, reaction-price boundaries, offered-skill/rank/tier
  eligibility, insufficient funds, exact successful transfer, duplicate protection, source subview text/ordering,
  cancel/No/result behavior, and visual rebuild/NPC rebind persistence.
- Complete EditMode regression suite: **478 passed, 0 failed, 0 skipped, 0 inconclusive**. This includes all prior
  dialogue/quest, character progression/derived stats, inventory/equipment/capacity, interaction, navigation,
  world-session, and portal tests. Unity compilation was clean.
- Computer Use Play Mode used the exact mayor ObjectID/prototype/dialogue in the authentic sector. A literal click on
  the visible mayor drove normal PC navigation and Talk. Physical selections opened response 5 (`t:11`), selected
  Persuasion, displayed the exact 99-Gold source prompt, confirmed Yes, displayed the source success result, and
  acknowledged back to line 20. The result was Persuasion/Apprentice, PC Gold 100 -> 1, mayor Gold 0 -> 99.
- A second literal click and physical `t:`/Persuasion selection displayed the source already-trained rejection. Its
  acknowledgement returned to line 20 with both balances and training unchanged. Original -> Enhanced -> restored
  rebuilds plus mayor-sector unload/reload preserved the same PC, mayor, progression record, training state, and Gold
  stacks with one coordinator, loader, navigation controller, interaction controller, dialogue presenter, PC
  presentation, mayor presentation, and sprite owner per stable identity. The validation recorded **0 new warnings
  and 0 errors**.

### Deliberately deferred

The slice does not add barter/economy UI, additional trainer discovery, other special dialogue tokens, Expert/Master
trainer routes, trainer skill inspection, character-leveling UI, backgrounds, equipment/spell modifiers, combat,
followers, travel, quest expansion, inventory UI, or save serialization. The exact normal selected fixture is covered;
source "dumb" background classification remains deferred with the broader background domain, while the existing
effective-Intelligence `<= 4` source rule is supported.
