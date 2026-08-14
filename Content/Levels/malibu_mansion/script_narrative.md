# The Missing Macaw - Narrative Script

A prose rendering of the case as it currently exists in `case_config.json` and the seven
`room_config.json` files. Each room is a chapter. Clue names are **bold**; their ids, categories
and macro status are listed in the table at the end of each chapter. Timestamps are called out
inline because the timeline IS the mystery.

UPDATED after the timeline rework (R1-R5, see `script_timeline_analysis.md`): confrontation
slips now unlock their own clues, bundled clues were atomized, every solve sentence was
de-keyed, and the final board is a 7-slot chronology.

Legend for the tables: (M) = macro clue (appears on the final board), (L) = local-only decoy,
(C) = confrontation clue (unlocks from a gated topic).

---

## Prologue

Malibu. Evening. Rudebeak the macaw - documentary star, professional heckler, owner of a
vocabulary nobody will repeat on camera - has vanished from his cage in Vivienne Vale's
living room. At 8:00 PM the whole house heard him squawk. At 8:15 PM Officer Reyes sealed
every door. Nobody has left since.

Which means the bird - and whoever took him - is still on the property. So is Dikkie.
Dikkie cannot talk. Dikkie does not need to. People explain themselves to cats.

---

## Chapter 1 - The Entrance: Sealed Doors

The front hall is a crime scene's waiting room. **Officer Reyes** guards the door with the weary
dignity of a man who trained for hostage negotiations and got missing poultry instead. Sit at
attention beside him and he confirms the frame of the whole case: **Lockdown - 8:15 PM**, full
perimeter, his seal on every door. Meow at the sealed front door and he adds the corollary via
logic a cat can respect: the **north gate was locked all evening** - if the bird left the
property, it left before lockdown, or it never left at all.

**Chip Sterling**, the manager, paces a groove into the rug with a phone welded to his ear
("My client LOVES animals"). Rub against his very expensive trousers and he gives up his
motive-shaped panic: this is a **PR Disaster**, front-page news, three interviews to cancel.
Swat the phone out of his hand and he accidentally exonerates himself: he takes **fifteen
percent** of everything the bird earns, documentary fee included. A man does not kidnap his own
commission. Probably.

Two objects round out the room. The **Officer's Radio** crackles dispatch confirmations of both
the 8:15 seal and the locked north gate. The **Guest Registry** is the room's real cargo: the
**whole household** parades through its pages - Wexler at 4:15, Petra plus one macaw at 5:05,
Coco at 5:50, staff since morning - and then a **crew column that begins at 6:30 PM**, the
signatures hurried, overlapping, hard to count.

The confrontation beat (solve-gated - Reyes is now part of the confrontation lock): solve the
entrance board, then nudge the registry toward him. He taps the page: one signature in the crew
column landed **half an hour before** its own call sheet - signed sloppy, blamed traffic. This
unlocks **Early Sign-In**, a macro clue and the final board's WHEN-1 answer. He never says
whose; the half-hour arithmetic (6:30 sign-in vs the 7:00 call on the pool area's schedule) is
the player's to do.

**Local solve:** "Whoever took Rudebeak never left: the [__] made sure of that. The suspect
pool arrived in the [__] column - and Chip will pay anything to keep it quiet, because all he
smells is a [__]." (Answers: Lockdown / Crew Arrivals / PR Disaster. North Gate Locked is a
live decoy for slot 1 - it also "sealed" something, just not everything.)

| Clue | Id | Category | Time |
|---|---|---|---|
| Lockdown - 8:15 PM (M) | `lockdown_815pm` | WhereWhen | 8:15 PM |
| PR Disaster (M) | `pr_disaster` | Why | - |
| Crew Arrivals - 6:30 PM (M) | `guest_registry` | WhereWhen | 6:30 PM |
| Early Sign-In (M)(C) | `early_signin` | WhereWhen | relative: 30 min early |
| North Gate Locked (L) | `north_gate_locked` | WhereWhen | all evening |
| Chip's Commission (L) | `managers_commission` | Why | - |
| Household Sign-In (L) | `household_roster` | Who | - |

---

## Chapter 2 - The Living Room: The Scene of the Crime

The heart of the case. **Vivienne Vale** is draped across the chaise in couture worn AS grief.
Purr against her arm and she gives the case its false anchor: she heard Rudebeak at
**8:00 PM, on the dot**, mid-affirmation - "I would know that screech anywhere." Paw at the
empty cage and she deflects toward the staff: the bird's **vocabulary problem** is Rosa's
doing, and Vivienne keeps a list.

**Petra**, the bird handler, stands perfectly still beside an untouched bowl of seed. Sniff it
and the timeline gets its true starting gun: Rudebeak's **six o'clock snack, never once
missed**, sits untouched - by 6:00 PM he was already somewhere he could not smell it. Two hours
before Vivienne "heard" him. Tilt your head at her and she profiles the victim: vain,
loud, and religious about his **evening splash in the garden bird bath**. This is the line that
sends the player's suspicions outdoors.

The physical evidence is dense here, and deliberately points at three different people:

- The **Empty Cage**: latch **snapped clean off**, forced with something flat and strong. The
  feathers beneath have settled - this happened a while ago.
- The **Hidden Speaker**: at whisker height, in the shadow under the couch, a tiny speaker
  taped to the frame; its thin cable slips away behind the wainscoting. (The couch is a
  walk-behind prop - the speaker is literally hidden until the furniture fades.) This is the
  HOW, found in room two, long before the player can know what it means.
- The **Dropped Glove**: one canvas gardening glove under the cage stand, smelling of mulch,
  no business on parquet. (Points at Basil.)
- The **Lemon Polish Cloth**: draped over the cage rail; somebody dusts this cage daily, close
  enough to reach right in. (Points at Rosa.)
- The **Vocab Ledger**: "Garbage slop" furiously crossed out, "MAID'S FAULT" beside it.
  (Vivienne pointing at Rosa; also seeds Rosa's motive before the player meets her.)
- The **Documentary Camera**: recording all day - aimed at the chaise. Four hundred hours of
  Vivienne's good side, not one frame of the cage. (Kills the easy "check the tape" solution
  and quietly establishes the crew wires this house.)

Cross-room payoffs: report the **garden feathers** to Petra and she decodes them - wet feathers
mean a happy bird, and a happy soggy bird does not fly away; he only allows himself to be
picked up when he is soggy and smug. Describe the **big black cases** and she goes very still:
put him somewhere dark and padded and he goes quiet as a stone - whoever picked those cases
knew birds, or knew sound.

Solve-gated beat: bat the speaker out where Vivienne can see it. The movie star drops for one
beat - "That is not mine. That is not RUDEBEAK'S" - then widens the suspect pool the way only
she can: the **film people** wire everything, the staff hold keys to everything, even Derek has
been buying gadgets. There are cables in her walls she has never been introduced to.

**Local solve** (now the room's real thesis - gone by six vs heard at eight): "By the time of
the [__] the cage already stood empty - no matter what the whole house swears it heard at the
[__]. The [__] let the bird leave. The [__] kept him 'home'." (Answers: Missed Snack 6:00 /
8:00 Squawk / Broken Latch / Hidden Speaker. Settled Feathers decoys the first slot, the
Documentary Camera decoys the What slots.)

| Clue | Id | Category | Time |
|---|---|---|---|
| 8:00 PM Squawk (M) | `squawk_at_8pm` | WhereWhen | 8:00 PM |
| Broken Latch (M) | `broken_latch` | What | ~6:00 PM (implied) |
| Hidden Speaker (M) | `hidden_speaker` | What | - |
| Dropped Glove (L) | `glove_by_cage` | Who | - |
| Lemon Polish Cloth (L) | `polish_cloth` | Who | - |
| Documentary Camera (L) | `documentary_camera` | What | all day |
| Vocab Ledger (L) | `vocab_ledger` | Why | - |
| Missed Snack - 6:00 PM (L) | `missed_snack_6pm` | WhereWhen | 6:00 PM |
| Settled Feathers (L) | `settled_feathers` | WhereWhen | hours old |

---

## Chapter 3 - The Bedroom: Wheels in the Night

The east wing. **Derek**, the husband, radiates the deep peace of a man who has finally solved
his one problem - brand-new noise-canceling headphones clamped over his ears. Paw at his
trailing cable and he surfaces long enough to log the room's key timestamp: something heavy
**rattled down the hall around seven**. Wheels. Dinner cart, probably. He didn't look. Wasn't
his problem.

Stare at the headphones until he explains and Derek self-incriminates in the most Derek way
possible: the bird screamed at five every morning, he left Viv a note - **do something** about
that bird or I will - she did not, so he did something himself. Legal. Non-violent. Three
hundred dollars.

The paper trail in this room is the case's best decoy engine:

- **Derek's Note** on the nightstand, in his own hand: "Do SOMETHING about that bird or I
  will. I am past asking nicely." (Motive, in writing - the WHY decoy.)
- The **Headphone Box**: receipt dated yesterday, a spare **audio cable** still coiled in the
  foam. (A second audio cable in the house - the HOW/WHAT near-miss for the speaker slot.)
- The **Service Tray**: soup bowls scraped clean. The dinner cart really did come through on
  schedule. (Confirms half of Derek's "it was the cart" assumption - the true half.)
- The **Delivery Manifest**, the room's centerpiece: florist out 6:10, catering ice run 6:55,
  **R. - dinner service 7:00**, "**2x equipment cases**" under a **hurried scrawl** at
  **7:00 PM**, linen return 7:10, **grip dept. lamp returns 7:20**. Half the house rolled
  through this wing inside one hour - and every entry carries a readable name except one.
  The **Scrawled Signature** is its own clue now: the identity question, held open.

The trick of the chapter: THREE things on wheels share the seven o'clock hour (cart at 7:00,
cases at 7:00, lamps at 7:20), and Derek's own memory is deliberately vague ("around seven -
maybe later"), so his rumble alone cannot be pinned to any of them. Disambiguating the wheels
takes Rosa's cable-grease slip from the kitchen.

Solve-gated beat: knead the duvet beside him and Derek, prompted by whiskers, remembers the
detail that breaks the wing wide open: the hall rattled **twice** that night. Once with the
soup smell. Once without. "Huh." This is now its own clue - **Second Rumble - No Soup** -
macro, and a near-miss decoy on the final board's WHEN-3 slot.

**Local solve:** "The [__] passed Derek's door and he never looked up. The paperwork knows why
he should have: it logs the [__] into the wing, backed by nothing but a [__]." (Answers:
Hallway Rumble / Manifest Entry / Scrawled Signature. The Lamp Returns decoy the middle slot -
they are also logged into the wing, but THEIR entry has a department name on it.)

| Clue | Id | Category | Time |
|---|---|---|---|
| Manifest Entry - 7:00 PM (M) | `d_marsh_7pm` | WhereWhen | 7:00 PM |
| Second Rumble - No Soup (M)(C) | `hall_rumble_no_soup` | WhereWhen | time unknown - the point |
| Derek the Husband (M) | `derek_the_husband` | Who | - |
| Derek's Ultimatum (M) | `star_note_to_husband` | Why | - |
| Derek's Headphones (M) | `headphone_box` | What | bought yesterday |
| Scrawled Signature (L) | `scrawled_signature` | Who | - |
| Hallway Rumble - 7 PM (L) | `husband_hall_7pm` | WhereWhen | ~7:00 PM, vague |
| Service Tray (L) | `service_tray` | What | - |
| Lamp Returns - 7:20 PM (L) | `lamp_returns_720` | WhereWhen | 7:20 PM |

---

## Chapter 4 - The Library: The Man Who Stopped Complaining

The crew's corner of the house, and the room where WHO finally firms up.

**Wexler**, the director, watches the room like it owes him a third act. Walk through his shot
and he confesses to the wrong crime with total enthusiasm: missing bird, weeping star, locked
mansion - **Emmy bait**, all of it. You cannot BUY a crisis like this. Sit on his notebook and
he hands over the case's quietest, sharpest clue while alibi-ing his crew: grips asleep, camera
department unionized, and **Marsh** - "Marsh is my rock. Used to file a complaint about that
bird every single day. Hasn't said a word in two weeks. Personal growth, probably."

His **Shot List** deepens his red-herring shine: "Rudebeak reaction shot - CANCELLED," and in
red pen beside it, "**BETTER WITHOUT HIM?**" Confronted about the pen (gated on finding the
list), Wexler is delighted rather than defensive: note the question mark, cat - editorial
instinct, not a confession. Though if it WERE a confession, the ratings would be extraordinary.
(His crew alibi also gives the lamps a voice now: the grips "**hauled the lamps back** after
dinner and died - heroes, all of them" - so the 7:20 wheels are heard of before the manifest.)

And then there is **The Sound Guy**. His badge reads **D. Marsh** - the dialogue label only
switches from "The Sound Guy" to his name once the badge clue is found (and cross-room toasts
deliberately keep calling him The Sound Guy so the name never leaks early). He is very busy
re-coiling a cable that was already coiled. "Nice cat," he says, half an octave too high.

His interrogation is a ladder of slips:

1. Stare, do not blink - he volunteers his alibi unprompted: "I was HERE at seven. Library.
   Audio setup. It's **on the schedule**." (Nobody asked. He is the only character who cites
   paperwork in his own defense - and the schedule itself is lying crumpled by the pool.)
2. Paw at his headphones - the monitoring cable ends a foot short of the board, plugged into
   **nothing at all**. "Wireless monitoring," he explains, to a cat, with total conviction.
3. (Gated on the cable) Follow the wall cable with your eyes - "That's **grounding**. Cables
   need grounding. Into walls." He stops himself explaining audio engineering to a cat and
   very carefully touches nothing.
4. (Gated on finding the cases) Sit at his feet smelling of tarp and potting shed - he goes
   pale: "They're empty! The cases are **EMPTY**, both of them, why would I even-" He hears
   himself say it. The library gets very, very quiet. The slip is now its own macro clue -
   **'The Cases Are EMPTY'** - evidence of knowledge nobody gave him. (Cross-room gated, the
   one deliberate exception: the library counter needs the garden trip to reach N/N.)
5. (Gated on solving the library) Sit on the mixing board and stare into his soul. Nobody has
   ever waited out a cat. The near-confession: **forty-seven takes**. "I hear the squawk when
   I close my eyes." Then, catching himself: nobody HURT anybody - wherever that bird is, he
   is sure he is comfortable. His eyes drift to the window, out toward the garden, and leave
   it slowly, like the view is an ex.

The objects corroborate: the **Mixing Board** hums after wrap, its **Audio Cable** vanishing
into the wall heading west (toward the living room - the game never says so; the player draws
the line). The **Session Log** behind the podium: "TAKE 12 - bird. TAKE 23 - bird. TAKE 47 -
RUINED. BIRD. AGAIN." - the handwriting angrier with every page, the last page torn through.

**Local solve:** "One crew member has an alibi nobody asked for: [__]. Behind his mixer, the
[__] leaves the room inside the wall - and the log by his chair is one long grudge, page after
page: the [__]." (Answers: D. Marsh / Audio Cable / Ruined Takes.)

| Clue | Id | Category | Time |
|---|---|---|---|
| Sound Guy: D. Marsh (M) | `d_marsh_sound_guy` | Who | - |
| Audio Cable (M) | `audio_cable` | What | - |
| Ruined Takes (M) | `ruined_takes` | Why | - |
| 'The Cases Are EMPTY' (M)(C) | `knew_cases_empty` | Who | - |
| Wexler's Ratings (L) | `director_notes` | Why | - |
| Shot List (L) | `directors_shot_list` | Why | - |
| Wexler the Director (L) | `the_director` | Who | - |
| Unplugged Headphones (L) | `marsh_headphones` | What | - |

---

## Chapter 5 - The Kitchen: The Woman with the Skull Doodle

**Rosa** is chopping vegetables with feeling; the cleaver hits the board like a verdict, and a
ring of **house keys** - every door in the mansion - jingles at her apron with every chop.
Meow the word "bird", approximately, and she does not even pretend: the bird called her paella
**garbage slop**, in front of guests, eleven years she has run this kitchen, and yes, she
dreamed of roasting him with a nice sofrito. "Now he is gone, and everyone looks at Rosa.
Maybe they are not wrong to look." (Per the clue conventions, the script never exonerates her -
she declines to exonerate herself.)

The room is built to make her look maximally guilty while proving nothing:

- The **Note on the Fridge**: the insult, written down, with a **skull** drawn next to it.
  In pen.
- The **Duty Roster** by the door: **5:45 PM - herbs in from the garden - B.** (placing Basil
  indoors just before the latch broke), 6:30 plating, **7:00 PM - dinner cart, east wing - R.**
  The east wing is where the bedrooms are.
- The **Poultry Crate**: tonight's roast, plucked in-house, a bin of **feathers** beside it.
  Dikkie freezes. Looks closer. Small ones. Plain ones. Probably the roast's. One eye stays on
  that bin.
- The **Treat Jar**, labeled "FOR THE CAT": Rosa keeps treats. For Dikkie. Specifically.
  If this is a bribe, it is working. (Her real characterization beat - the softness under the
  cleaver.)

Gated beat (on Derek's hallway rumble): give her a slow, skeptical blink and she confirms the
7:00 cart run herself - soup, roast, the little breads, and Mister Derek did not even lift his
headphones to say thank you. "Ask HIM who else was rolling things around up there."

Solve-gated beat: lay the solved case notes at her feet. She reads the board the way she reads
a delivery invoice - fast, twice, looking for the cheat - then sets down the cleaver and
crouches to cat eye level, which nobody does. "You did the arithmetic and it comes out Rosa.
Then do the rest of it, detective." Her cart is heavy going out and light coming back - and
that night the wing smelled of soup and **cable grease**. Soup was hers. Kitchens know their
smells. Find whose the other one was. The slip is now its own macro clue - **Smell of Cable
Grease** - the fact that pins Derek's soupless rumble to sound equipment instead of the grips'
lamps. (The reward for accusing the red herring: she hands the player the thread back to the
cases.)

**Local solve:** "The house has its favorite suspect: [__], with a key to every door. The
fridge gives her fury a name - the [__] - and the duty roster wheeled her straight into the
bedroom wing: the [__]." (Answers: Rosa / Insulted Cooking / Dinner Cart. Rosa's Paella is a
live motive decoy; the Ring of House Keys tempts the suspect slot; the Herb Run decoys the
roster slot but runs the wrong direction.)

| Clue | Id | Category | Time |
|---|---|---|---|
| Rosa the Maid (M) | `the_maid` | Who | - |
| Insulted Cooking (M) | `insulted_cooking` | Why | - |
| Smell of Cable Grease (M)(C) | `cable_grease` | What | - |
| Dinner Cart - 7:00 PM (L) | `dinner_cart_7pm` | WhereWhen | 7:00 PM |
| Rosa's Paella (L) | `paella_pot` | Why | - |
| Feathers in the Bin (L) | `kitchen_feathers` | What | - |
| Suspected Bribery (L) | `treat_jar_bribe` | Why | - |
| Ring of House Keys (L) | `rosa_keys` | Who | - |
| Herb Run - 5:45 PM (L) | `herb_run_545` | WhereWhen | 5:45 PM |

---

## Chapter 6 - The Garden: The Coward Behind the Shed

**Basil** is trimming a hedge that has nothing left to give, announcing his philosophy to the
hedge, the fountain, and the concept of justice: animals belong in **Nature**; **a cage is a
prison** with better catering. "Not that I would DO anything about it. That would be wrong.
Probably." Principles that loud usually want a witness. Or an alibi.

Sniff his muddy glove, loudly, and he holds up the **mud-caked glove** as evidence, realizes
evidence is exactly the problem, and puts it behind his back. Bulbs. Daffodils. Ask the flower
bed. The **freshly turned flower bed** backs him up. His other hand, Dikkie notices, is bare -
and the glove's partner is lying under a birdcage two rooms away.

The garden holds the case's biggest physical find. Behind the potting shed, under a tarp thrown
in a hurry: two big black road cases stenciled "STUDIO 4 - THIS SIDE UP - FRAGILE." Heavy
latches, thick **foam-lined** walls. Dikkie presses an ear to one, for science: **silence**,
the deep upholstered kind. Around them, the grass is pressed flat by **heavy boot prints**,
deep at the toe - somebody standing here was carrying weight. Gardeners wear boots. So does a
film crew.

By the hedgerow, the **bird bath**: a few **bright feathers** float on the surface, still
crisp, not waterlogged. Rudebeak was here **this evening**, out of his cage, having a lovely
time. (Feeds Petra's cross-room decode: soggy and smug is exactly when he lets himself be
picked up.)

Solve-gated beat - the case's biggest confession. Stare from his bare left hand to the mansion,
slowly. The snipping stops. The fountain burbles. Somewhere, a gull. Then it falls out of him
like loose soil: it is his glove by the cage. **He broke the latch** - with the shears, just
before six. The front door went, he panicked, and he ran for the garden and hid behind the
shed. Rudebeak let HIMSELF out and went straight to his bird bath, happy as spring. Then one
of those television people came wheeling **big black boxes** through - and Basil stayed
hidden. "I am a coward, cat. But I am not a thief." He admits the latch, never the theft, and
he becomes the case's second eyewitness to the snatch.

**Local solve:** "[__] is confessing to the hedge in installments. The [__] would fit the
damage two rooms away, half of his [__] pair never came back from the house - and the [__]
says Rudebeak made it out here alive tonight." (Answers: Basil / Bent Shears / Muddy Glove /
Evening Feathers - four slots, the biggest local board. The Sound-Proof Cases now genuinely
tempt the tool slot, the Boot Prints the suspect slot, the Flower Bed the last.)

| Clue | Id | Category | Time |
|---|---|---|---|
| Basil the Gardener (M) | `the_gardener` | Who | - |
| Bent Shears (M) | `shears_match_latch` | What | just before 6:00 PM |
| Sound-Proof Cases (M) | `sound_proof_cases` | What | stashed before lockdown |
| Cages Are Prisons (M) | `basil_free_the_bird` | Why | - |
| Muddy Glove (L) | `muddy_gloves` | What | this afternoon |
| Evening Feathers (L) | `bird_bath_feathers` | WhereWhen | this evening (~6 PM on) |
| Fresh-Turned Flower Bed (L) | `flower_bed_bulbs` | WhereWhen | this afternoon |
| Boot Prints by the Shed (L) | `boot_prints_shed` | Who | - |

---

## Chapter 7 - The Pool Area: The Bored Witness

**Coco** lowers her sunglasses exactly one centimeter. "Oh thank GOD. Someone with cheekbones."
She is the case's accidental star witness - bored, and therefore reliable.

Pose beside her chaise and she delivers the sighting: SOMEBODY lugged **huge black cases**
through the garden at, like, **quarter to seven** - walked straight through her sunset shot,
the nerve. And it was not just them: the gardener spent the whole evening **skulking behind
that shed** like a garden gnome with secrets, and Rosa stormed past the windows with that cart
of hers looking absolutely furious about something. Everyone in this house had somewhere to
be. Except Coco. She was radiant.

Investigate her abandoned **spritz** - warm all the way through, poured for sunset and
forgotten - proving she really was out here well before the light went. She adds the sound
texture: wheels all evening, **little squeaky wheels** up the garden path, the dinner cart
rattling through the house, rrr, rrr, rrr. Who can even tell one squeak from another? (Coco's
garden squeak and Derek's hall rumble are the same cargo, one hour apart - the player is left
to stitch that.)

The objects close the trap:

- **Coco's Phone**: a pool selfie timestamped **6:47 PM**. In the blurry background, a figure
  hauls large dark cases up the garden path. And at the very edge of the frame, half-hidden
  behind the potting shed: a **straw hat**, peeking. (One photo, two suspects placed: the
  snatcher in motion, and Basil the witness - hiding, not working.)
- The **Crumpled Schedule**: a ball of paper, thrown at the pool with feeling. It missed.
  Batted flat, it reads: "7:00 PM - Crew Assignments: **D. Marsh - Library (audio setup)**."
  The alibi Marsh recited in the library, in writing - and the photo says its owner was doing
  cardio through the begonias with two giant cases fifteen minutes early. Somebody balled it
  up and threw it away; schedules only mean something when people follow them.
- A single **bright feather**, red fading into blue, resting where the breeze left it. It
  could have blown in from anywhere. Dikkie resists the overwhelming urge to bap it. There
  were no witnesses either way.

Gated beat (on the schedule): sprawl across it and Coco reads over her sunglasses. "Seven
o'clock, library, audio setup? Honey. At quarter to seven that man was doing CARDIO through
the begonias with two giant cases. **I know what I saw**."

**Local solve:** "The crew's paperwork - the [__] - says where everyone was meant to be at
seven. But [__] says otherwise, and the [__] freezes the proof: those cases crossed the garden
while their owner was still on the clock somewhere else." (Answers: Production Schedule /
Coco's Sighting 6:45 / Photo 6:47 - three WHERE/WHEN slots, chronological, with the Abandoned
Spritz as the live decoy. Coco's Sighting is now a WhereWhen clue, "Coco's Sighting -
6:45 PM", so the final WHO bank holds only plausible suspects.)

| Clue | Id | Category | Time |
|---|---|---|---|
| Coco's Sighting - 6:45 PM (M) | `eyewitness_645pm` | WhereWhen | 6:45 PM |
| Photo - 6:47 PM (M) | `selfie_figure_cases` | WhereWhen | 6:47 PM |
| Production Schedule (L) | `production_schedule` | WhereWhen | 7:00 PM (assignment) |
| Straw Hat in Frame (L) | `straw_hat_witness` | Who | 6:47 PM |
| Abandoned Spritz (L) | `warm_spritz` | WhereWhen | before sunset |
| Bright Feather (L) | `feather_in_filter` | What | - |

---

## The True Timeline

What actually happened, assembled from the clues that carry each timestamp:

| Time | Event | Carried by |
|---|---|---|
| all day | Camera records - aimed at the chaise, cage out of frame | `documentary_camera` |
| 4:15 PM | Wexler signs in | `guest_registry` |
| 5:05 PM | Petra signs in, plus one macaw | `guest_registry` |
| 5:45 PM | Basil brings herbs in from the garden ("B.") - he is now indoors | `herb_run_545` |
| 5:50 PM | Coco signs in, heads for the pool | `guest_registry`, `warm_spritz` |
| just before 6:00 | Basil forces the cage latch with the shears, drops one glove, panics at the front door, bolts to hide behind the shed | `shears_match_latch`, `glove_by_cage`, Basil's confession |
| 6:00 PM | Rudebeak lets himself out; his snack goes untouched; evening splash at the bird bath | `missed_snack_6pm`, `bird_bath_feathers` |
| 6:30 PM | The crew column in the registry begins - one arrival is half an hour ahead of its own call sheet | `guest_registry`, `early_signin` (Reyes's confrontation) |
| 6:45-6:47 PM | A figure wheels two big cases through the garden; finds Rudebeak soggy and docile; Coco sees it, her selfie catches it - and catches Basil's straw hat watching from behind the shed | `eyewitness_645pm`, `selfie_figure_cases`, `straw_hat_witness` |
| 6:55 PM | Manifest: catering ice run | manifest (`inspect_delivery_manifest`) |
| 7:00 PM | Two equipment cases signed into the east wing under a scribble - while the schedule has D. Marsh in the library. Rosa's dinner cart is in the wing the same hour. Derek hears wheels - twice: once with soup smell, once without; the soupless one smelled of cable grease | `d_marsh_7pm`, `scrawled_signature`, `production_schedule`, `dinner_cart_7pm`, `husband_hall_7pm`, `hall_rumble_no_soup`, `cable_grease` |
| 7:10 PM | Linen return, housekeeping | manifest |
| 7:20 PM | Grip department wheels lamp returns through the wing | `lamp_returns_720` |
| before lockdown | The cases end up stashed behind the potting shed under a tarp, boot prints deep at the toe | `sound_proof_cases`, `boot_prints_shed` |
| 8:00 PM | The fake squawk: speaker under the couch, fed by the cable from the library mixing board | `squawk_at_8pm`, `hidden_speaker`, `audio_cable` |
| 8:15 PM | Reyes seals the mansion; north gate confirmed locked all evening | `lockdown_815pm`, `north_gate_locked` |

The design of the deceit: three sets of wheels share the 7:00 hour (cart, cases, lamps), so
every witness honestly misremembers. The 6:30 registry column vs the 7:00 manifest is the
final board's WHEN trap. And the 8:00 squawk moves the presumed crime two hours later than
the truth - only the untouched 6:00 snack bowl says otherwise.

## The Final Solve

Now a seven-slot chronology - the finale asks the player to SORT the evening:

"The [WHEN-1] put the thief inside before he was ever supposed to be there. The snatch is
frozen in the [WHEN-2], and the [WHEN-3] tracked his cargo right past the bedrooms. [WHO] hid
Rudebeak inside the [WHAT], kept the house believing with the [HOW], and it all comes back to
the [WHY]."

| Slot | Answer | The near-miss it must beat |
|---|---|---|
| WHEN-1 | Early Sign-In | Crew Arrivals - 6:30 PM (the innocent reading of the same page) |
| WHEN-2 | Photo - 6:47 PM | Coco's Sighting - 6:45 PM (testimony vs the photo that freezes it) |
| WHEN-3 | Manifest Entry - 7:00 PM | Second Rumble - No Soup (the sound of the move vs the record of it) |
| WHO | Sound Guy: D. Marsh | Derek (ultimatum in writing), Rosa (skull + keys), Basil (broke the latch), 'The Cases Are EMPTY' (the slip as pseudo-suspect) |
| WHAT | Sound-Proof Cases | Derek's Headphones, Broken Latch, Bent Shears, Smell of Cable Grease |
| HOW | Hidden Speaker | Derek's Headphones (the rival audio device), same What pool |
| WHY | Ruined Takes | Derek's Ultimatum, Insulted Cooking, Cages Are Prisons, PR Disaster |

The WHERE/WHEN bank holds eight macro chips for three slots - and the word bank now sorts them
chronologically, so the timeline is read, not remembered. The board only opens once every room
is solved AND every confrontation has been heard - Reyes, Vivienne, Derek, Rosa, Basil, and
Marsh each owe the player their slip before the accusation, which also guarantees the
confrontation clues (including the WHEN-1 answer) are in the notebook.

## The Red-Herring Web

- **Basil** - actually guilty of the latch. Glove at the scene, bent shears, loud philosophy,
  hides for the entire crime window. His confession converts him from suspect to witness.
- **Rosa** - motive in writing (skull), master keys, a heavy cart in the right wing at the
  right hour, ambiguous feathers, a polish cloth at the cage - and a script that never
  exonerates her ("maybe they are not wrong to look"). Her solve payoff hands back the
  cable-grease thread.
- **Derek** - motive in writing (ultimatum), a fresh audio cable, a purchase the day before.
  His actual solution was three hundred dollars and legal.
- **Chip** - all panic, no motive: fifteen percent of the bird's earnings argues loudly for
  innocence.
- **Wexler** - thrilled where he should grieve, red-pen "BETTER WITHOUT HIM?" - but his crime
  is loving the material. His real function is dropping the case's quietest true clue: Marsh
  stopped complaining two weeks ago.

---

## Loose Threads (tuning notes, not part of the script)

Status after the timeline rework:

1. **RESOLVED - the 6:55 "Garden supply run, B." manifest line** contradicted Basil hiding
   behind the shed; the entry is now a neutral "Catering ice run - 6:55 PM".
2. **OPEN - where is Rudebeak at the end?** Marsh blurts that both cases are EMPTY, but the
   final solve says he hid the bird inside them; the only pointer to the bird's actual
   whereabouts is Marsh's look toward the garden. A beat worth landing in the end scene.
3. **RESOLVED - Coco seeing Rosa's cart** now reads "stormed past the windows with that cart
   of hers".
4. **OPEN - the squawk heard by whom?** Only Vivienne attests to the 8:00 squawk in dialogue.
   A second earwitness (Chip? Coco through a window?) would strengthen the speaker gag - or
   keep it Vivienne-only as characterization. (The living room solve sentence now says "what
   the whole house swears it heard", which leans on the premise either way.)
