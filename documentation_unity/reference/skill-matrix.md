# Skill and Training Matrix

Source basis: official manual skill/training chapters, Prima mentor tables, retail skill data, `arcanum-ce` skill/stat code, and current OpenArcanum runtime/UI tests.

One purchased skill point equals four internal units. Effective rank is capped by the governing attribute. For attribute values 1–20 the cap is `3,3,3,3,3,7,7,7,11,11,11,15,15,15,19,19,19,20,20,20`. Training requires effective rank 1 for Apprentice, 9 for Expert, and 18 for Master, and advances sequentially.

`Stored` means ranks/training/point spending and persistence exist. It does not mean the action or training benefit is implemented.

| ID | Skill | Family | Attribute | Source Apprentice effect | Source Expert effect | Source Master effect | OpenArcanum runtime |
|---:|---|---|---|---|---|---|---|
| 0 | Bow | Combat | DX | speed +5 | fires two arrows | no range penalty | `PARTIAL`: bow/ammunition and bounded expertise exist; certify every timing/range case |
| 1 | Dodge | Combat | DX | a critical dodge gives 10% chance the attacker critically fails | chance becomes 50% | chance becomes 100% | `PARTIAL`: dodge participates in combat; full critical-conversion evidence incomplete |
| 2 | Melee | Combat | DX | speed +5 | ignore light penalty | cannot critically fail | `PARTIAL`: core and Master boundary exist; complete light/training integration not certified |
| 3 | Throwing | Combat | DX | speed +5 | range +50% | no range penalty | `MISSING/PARTIAL`: rank stored; full thrown-weapon action and training parity absent |
| 4 | Backstab | Thieving | DX | rear attack against unaware target bypasses armor | may use swords and axes | large critical-success bonus | `PARTIAL`: rank stored and combat has bounded support; all awareness/weapon/critical rules not certified |
| 5 | Pick Pocket | Thieving | DX | caught only on critical failure | item-size penalty is halved | planting an item cannot be detected | `MISSING`: source player action and training effects are not end-to-end |
| 6 | Prowling | Thieving | PE | ignore light; armor penalty halved | ignore nearby-object penalty and may walk | may conceal in combat and run | `MISSING`: HUD action kernel and detection state are absent |
| 7 | Spot Trap | Thieving | PE | ignore light | can detect magical traps | second chance after noncritical failure | `PARTIAL/MISSING`: stored; authoritative passive detection/training loop incomplete |
| 8 | Gambling | Social | IN | exposes more expensive inventory | exposes wielded merchant items | exposes normally unsellable items | `MISSING`: source gambling commerce/training effects absent |
| 9 | Haggle | Social | WP | merchant reports markup | merchants buy any item | can sell Not-for-Sale items | `PARTIAL`: pricing uses skill; complete reporting/inventory/NFS training effects absent |
| 10 | Heal | Social | IN | healing +50% | critical failures become ordinary failures and use no extra bandage | every success is critical and can heal crippling injuries | `PARTIAL/MISSING`: data stored; full bandage/action/injury loop incomplete |
| 11 | Persuasion | Social | CH | followers wait longer | maximum followers +1 | alignment reaction no longer blocks joining | `PARTIAL`: dialogue/follower cap integration exists in bounded form; all training effects not certified |
| 12 | Repair | Technological | IN | maximum-HP loss is 5% | maximum-HP loss is 1% | no maximum-HP loss except on critical failure; repairs broken items at 5% loss | `MISSING`: HUD action, condition/durability, and training loop absent |
| 13 | Firearms | Technological | PE | speed +5 | called-shot penalty reduced to two thirds | no range penalty | `PARTIAL`: ranks and ranged infrastructure exist; source firearms/ammunition/training parity not certified |
| 14 | Pick Locks | Technological | DX | action time halved | ignore light | lock difficulty penalty halved | `PARTIAL`: lock action exists at a boundary; timing/light/difficulty training effects incomplete |
| 15 | Disarm Traps | Technological | PE | ignore light | critical success yields a component | second chance after noncritical failure | `MISSING`: HUD action and trap/component training loop absent |

## Trainers

The official manual establishes the training model; the Prima guide pp. 30–32 supplies the retail mentor/location corpus for all sixteen skills and levels. OpenArcanum can store training and dialogue scripts can grant it, but this audit did not establish that every mentor, dialogue condition, payment, reaction prerequisite, and location schedule is campaign-reachable. Trainer availability is therefore `PARTIAL`, even where the rank model itself is complete.

## Shared gaps

1. Training effects need a single authoritative dispatch rather than ad hoc UI-only flags.
2. Environmental inputs—light, movement mode, armor penalty, range, target awareness, merchant inventory class, and item condition—must be available to skill resolution.
3. Critical success/failure must return typed outcomes so training can transform them without duplicating chance rolls.
4. Dialogue-granted training must enforce source rank, money, reaction, and quest prerequisites.
5. Save/load must preserve learned training, not cache its derived effects.

## Regression standard

For each skill, tests should prove point cost, governing-stat cap, effective-rank loss/restoration after attribute changes, sequential training requirements, persistence, and each implemented training effect at its behavior boundary. UI display tests alone do not certify the skill.
