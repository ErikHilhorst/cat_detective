# Playtest 2026-11-01 - Haiku 5.5 (text simulator, first-time player)

Played through `tools/story_sim/sim.py` only. Reached the ending ("CASE CLOSED - The Missing Macaw"). Command count is approximate (about 115 sim calls including help and the two miscounted submits).

## 1. Opening

- **Did it make me care?** Yes, mostly. The 8:15 PM / "EMPTY!" cage beat and Reyes's "somebody in there is lying to me" give a clear hook. The news-banner roll call is fun and memorable.
- **Stakes in my own words:** Find who took Rudebeak, how he got out of a locked cage, and where he is now, before the gates open at sunrise.
- **Roll call I remembered:** Vivienne (grief-as-couture, "I heard him at eight"), Derek (headphones, "the bird woke me at 5 AM"), Chip (fifteen percent, "rude SELLS"), Reyes, Rosa (house keys, "maybe they are not wrong to look"), Basil (gentle, opinionated about cages), Wexler (crew), Petra, Coco.
- **Gap:** The sound guy is not in the roll call at all. He is only "crew: lights, grips, sound". Nobody names him until I found his badge in the library, so the most important suspect is unnamed in the intro.
- Intro line I quoted to myself: "Find out who took him, how he got out of a locked cage, and where he is now. Before sunrise." Clear.

## 2. Room by room

**Entrance** - *Question as I read it:* Was the bird ever here at all, and whose timeline is off? Reyes's "Still Inside" and "Lockdown" told me the bird had to still be on the property.
- Suspects after this room: Chip (PR panic, commission) and Reyes's registry hint "somebody's evening in this book does not add up" pointed at nobody specific.
- Board difficulty: **3/5**. 2 wrong submits (see Stats).
- Confusing: The board sentence "right down to the [5]" accepted only "Crew Arrivals". I tried "6:30 PM" first (wrong), and both "6:30 PM" and "Crew Arrivals" read well. I could not tell which was the intended answer until it accepted.
- Also: "sealed since the [1] hit at [2]" was ambiguous. Lockdown + 8:15 PM was right, but "North Gate Locked" was tried as slot 1 and was worse (7/9).
- Dead clue: none.

**Bedroom** - *Question:* Is Derek's "bird was alive at eight" testimony reliable?
- Derek's headphones, spare audio cable, ultimatum note, and "Squawk Through Headphones" all felt like a real lead. Derek became my second suspect.
- Board difficulty: **2/5**. 0 wrong submits.
- Confusing: The Service Tray (a HOW/WHAT chip) sits in a sentence about a *time* ("lines up with the [3] due at [4]"). I got it right, but the chip is an item not a fact about time. The paperwork slot (Manifest Entry vs Lamp Returns) was a coin flip that I got right.
- Dead-ish clue: "Spent Earplugs" felt like flavour, though Derek's line about "three hundred dollars" is funny.

**Living Room** - *Question:* How did the cage get opened, and who heard the bird last?
- Vivienne's testimony and the Vocab Ledger pointing at Rosa made Rosa a mid-level suspect. Basil's dropped glove and the bent shears (not yet found) were also in play.
- Board difficulty: **3/5**. 0 wrong submits, but I was guessing on slot 5-6.
- Confusing: "Rudebeak was already gone before [6]". The sentence says "gone", but the chip set offers 6:00, 6:30, 7:00, Missed Snack, Settled Feathers. I chose Missed Snack + 6:00 PM from Petra's feeding story. Accepted, but the logic is thin: "Settled Feathers" reads just as well as an answer and I could not justify choosing one over the other.
- Note: Vivienne is still listed as a WHO answer slot here, which made her look like a near-culprit when she was just a witness.

**Library** - *Question:* What was the sound guy doing, and why does he have no audio cable plugged in?
- This was the first real suspicion of the culprit. "Unplugged Headphones" (headphones ending a foot short of the board) plus "Ruined Takes" (47 takes ruined by the bird) made D. Marsh the strongest suspect. The 7:00 library alibi was a nice lie to catch.
- Board difficulty: **1/5**. 0 wrong submits.
- Confusing: After solving, the chip is named "Sound Guy: D. Marsh" in the notebook but "The Sound Guy" in the room list. I had to reread both to be sure they were the same person.
- Good: Wexler's "Marsh is my rock... hasn't complained about that bird in two weeks" is a sharp line that I only appreciated in hindsight.

**Kitchen** - *Question:* Who else was moving carts and keys in the east wing?
- Rosa's house keys, the duty roster ("R. - dinner cart to the east wing" at 7:00) and her "Insulted Cooking" motive made her a strong suspect again, but "Feathers in the Bin" (chicken feathers she says) felt like a red herring. My suspicion of Rosa dropped after she explained "I am a cook, officer".
- Board difficulty: **2/5**. 0 wrong submits (the 5-slot board went through on the first valid submit).
- Confusing: I accidentally ran `go Garden` before solving the kitchen, then tried to solve the kitchen board and got "This board has 4 slots - give exactly 4 chips" (the garden board). That error does not name the room, and it looked like the kitchen board had changed. This was my mistake, not a game bug, but the error text should say which board it checked.

**Garden** - *Question:* Where was Basil, and what was he doing with the shears?
- Basil's bent shears, muddy glove and "the cages are prisons" rant made him my suspect for the cage. The "boot prints" and hidden cases were a nice find.
- Board difficulty: **2/5**. 0 wrong submits.
- Dead-ish clue: "Cages Are Prisons" (WHY) is a good line but the board never uses it as an answer.

**Pool Area** - *Question:* Where was the crew between 6:45 and 7:00?
- Coco's phone selfie at 6:47 with the straw hat at the frame edge, plus the crumpled schedule ("D. Marsh - Library (audio setup)" at 7:00 PM) made the timeline click. This is the room where the timeline became a story.
- Board difficulty: **2/5**. 0 wrong submits.
- Confusing: The sentence "Coco ... watched [1] through the garden at [2]" has a WHERE/WHEN chip in slot 1 that is an odd object for "watched". I used "Coco's Sighting" and it accepted.

## 3. Suspect-theory timeline

- After **Entrance**: Chip Sterling (PR and commission). Reyes was clearly not the culprit (he is the policeman). Rosa and Derek weren't yet in view.
- After **Bedroom**: Derek. The "bird alive at 8" alibi and the spare audio cable made him feel like a planted red herring rather than the culprit.
- After **Living Room**: Basil (glove, shears) rose fast. Vivienne was a witness. Rosa picked up Vocab Ledger "MAID'S FAULT" as a motive.
- After **Library**: **first suspicion of the real culprit, D. Marsh.** What tipped me: the unplugged headphones ("plugged into nothing at all"), the audio cable into the wall, "Ruined Takes - TAKE 47 RUINED. BIRD. AGAIN.", and the 7:00 "library audio setup" alibi.
- After **Garden**: Basil confessed the latch was his only as far as the board said; I still had Marsh as favourite.
- After **Pool Area**: Marsh confirmed by the crumpled schedule and the 6:47 selfie figure rolling cases. Marsh was never really challenged until the finale, which was fine.

## 4. Timeline

- I understood the broad sequence: bird out of cage before 6:45, cases through the garden at 6:47, cases into the bedroom wing at 7:00, fake squawk at 8:00.
- **Confusing pairing 1:** 6:00 PM vs 6:30 PM vs Settled Feathers as "when the bird was gone." I had to guess in the Living Room board.
- **Confusing pairing 2:** the final "He was inside by [WHEN] - a full half hour before his own call." The intended answer is 6:30, but nothing in the game states "his call is 7:00" in the same place. I only had the crumpled schedule (pool area) and reasoned 7:00 - 30 min. It felt like a guess that happened to be right.
- **Confusing pairing 3:** 7:00 PM vs 7:20 PM (cases vs lamp returns). The manifest's readable names helped; the scrawled signature was the only one nobody could read, which is a good clue.
- The Sound-Proof Cases as a HOW/WHAT chip for "hid Rudebeak inside" was clear once you had the cases in the garden.

## 5. Finale / gathering

- **Did it feel dramatic and earned?** Yes overall. The "Headline Man", "Squawk at Eight", "Dinner Cart", "Broken Latch", "Long Evening" and "Sound Guy" boards each advanced the case and each confrontation paid off something I had found. The final reveal (cases opened, Rudebeak in the foam, "FORTY-SEVEN TAKES!") is a satisfying payoff.
- **Felt like guessing:**
  - "The Long Evening" WHEN-1 (6:30) - correct, but the arithmetic is not on any one clue.
  - "The Squawk at Eight" slot 2 (6:00 PM) - I guessed from Petra's feeding story; the chip "Missed Snack" alone was not a time.
  - "Broken Latch" slot 2 ("Straw Hat in Frame") - it read as the best fit for "the WHO at the edge of Coco's photo" but the clue's "Straw Hat" is a WHO-category object, and I had the photo time as the other option.
- **Wrong submits in the finale:** 0. Every gathering board accepted on the first submit.
- Confrontation gating: after solving all seven boards the game said "FINAL SOLVE locked: still owing an explanation - Basil, Derek, Reyes, Rosa, The Sound Guy, Vivienne Vale". Useful, but I had to walk back through all six rooms (~15 extra commands). The "Lay your solved case notes" option for Rosa and "Place one paw on his boot" for Reyes were only visible after the solve. I did not know those options existed until I revisited.

## 6. Payoff

- **Who took him?** D. Marsh, the sound guy. Answered. The cases confirmed it.
- **How did he get out of a locked cage?** Basil broke the latch with the shears just before 6, then Rudebeak walked out through the garden. Answered, but Rudebeak "let himself out" is only stated in Basil's confession, not shown.
- **Where is he now?** In a sound-proof case behind the potting shed. Answered.
- **Left unexplained:** Who was the "Straw Hat"? Basil (the hat was his, I think - never stated outright). Also: Derek's "Viv - he's coming to the guest house" note and the spare audio cable are only half-resolved (Derek says he wanted the bird moved, not stolen). The ending says "Basil installed a brand-new latch... left it unlocked on Sundays" - fun, but it does not say what Marsh gets.

## 7. Top 5 problems (ranked)

1. **The finale WHEN-1 depends on arithmetic no single clue states.** "a full half hour before his own call" needs the 7:00 call time from the crumpled schedule, which is only visible in the pool area (visited last). Fix: put the call time into the finale sentence or into the Library/Entrance clue ("his call is at 7:00 PM"), so the subtraction is a player step, not a hidden step.
2. **Entrance slot 5 has two valid-looking answers ("6:30 PM" and "Crew Arrivals").** I burned a wrong submit on the 6:30 and only got the right one by trying again. Fix: make the sentence say "the crew column begins at [5]" or give "6:30 PM" its own word-bank exclusion.
3. **Living Room "already gone before [6]" has four plausible answers.** Settled Feathers, Missed Snack, 6:00 and 6:30 all fit. Fix: change the sentence to name the event ("the bird was out of the cage before [6]") and make Settled Feathers a decoy.
4. **Confrontation options appear silently after the solve.** I only found Rosa's "Lay your solved case notes at her feet" and Reyes's "Place one paw on his boot" because I walked back. Fix: a `[!]` or "new option" marker on each person after the room solves, or a line in `notes` listing "confront Rosa, Reyes, ..." (the FINAL line helps but does not say where they are).
5. **Board errors do not name the room.** "This board has 4 slots - give exactly 4 chips" told me nothing about which room. Fix: "Garden board has 4 slots - give exactly 4 chips" (or the room you are in).

Minor: the sound guy is absent from the roll call, so the culprit is unnamed in the opening. Consider adding "a sound guy with a clipboard" to the news banner.

## 8. Stats

- Total sim commands: about 115 (approximate; includes `new`, `help`, `notes`, `board`, `notebook`, `look`).
- Wrong submits per board:
  - Entrance: **2** (first try "8/9 fit" with Lockdown, Rudebeak, Still Inside, 6:30 PM, Reyes, Does Not Add Up, Chip, PR Disaster; second try "7/9 fit" with North Gate Locked in slot 1; third try correct with "Crew Arrivals" in slot 5).
  - Bedroom: 0 (solved first try).
  - Living Room: 0 (solved first try).
  - Library: 0.
  - Kitchen: 0 (two earlier attempts were rejected for slot count, not logic: my own sequencing mistake).
  - Garden: 0.
  - Pool Area: 0.
  - Finale gatherings (7 boards): 0 wrong submits.
- Final solve unlocked after all six confrontations. The FINAL solve locked message listed all six owing suspects.
- Ending reached: yes.
