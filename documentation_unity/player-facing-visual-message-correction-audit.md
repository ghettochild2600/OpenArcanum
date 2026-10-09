# Player-Facing Visual / Message Correction Audit

Date: 2026-10-09
Branch: `feature/session-save-load`
Starting HEAD: `a9a3642efc65d6024943a2ab26f3335cfea064f3`

## Boundary

This pass addresses only the defects visible in the supplied physical-review screenshots: Character Editor help,
raw prototype identifiers in the gameplay message window, synthetic dialogue acknowledgement, ordinary NPC speech
presentation, and the corpse `LOOT` hover question. It does not begin another UI family or change gameplay authority.

RetailSandbox did not provide a reliable executable window during the available certification work. Accordingly,
no result below is labelled retail-physically-certified. Original behavior is instead traced to `arcanum-ce` and
retail data, then physically validated in the production Unity path. The supplied retail-looking screenshots remain
review evidence, not independent executable certification.

## Source evidence

- Character help: `arcanum-ce/src/ui/charedit_ui.c:1592` routes hover help through `charedit_show_hint` and an
  `UI_MSG_TYPE_STAT`. `arcanum-ce/src/ui/intgame.c:5841-5868` splits the first newline, draws the title in source
  rectangle `{290,63,291,19}` with the Morph 15 font, and the body in `{290,87,291,54}` with the Flare 12 font.
- Object names: `arcanum-ce/src/game/object.c:3934-3991` resolves ordinary objects from `OBJ_F_DESCRIPTION` through
  `description_get`; `arcanum-ce/src/game/description.c:123-140` selects the system/module description MES. Raw
  prototype numbers are not the ordinary display-name path.
- NPC speech: `arcanum-ce/src/ui/dialog_ui.c:431-459` routes ordinary speech to `tb_add`. In
  `arcanum-ce/src/game/tb.c:407-439`, text is rendered into a color-keyed bubble and attached to the speaking object.
  `tb.c:205-236` proves the transparent blue color key, source font ART ID 229, centered text, and shadow. The source
  content surface is 200 by 200 pixels; it is not a modern translucent dark panel.
- Dialogue response acknowledgement: no source path was found that writes `Response selected.` to the gameplay
  message window. The literal was local controller feedback.
- Corpse hover: the available source/retail evidence was insufficient to prove whether the displayed `LOOT` label,
  capitalization, backing, and cursor relationship differ from retail. The presentation was therefore not changed.

## Production corrections

### Character Editor help

The single-line `Editor Feedback` projection was replaced with a source-shaped title and body. The first source
newline is authoritative separation; the title uses source font 27 at `{290,63,291,19}`, and the body uses source
font 229 in `{290,87,291,54}` with at most three wrapped lines. The body is read-only and creates no input caret.
The physical validator now enters the live Strength hover state and checks the separated title, exact body bounds,
wrapped-line limit, and absence of an `InputField`.

### HUD item names

`WorldMapSessionCoordinator` now owns the bounded display-name resolver used by the UI controller. The production
sector loader binds `mes/description.mes`; the resolver follows the prototype description ID and falls back to the
object type without exposing the prototype number. HUD ready/equip paths now use that shared authority. The observed
`WEAPON 6049` / `READY WEAPON 6049` state now presents `UNARMED` / `READY UNARMED` for the production fixture.

This is intentionally not a complete reimplementation of every special case in `object_examine`: unidentified
items, keys, PC names, and NPC observer-dependent names remain separate future boundaries if their callers need them.

### Dialogue feedback and speech

Successful response selection no longer writes the synthetic `Response selected.` string. Failure feedback remains
available. Ordinary NPC speech now uses source bitmap font 229, white centered text with the source-style one-pixel
shadow, a transparent backing, source-width wrapping, and speaker-relative placement. Dialogue authority, response
buttons, input gating, and the single presenter/EventSystem ownership are unchanged.

### Corpse `LOOT` hover

No presentation change was made. The M8E production proof reconfirmed the same-identity corpse, exactly-once death
consequence, item transfer, reload persistence, Save V1 no-replay behavior, and zero warnings/errors. The hover label
itself remains **SOURCE AMBIGUITY** pending a working retail side-by-side capture; changing it from fan-memory or the
other implementation alone would violate the preservation rule.

## USER PHYSICAL REVIEW CORRECTIONS

| Review state | Visible defect | Finding and root cause | Correction / regression | Physical result | Status |
|---|---|---|---|---|---|
| Character Editor help | Concatenated title/body, one-line overflow, stray white line | Source splits on the first newline into exact title/body rectangles; Unity used one oversized generic text row | Separate bitmap title/body projection; exact rectangles; bounded wrapping; no caret; focused and physical assertions | 800x600 production Character Editor proof passed; modern mappings through 4K remain in-bounds | **SOURCE-CERTIFIED**, **OPENARCANUM-PHYSICALLY-VALIDATED** |
| HUD weapon messages | `WEAPON 6049` and `READY WEAPON 6049` exposed a prototype ID | Controller interpolated `Type` and `PrototypeNumber` instead of the prototype description | Shared coordinator display-name resolver plus production MES binding; regression rejects `8100`-style leakage | 800x600 Virgil/M12C production proofs show `UNARMED` / `READY UNARMED`; integrated harness passed | **SOURCE-CERTIFIED**, **OPENARCANUM-PHYSICALLY-VALIDATED** |
| Dialogue acknowledgement | `Response selected.` appeared after choosing a response | Local success feedback was projected into the gameplay message window | Successful selection clears feedback; regression asserts no synthetic acknowledgement | 800x600 Virgil proof retained dialogue responses without the string | **SOURCE-CERTIFIED**, **OPENARCANUM-PHYSICALLY-VALIDATED** |
| Virgil speech | Modern sans-serif text on a large translucent dark rectangle | Presenter used `LegacyRuntime.ttf` and a custom dark `Image`; source uses the object text-bubble renderer | Transparent 200px source bubble, font 229, centered white bitmap glyphs and shadow; focused geometry/font assertions | Same opening Virgil line inspected at 800x600 and after 1920x1080 rebuild; no dark panel, clipping, duplicate presenter, or EventSystem | **SOURCE-CERTIFIED**, **OPENARCANUM-PHYSICALLY-VALIDATED** |
| Corpse interaction | Dark `LOOT` hover was flagged for investigation | Corpse gameplay authority is green, but exact retail hover styling is not yet proven | No speculative production change; M8E physical proof rerun | Death/corpse/loot lifecycle passed with zero warnings/errors; hover styling remains pending retail comparison | **SOURCE AMBIGUITY** |

No item is labelled `RETAIL-PHYSICALLY-CERTIFIED` in this pass.

## Bounded message audit

The opening production path was exercised through new game, ready/unready projection, equipment transactions,
inventory use, dialogue response, turn-based and real-time combat, save/load rebuild, map travel, and
Original -> Enhanced -> Original presentation rebuild. No raw prototype number or synthetic response acknowledgement
appeared in the validated states.

Two pre-existing, non-opening crafting strings remain candidates for their own source-certified pass:
`Crafted prototype ...` and the schematics `Product ...` label. They were not changed because this pass did not obtain
the retail/source wording for that UI family.

## Validation

- Live Unity compile: clean.
- UI-D focused EditMode: 45 passed, 0 failed/skipped/inconclusive.
- M12C focused EditMode: 19 passed, 0 failed/skipped/inconclusive.
- UI-F.1 focused EditMode: 84 passed, 0 failed/skipped/inconclusive.
- UI-C focused EditMode: 70 passed, 0 failed/skipped/inconclusive.
- M13A directly affected regressions: 179 passed, 0 failed/skipped/inconclusive.
- UI-D production Play Mode: pass at 800x600; source help bounds and no-caret assertion included; resolution mapping
  checks cover 800x600, 1024x768, 1920x1080, 2560x1440, and 3840x2160.
- UI-F.1 production Play Mode: authentic Virgil 1324 pass; transparent source-font NPC bubble and response layout
  observed at 800x600 and after a 1920x1080 rebuild.
- M12C integrated production Play Mode: pass; warnings 0, errors 0.
- M8E death/corpse/loot production Play Mode: pass; warnings 0, errors 0.
- Complete EditMode suite: 1323 passed, 0 failed/skipped/inconclusive.
- Final Unity Console after clearing expected negative-test warnings: 0 errors, 0 warnings.
- `git diff --check`: clean.
