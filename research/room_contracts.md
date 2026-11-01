# The Missing Macaw - room contracts

Applies `structure_analysis_framework.md` to Malibu. Inputs: the full script
(`playtests/malibu_script_dump.md`), the structural readout, and playtest round 1
(`playtests/2026-11-01_summary.md`, tagged **[PT-H]** Haiku / **[PT-S]** Sonnet below).

Status: **design - nothing here is implemented yet.** Section 4 lists the decisions to sign
off before the text is rewritten.

---

## 1. The spine

### 1a. Case questions (stated by Reyes in the new intro)
| # | Question | Answer | Owned by |
|---|----------|--------|----------|
| Q1 | **Who** took him? | D. Marsh, the sound guy | entrance (seed) -> bedroom -> pool -> library (ID) |
| Q2 | **How** did he get out of a locked cage? | Basil forced the latch ~5:50, the bird let himself out | living room (latch) -> kitchen (Basil inside 5:45) -> garden (shears, confession) |
| Q3 | **Where** is he now? | in a foam-lined case behind the potting shed | garden (cases) + gathering reveal |
| QA | **When** did he really vanish? (the Big Lie) | before 6:00; the 8:00 squawk was a speaker | bedroom (plant) -> living room (crack + proof) |
| QB | **Whose evening doesn't add up?** (opportunity) | the tailed scrawl: in at 6:30, garden 6:47, wing 7:00, called for 7:00 | entrance -> bedroom -> pool -> library |
| QC | **Why?** | 47 ruined takes | library |

### 1b. Means / Motive / Opportunity ledger (target)

| Suspect | Means | Motive | Opportunity | The break (clears them) | Paid off in |
|---------|-------|--------|-------------|------------------------|-------------|
| **Marsh** | foam-lined cases (garden), speaker + wall cable (living room, library) | 47 ruined takes (library) | the scrawl: registry 6:30 (entrance), photo 6:47 (pool), manifest 7:00 (bedroom) vs call 7:00 (pool) | - | gathering 5-6 |
| Basil | shears fit the latch (garden) | "a cage is a prison" (garden) | herbs in at 5:45 (kitchen), glove under cage (living room) | the bird let HIMSELF out; Basil was behind the shed at 6:47 (straw hat) watching someone else | garden solve, gathering 4 |
| Rosa | keys to every room + a cart in the wing at 7 (kitchen) | "garbage slop" (kitchen) | 7:00 cart (kitchen, bedroom) | one cart; the second rumble smelled of cable grease | kitchen solve, gathering 3 |
| Derek | spare audio cable (bedroom) | "him or me" ultimatum (bedroom) | upstairs all evening | he was FOOLED by the squawk (he is the lie's victim) | bedroom solve, gathering 2 |
| Chip | - | PR disaster (entrance) | inside all evening | 15% commission: he needs the bird | gathering 1 |
| Wexler | crew access, carts | "BETTER WITHOUT HIM?" (library) | crew, in the house | **no break today** - see decision D5 | - |

Today Marsh's opportunity cell has a hole: the registry says the *crew column* starts at 6:30,
so the "half hour before his own call" arithmetic applies to the whole crew, and nothing ties
6:30 to Marsh **[PT-S]**. Section 2 fixes this with one thread.

### 1c. The red thread: the tailed scrawl (new)
One handwriting detail runs through three rooms and turns the opportunity cell into the
player's own deduction (a "same handwriting" match):

- **Entrance:** the crew column opens at 6:30 with ONE lone signature, *a scribble with a long
  looping tail*. The rest of the crew piles in together just before seven. (No name; it only
  says "someone came early".)
- **Bedroom:** the manifest's 7:00 "2x equipment cases" line is signed with *a scribble with a
  long looping tail*. (Same words; the match is the player's.)
- **Library:** every page of the session log is initialed with *a scribble with a long
  looping tail*, the log that ends "TAKE 47 - RUINED". (This ties the scrawl to the sound
  desk, and the badge says D. Marsh.)
- **Pool:** the call sheet shows the whole crew called for 7:00, Marsh one name among several.

Rule: the three sources use the identical phrase and never say "the same". This keeps the
pacing rule (no Marsh name before the library), and Marsh becomes the scrawl's owner only in
the library.

### 1d. Big Lie arc (already working, per [PT-S])
plant: intro cold open + bedroom (Derek) -> crack: Reyes's "unanimous at eight" (entrance solve)
-> proof: living room (untouched 6:00 supper + speaker) -> public demolition: gathering 2.

### 1e. Timeline lattice
| Time | Event | Source | Places |
|------|-------|--------|--------|
| 5:45 | herbs in from the garden - B. | kitchen roster | Basil inside, minutes before the latch |
| ~5:50 | latch forced, glove dropped | garden (Basil confession) | Basil |
| 6:00 | supper untouched | living room (Petra) | bird already gone |
| 6:30 | crew column opens: the tailed scrawl, alone | entrance registry | the scrawl inside early |
| 6:45 | cases through the garden (Coco, rounded) | pool | a crew member |
| 6:47 | photo: figure with cases + straw hat | pool | figure + Basil |
| 7:00 | Rosa's cart + 2x cases (scrawl) in the wing; crew call | kitchen / bedroom / pool | Rosa, the scrawl |
| 7:20 | grips' lamp returns | bedroom / library | grips (decoy) |
| 8:00 | the squawk | bedroom / living room | (fake) |
| 8:15 | lockdown | entrance | everyone sealed in |

---

## 2. Room contracts

Scores: today's state against R1-R8 (0/1/2) -> target. "Board" proposals are drafts; slot
answers are in `[brackets]` with their clue id.

### Entrance - "Whose evening doesn't add up?"
| Field | Contract |
|-------|----------|
| Case question | QB seed + "is the thief still inside?" |
| Key suspect | none named; the anonymous **scrawl** |
| Lie beat | none (Reyes's solve topic seeds the crack: "unanimous at eight") |
| Timeline input | 8:15 lockdown, **6:30 lone scrawl** |
| Final output | `time_630pm` + the scrawl (gathering 5) |
| Turn | "a bird went missing" -> "the thief is still in here, and someone came early" |
| Bridges | out: the scrawl -> bedroom manifest; "unanimous" -> living room |
| Confrontation | Reyes (solve): unanimous at eight (keep) |

Today: R1 2, R2 0 (board restates Reyes **[PT-S]**), R5 0 (6:30 never asked), R8 1. Haiku lost two
submits on slot 5 (Crew Arrivals vs 6:30) **[PT-H]**.

Changes:
- Registry text: the crew column opens at 6:30 with one lone *scribble with a long looping
  tail*, then nothing until the rest of the crew piles in together just before seven. New clue
  `early_scrawl` (Who, "Early Signature"). `guest_registry` stays as a WhereWhen decoy.
- Board (draft): "Nobody has left since the [lockdown] at [8:15], so whoever took Rudebeak is
  [still inside]. The crew's column opens at [6:30] with one lonely [scrawl] - a long time
  before anybody else on the crew - and [Reyes] says somebody's evening in this book
  [does not add up]." Answers: `lockdown_815pm`, `time_815pm`, `still_inside`, `time_630pm`,
  `early_scrawl`, `officer_reyes`, `registry_doesnt_add_up`. Chip leaves the board (gathering 1
  covers him); his chips stay as WHO/WHY decoys.

### Bedroom - "What came through the wing at seven?" + plant the lie
| Field | Contract |
|-------|----------|
| Case question | QA (plant) + QB (the scrawl's cases at 7:00) |
| Key suspect | Derek (motive, cable) |
| Lie beat | **plant** (squawk through the headphones) |
| Timeline input | 7:00 cart + 7:00 cases (scrawl), 7:20 lamps |
| Final output | `time_700pm`, `d_marsh_7pm` (gathering 3 + 5) |
| Turn | "the house was quiet" -> "heavy things rolled through, and the bird was alive at 8... right?" |
| Bridges | in: the scrawl; out: one cart? -> kitchen; the squawk -> living room |
| Confrontation | Derek (solve): the rumble came twice (keep) |

Today: R1 1 (two questions), R2 1, R5 2, R8 2 (ends on the lie - keep). Sonnet: slot 5 fit both 7:20
and Manifest Entry **[PT-S]**.

Changes:
- Manifest: the case line's signature becomes *a scribble with a long looping tail*;
  `scrawled_signature` keeps its id, with the same phrase in its name/context.
- Board: keep the shape and the false conclusion, but make every slot single-answer by its own
  wording. The cases slot reads as "the [manifest line] for two equipment cases, signed in the
  same minute" (7:20 is a different minute, so it is no longer a near-tie).

### Living room - "When did he really vanish?" (the midpoint)
| Field | Contract |
|-------|----------|
| Case question | QA crack + proof; Q2 opened (who forced the latch?) |
| Key suspect | Vivienne/Derek's certainty; the latch points to Basil (glove) and Rosa (cloth, ledger) |
| Lie beat | **crack + proof** |
| Timeline input | 6:00 supper untouched, 8:00 squawk |
| Final output | `missed_snack_6pm`, `time_600pm`, `hidden_speaker` (gathering 2) |
| Turn | "alive at 8" -> "gone before 6; the 8:00 squawk was a recording" |
| Bridges | out: glove (mulch) -> garden; cloth/ledger -> kitchen; wall cable -> library |
| Confrontation | Vivienne (solve): "film people wire everything" (keep) |

Today: R1 2, R2 1 (the speaker, the proof, is not on the board), R5 0, R8 1 ("Who? When? Why?"
is generic). Haiku: "gone before [six]" had four plausible chips **[PT-H]**.

Changes:
- Board (draft): "Everyone swears by the [squawk] at [8:00]. But Rudebeak's [supper] has sat
  untouched since [6:00], and the scream came from a [speaker] taped behind the couch. The
  cage's [wrenched latch] smells of mulch - somebody opened it hours before anyone heard a
  thing." Answers: `squawk_at_8pm`, `time_800pm`, `missed_snack_6pm`, `time_600pm`,
  `hidden_speaker`, `broken_latch`. Vivienne and Petra leave the board as name-recall slots.
- Decoys earn their place: `settled_feathers` (WhereWhen) and `documentary_camera` (What) stay.
  The camera's role is a negative clue (why there is no footage: the cage is out of frame); keep
  it, no rewrite.

### Kitchen - "The staff's clock: who moved through the house before and after six?"
| Field | Contract |
|-------|----------|
| Case question | Q2 support (Basil inside at 5:45) + QB contrast (only ONE cart at 7:00) |
| Key suspect | Rosa (keys, grudge, cart) |
| Lie beat | none |
| Timeline input | 5:45 herb run (B.), 7:00 cart (R.) |
| Final output | `time_700pm` + `dinner_cart_7pm` (gathering 3); Basil's 5:45 opportunity (gathering 4) |
| Turn | "Rosa had keys, a cart and a grudge" -> "...and the roster says exactly one cart went up" |
| Bridges | in: Derek's rumble, the ledger/cloth; out: B. at 5:45 -> garden; one cart vs two rumbles -> bedroom confrontation |
| Confrontation | Rosa (solve): cable grease (keep) |

Today: R1 0 (no stateable question), R2 0 (board restates the roster **[PT-S]**), R5 0 (feeds
nothing final), R8 1.

Changes:
- Board (draft): "The roster puts [B.] in this kitchen with herbs at [5:45], minutes before the
  bird's supper went untouched. At [7:00] exactly [one cart] went up to the bedroom wing:
  Rosa's, with Rosa's [keys] on her apron and Rosa's [grudge] pinned to the fridge."
  Answers: `herb_run_545`, `time_545pm`, `time_700pm`, `dinner_cart_7pm`, `rosa_keys`,
  `insulted_cooking`. It ends on suspicion (no exoneration), and both outputs are the ones the
  finale needs.
- The roster's 7:00 line explicitly reads "one cart". `kitchen_feathers` / `treat_jar_bribe`
  stay as decoys.

### Garden - "How did he get out, and where did he go?"
| Field | Contract |
|-------|----------|
| Case question | Q2 (latch: Basil) + Q3 (where: the cases) |
| Key suspect | Basil (all three cells - the strongest herring) |
| Lie beat | none |
| Timeline input | bird bath = alive outdoors after leaving the cage |
| Final output | `sound_proof_cases` (gathering 6), `shears_match_latch` near-miss |
| Turn | "the gardener did it" -> (solve) "the gardener broke the latch, then watched someone else take the bird" (the 75% low) |
| Bridges | in: glove, 5:45 herbs; out: cases -> library/pool; straw hat <- pool |
| Confrontation | Basil (solve): the latch confession (keep - testers' favourite beat) |

Today: R1 1, R2 1, R5 0 (cases not on the board), R6 2, R8 2.

Changes:
- **Clue name `sound_proof_cases` -> "Foam-Lined Cases"** (id kept). "Sound-proof" named the
  culprit's trade on the chip **[PT-S]**; Petra's gated topic still makes the "knew sound"
  inference out loud, as a slip, where it belongs.
- Cases text: one caster squeaks when nudged (pays off Coco's "little squeaky wheels").
- Board (draft): "Basil's [bent shears] fit the wrenched latch, and the [crisp feathers] in the
  bird bath say Rudebeak made it out here alive. Behind the shed, [heavy boot prints] stop at
  two [foam-lined cases]. And Basil swears he never left the garden all evening - which puts
  him right here when it happened." Answers: `shears_match_latch`, `bird_bath_feathers`,
  `boot_prints_shed`, `sound_proof_cases`. It ends ON Basil, so the solve confession lands as
  the low.

### Pool area - "Who was in the garden at 6:47, and where should he have been?"
| Field | Contract |
|-------|----------|
| Case question | QB (6:45/6:47 sighting vs 7:00 call) |
| Key suspect | "a crew member in black" (unnamed) |
| Lie beat | none |
| Timeline input | 6:45 Coco, 6:47 photo, 7:00 call |
| Final output | `time_647pm`, `straw_hat_witness` (gathering 4-5) |
| Turn | "someone moved cases" -> "a crew member was hauling them 13 minutes before the whole crew's call" |
| Bridges | in: cases, Basil; out: the straw hat -> Basil; the call list -> library |
| Confrontation | none (Coco's schedule topic, clue-gated) |

Today: R1 2, R2 2, R5 2, R8 2 - the strongest room, but it names Marsh **[PT-S]**.

Changes:
- **The call sheet becomes a crew list** under "7:00 PM - Crew call": grips (west terrace),
  camera (living room), lights (hall), sound - D. Marsh (library). One name among four
  (Christie's "hide it in a list"). `production_schedule` keeps its id; the chip name becomes
  "Crew Call Sheet".
- Coco's schedule topic: "that man" -> she can't tell the crew apart ("they all wear black,
  darling"). She confirms a crew member, not Marsh.
- Bright feather: re-aim from "could be anywhere" to *snagged in the hedge at wheel height,
  beside two thin ruts in the lawn* (bird + wheels together; means).
- Board: keep. Make the call-sheet slot read "the whole crew was called for [7:00]".

### Library - "Which crew member? The identification room"
| Field | Contract |
|-------|----------|
| Case question | Q1 (ID) + QC (why) + QA proof source (the wall cable) |
| Key suspect | Marsh vs Wexler (two crew suspects, not a victory lap) |
| Lie beat | proof source (the cable into the wall) |
| Timeline input | (Marsh's own claim "here at seven") |
| Final output | `d_marsh_sound_guy`, `ruined_takes` (gathering 6) |
| Turn | "a crew member did it" -> "the scrawl is Marsh's, and he had a reason" |
| Bridges | in: the scrawl (entrance, bedroom), the call list (pool), the speaker (living room) |
| Confrontation | Marsh (solve): 47 takes (keep) |

Today: R1 1, R2 2, R5 2, R8 2 - but a victory lap **[PT-S]**, because rooms 4-6 already named him.

Changes:
- Session log: every page initialed with *a scribble with a long looping tail*. New clue
  `log_initials` (Who, "Initials on the Log").
- Board (draft): "The [initials on the session log] match nobody's handwriting but one crew
  member's: [the Sound Guy]. His [monitoring gear] is plugged into nothing, and the log is one
  long grudge: [the 47 takes]. Wexler wanted the bird off his shot list - but somebody wanted
  him gone for good." Answers: `log_initials`, `d_marsh_sound_guy`, `marsh_headphones`,
  `ruined_takes`. Wexler's chips remain as WHO/WHY decoys.
- Sonnet: the HOW bank shows only Audio Cable until Marsh's topic **[PT-S]** - acceptable, since
  his headphones topic is ungated. Verify with the audit's option count.

---

## 3. Finale follow-ups (phase 3, after the rooms)
- Gathering 5 (timeline): drop "a full half hour before his own call" **[PT-S]**. New draft:
  "The [scrawl] opens the crew column at [WHEN]; the crew wasn't called until [WHEN]. By [WHEN]
  he was on Coco's photo with the cases..." The slots ask for the comparison; the sentence
  states no conclusion.
- Every gathering slot gets at least one near-miss decoy (the audit's option count, plus a
  manual "is it wrong for a stateable reason?" pass).
- Petra's "Dark and padded, I said" depends on a clue-gated topic that may be unheard. Make it
  a standalone line.
- Roll call: one neutral crew line ("...and a sound man who has not stopped coiling the same
  cable") so the culprit is met **[PT-H]**.
- The garden's "Coco's Sighting" source: `eyewitness_645pm` is also unlocked from the garden,
  and Coco's intro bracket "[Coco]" maps to it, so the chip appears before her sighting
  **[PT-S]**. Re-map: her name bracket should map to a WHO chip.

## 4. Decisions to sign off
1. **D1 - The tailed scrawl** as the case's red thread (entrance -> bedroom -> library).
   Adds `early_scrawl` + `log_initials`.
2. **D2 - Rename `sound_proof_cases` to "Foam-Lined Cases"** (the chip shouldn't name the trade).
3. **D3 - The call sheet as a 4-name crew list**, and Coco no longer singles out "that man".
4. **D4 - Board rewrites** for the entrance, living room, kitchen, garden and library (drafts
   above; bedroom and pool get wording fixes only). Chip leaves the entrance board.
5. **D5 - Wexler:** leave him a light decoy (recommended; the library board names his motive
   and moves past it), or give him a full herring arc with a break and a gathering step
   (more content, longer finale).
