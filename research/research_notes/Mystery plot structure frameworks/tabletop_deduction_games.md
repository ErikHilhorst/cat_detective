# Tabletop deduction games: mystery structure and clue flow (lessons for Cat Detective)

Scope: how non-video deduction games (board games, party kits, escape-room boxes) build their mysteries, hand out clues, check solutions and pay off the player, plus what reviewers say works or fails. Every finding below has an inline source. Inferences mapped to Cat Detective's systems (clue chips, per-room mad-libs boards, the final timeline board, gated confrontation topics) are marked as inferences.

Source quality: most sources are hobby reviews (Opinionated Gamers, Room Escape Artist, Board Game Quest, Zatu, Meeple Mountain) and rulebook mirrors. Designer-voiced material was thin. The Shut Up & Sit Down MicroMacro review returned HTTP 403, so it is not used.

## Cluedo/Clue: pure elimination logic, the who/what/where solution space, and why it lacks narrative

### Takeaway
Cluedo is a closed constraint problem with 6 suspects x 6 weapons x 9 rooms = 324 possible answers. Skill comes from logic over what you have NOT seen, not from story. The "narrative" is just a label on a combination, so no answer is more meaningful than any other.

### Cited Findings
- 6 x 6 x 9 = 324 possible solutions — [Kiddle/Cluedo](https://kids.kiddle.co/Cluedo); [Cluedo (IPFS wiki mirror)](https://bafybeiemxf5abjwjbikoz4mc3a3dla6ual3jsgpdr4cjr3oz3evfyavhwq.ipfs.4everland.io/wiki/Cluedo.html)
- Beginners reason only from cards they hold or have seen. Experts also use propositional logic about cards they have not seen, which solves the game in fewer turns. Adding extra information to the logical deductions sometimes made things worse — [Kingston, "Comparing question answering strategies for Cluedo" (Univ. of Brighton)](https://research.brighton.ac.uk/en/publications/comparing-question-answering-strategies-for-cluedo/)
- Deduction games can be framed as constraint satisfaction problems (low-authority source) — [Vectree: game deduction combinatorics](https://vectree.io/c/game-deduction-combinatorics)

### Inferences
- Cat Detective's slot categories (WHO / WHAT / WHY / WHERE-WHEN) are a Cluedo-style factored solution space. The difference is that each chip carries story context. That fixes Cluedo's weakness, because a wrong combination is *implausible*, not just eliminated.
- The final board has 7 slots, and each has a bank of 4-8 chips. The raw combination space is far larger than Cluedo's 324. That is fine only because partial-progress feedback ("2/3 fit") shrinks it. Without that feedback, brute force would be hopeless and frustrating.
- Decoy chips play the role of Cluedo's unseen cards. They are worth having only when they can be eliminated *by reasoning about story facts*, not by trial.

### Gaps
- I found no authoritative design critique (designer or critic) that explicitly argues "Cluedo lacks narrative." The claim is widely held, but I found no source to cite.

## Sherlock Holmes Consulting Detective (SHCD): leads directory, newspaper, scoring against Holmes, end-of-case questionnaire, leaps of logic

### Takeaway
SHCD is open-world deduction. Players freely visit any address from a directory, then answer a question sheet. Score = points for correct answers minus a penalty for every lead beyond Holmes's par. It is loved for open reasoning and criticized for "reading the author's mind" and Holmes's leaps of logic.

### Cited Findings
- When satisfied, players answer questions at the back of the case book. "Whodunit?" is one question, and tangential questions earn fewer points. You lose 5 points per location visited beyond par (equivalently, every location costs 5 but some are free). Holmes's solution is worth 100 by fiat and sets par — [The Stack: SHCD Scoring and Progression](https://www.wurb.com/stack/?p=6720)
- Players are Baker Street Irregulars, not Holmes. Failure is part of the design, since you are meant to be impressed when Holmes gets it right. Unlike a novel, SHCD lets you miss key clues and implies the fault is yours. Some end questions reference characters you never met — [The Stack](https://www.wurb.com/stack/?p=6720)
- The critic says there is "no exit, no definite goal", so progress is hard to define. The game is "too dependent on reading the author's mind." In one case the critic answered half the questions correctly, but visiting too many locations wiped out the points. He suspects the designers "underestimated the difficulty and didn't do enough playtesting" — [The Stack](https://www.wurb.com/stack/?p=6720)
- A defense of the scoring: it is "an integral part of the game... an important driver of its aesthetics." It rewards Holmesian economy and inductive reasoning ("We balance probabilities and choose the most likely"). Many clues are redundant and the key information sits in a few paragraphs: "The substance of Consulting Detective is in reasoning... not reading." — [Erik Twice: SHCD, the aesthetics of scoring and murder](https://eriktwice.com/?p=6134)
- Holmes makes "major, perhaps unreasonable" logical jumps and typically visits about 6-8 locations per case — [Geeks Under Grace review](https://www.geeksundergrace.com/tabletop/review-sherlock-holmes-consulting-detective/)
- The leaps of logic and the record-scratch storytelling are a known weakness. The reviewer advises playing for fun rather than for a perfect score — [Tabletop Gaming: Jack the Ripper & West End review](https://tabletopgaming.co.uk/reviews/sherlock-holmes-consulting-detective-jack-the-ripper-west-end)
- One player followed 8 leads and still had to make serious logical leaps, then named the wrong killer — [Black Gate: The Public Life of Sherlock Holmes](https://www.blackgate.com/?p=126636)

### Inferences
- **The question sheet works better than a single accusation**, because partial understanding still earns credit ("half the questions correct"). Cat Detective's per-room boards plus the 7-slot final board, with "N/M fit" feedback, are the same idea, delivered incrementally rather than in one sitting at the end.
- **Efficiency scoring backfires when the solution needs author-mind-reading.** SHCD's lead penalty punishes the thoroughness its leaps of logic require. Cat Detective deliberately avoids a lead penalty (revisiting a source to re-read a time is "intended play"). Keep it that way unless the logic chain is airtight.
- **The "Holmes did it in 6 leads" comparison** could become a gentle optional epilogue stat (e.g. "Dikkie's mentor solved it after N interviews") without punishing the player. This is an idea, not a sourced practice.
- **Questions about things the player never saw** are a top frustration. Cat Detective's rule that every final answer has an ungated source already guards against this.

### Gaps
- I did not fetch a primary rulebook for the exact newspaper mechanics or the exact split into primary and secondary questions. The Stack confirms only that "tangential questions earn fewer points." Treat the "two-part questionnaire" detail as unverified here.

## Detective: A Modern Crime Board Game (Portal Games): time budget, leads, Antares database, final report

### Takeaway
Every action costs in-game hours against a fixed deadline, with overtime tokens as a hard fail. Leads branch outward from each card. A web database (Antares) holds transcripts and records. The case ends with app-scored multiple-choice report questions, scored out of 40, a scale rather than a win or loss.

### Cited Findings
- Most actions take set time. Working past 8 hours costs overtime tokens, and "run out of tokens and the detectives run themselves into the ground and you fail the case." Each case also has an overall time limit — [Zatu: Detective review](https://zatu.com/blogs/reviews/detective-review)
- Each investigated scene, interviewed suspect or analysed piece of evidence "will usually give the players a lead." Skill tokens allow deeper actions (pressing suspects, deeper analysis) at a time cost — [Zatu](https://zatu.com/blogs/reviews/detective-review)
- The game "relies heavily on the Antares web app, a 'detective database'" for interview transcripts, suspect data and fingerprint comparison. It uses the same numbering as the cards — [Zatu](https://zatu.com/blogs/reviews/detective-review)
- When time runs out, players answer questions (killer, motive, weapon...) in the app, which scores out of 40: "There isn't necessarily a win or a lose scenario, but simply a scale on how good of a detective you are!" — [Zatu](https://zatu.com/blogs/reviews/detective-review)
- The Antares database asks multiple-choice case questions, and more right answers mean a higher score — [Board-Game.co.uk review](https://board-game.co.uk/?p=200008)
- Every action (following a lead, the archives, questioning) costs hours — [Detective Hawk Games review](https://detectivehawkgames.com/blogs/news/detective-a-modern-crime-board-game-review)
- Praise: it "breaks away from the linear mould," and recurring subtle clues link the cases into a larger reveal. Criticism: lack of replayability, and it "can become very lengthy" — [Zatu](https://zatu.com/blogs/reviews/detective-review)

### Inferences
- **Scale-not-win scoring** suits a cozy game. Cat Detective could grade the final solve (first-try submit, number of wrong submits, confrontations heard) without ever failing the player.
- **Multiple-choice report questions** are the tabletop cousin of Cat Detective's chip banks. The answer options are visible, so the challenge is discrimination among near-misses, not recall. That is why near-miss decoys (6:45 vs 6:47, 7:00 vs 7:20) carry the difficulty.
- **Cross-case recurring clues** ("a larger reveal") are a model for future Cat Detective cases: a minor thread that pays off across cases.
- A hard time budget would clash with Cat Detective's "revisit freely" philosophy. Note it as a known alternative, not a recommendation.

### Gaps
- I did not confirm Portal's own statements on design intent, or on question weighting within the 40 points.

## Chronicles of Crime (Lucky Duck Games): app + QR scanning, interrogating characters about items, VR crime-scene search, scoring

### Takeaway
The core verb is "ask person X about card Y". Any character can be asked about any evidence or person card, which makes a cross-product interrogation matrix. Crime scenes are a timed 360-degree phone search. Every action burns clock time, and the end is an app questionnaire at HQ.

### Cited Findings
- "Everything revolves around scanning QR codes." Scanning a location travels there. Scanning a person starts a conversation, and scanning an item or person card during it asks about that card — [Board Game Quest review](https://boardgamequest.com/chronicles-of-crime-review)
- Scenes are searched by physically turning the device through 360 degrees. The search is timed, so players call out what they see while others hunt the item decks for matching cards. The reviewer calls the VR scenes "brilliant and absolutely my favorite part." — [Board Game Quest](https://boardgamequest.com/chronicles-of-crime-review)
- Four specialists can be consulted about people and evidence — [Board Game Quest](https://boardgamequest.com/chronicles-of-crime-review)
- "Everything you do... takes time off the clock, which will affect your final score." Time can also change the narrative. At the end you report to your boss and answer questions. If you lack evidence, the app sends you back out — [Board Game Quest](https://boardgamequest.com/chronicles-of-crime-review)
- Each scan, interrogation or scene costs 5 in-game minutes, and faster solves score better — [Chronicles of Crime rules mirror](https://lehi-innovation.github.io/boardgame-rules/rules/chronicles-of-crime/)
- Criticism: "a little too fiddly with the constant QR scanning." Reviewing information is harder because it is locked in the app. The mysteries are fairly linear, so stuck players "resort to scanning everything to see what sticks" — [Board Game Quest](https://boardgamequest.com/chronicles-of-crime-review)

### Inferences
- **"Scan everything" is the tabletop equivalent of chip brute-forcing.** When a game cannot tell good questions from exhaustive ones, players fall back on exhaustion. Cat Detective's partial-progress feedback is safer than a per-action time cost, but watch for players spamming submits. One mitigation is the existing "N/M fit" message, which rewards thought rather than blind permutation.
- **Ask-X-about-Y is a strong confrontation model.** Cat Detective's `requiresClue` topics are a curated subset of that matrix: the character reacts to a specific piece of evidence. Expanding a few "show Dikkie's evidence" reactions per character is a cheap way to make interrogation feel systemic.
- **Information locked in an app is hard to review.** Cat Detective's casebook (inspector descriptions, "The Evening" timeline on the notes page) already addresses the "reviewing info is harder" complaint. Make sure every chip shows where it came from.
- **"Not enough evidence, go back out"** is a soft-gate pattern that resembles Cat Detective's FINAL SOLVE confrontation lock.

### Gaps
- I did not find designer commentary from Lucky Duck on how they authored the interrogation matrix (e.g. default "I don't know" replies versus bespoke ones).

## Exit: The Game / Unlock! / Deckscape / Escape Room: The Game: puzzle chaining, gating, hint systems, linear vs parallel paths

### Takeaway
Escape-in-a-box games chain puzzles behind code checks (decoder wheel, app code entry, answer cards). They supply graded hints (nudge, then heavy hint, then solution), score by time and hints used, and use answer-verification artifacts so players cannot brute-force.

### Cited Findings
- EXIT is designed by Inka and Markus Brand (Kosmos). They describe it as closer to an experience than a game — [Mojo Nation interview](https://mojo-nation.com/inka-markus-brand-origins-escape-room-inspired-exit-game-series/)
- Each puzzle has three hint cards. The first is a nudge, or lists the needed riddle cards and components. The second is a heavier push. The third gives the solution and how to get there. You score at the end from elapsed time and hint cards used — [Meeple Mountain EXIT mega review](https://www.meeplemountain.com/reviews/exit-the-game-mega-review-puzzling-out); [Board Game Quest EXIT review](https://www.boardgamequest.com/exit-the-game-review/)
- A three-part code on the decoder wheel points to an answer card. Answer cards exist so players cannot accidentally brute-force the wheel — [Room Escape Artist EXIT review](https://roomescapeartist.com/2017/07/06/exit-the-game-the-abandoned-cabin-the-secret-lab-the-pharaohs-tomb-review/); [I Slay the Dragon EXIT review](https://islaythedragon.com/featured/review-exit-the-game)
- Unlock!: press "Hint" in the app at any time and enter a revealed card's number. Some cards offer a second hint and possibly the solution. Penalty cards (wrong combinations) generally cost a few minutes. Some machines cannot be understood until later in the adventure — [Space Cowboys Unlock! rules PDF](https://cdn.svc.asmodee.net/production-spacecowboys/uploads/2025/12/SCUNL11EN_Rules_depliant_3volets-2.pdf); [Unlock! rulebook mirror](https://rulespal.com/unlock-escape-adventures/rulebook)
- Deckscape: an intro card says not to flip or reorder cards unless instructed, which implies a fixed sequence — [Room Escape Artist Deckscape Test Time](https://roomescapeartist.com/2018/01/19/deckscape-test-time-fate-london-product-review)
- Escape Room: The Game (Identity Games, a Dutch publisher): each mission has three parts. Each part ends by inserting 4 keys into the Chrono Decoder against a 60-minute countdown. One reviewer found it gimmicky and disliked that it cannot be paused — [Board Game Quest Escape Room review](https://boardgamequest.com/escape-room-the-game-review)

### Inferences
- **The graded three-step hint ladder (nudge, then direction, then answer)** maps well to Cat Detective's stuckness problem. For example, a board-side "Ask Dikkie's whiskers" could (1) name which room or character holds a missing chip, (2) highlight the relevant category, (3) fill one slot. Score it only softly, if at all.
- **Answer verification that resists brute force**: EXIT's answer cards exist because a 3-wheel code is guessable. Cat Detective's "N/M fit" feedback is generous. Consider limiting it to stating the count (not which slots), as now, and never showing which slot is wrong. That keeps the deduction from collapsing into slot-by-slot hill-climbing.
- **Three-part missions** (Escape Room: The Game) resemble Cat Detective's room boards feeding the final board: staged local closure plus a final lock.
- **Linear vs parallel**: these sources only imply linearity (Deckscape's ordering rule). Cat Detective's structure is a hybrid: one forced order (bedroom before living room), then free movement. That is close to how escape rooms open up after an intro bottleneck.

### Gaps
- I found no designer-voiced source on linear vs parallel puzzle-path design for these lines, and no source stating whether Unlock! app hints cost time.

## MicroMacro: Crime City (Pegasus / Edition Spielwiese, Spiel des Jahres 2021): visual search and the case-card question chain

### Takeaway
One huge black-and-white city map, with every case hidden in it simultaneously. Each case is a short deck. A black intro card sets the crime, then each card asks one narrow "find X" question with a location hint, and the back of the card self-checks. Answering the micro questions in sequence reconstructs the macro story (how, why, where). Cases are rated 1-5 stars.

### Cited Findings
- The first card of each case is black. Following cards show the next thing to search for, with location hints ("southwest corner near the water," "close to city hall"). You find it, flip the card, and the back shows the target illustration (often with coordinates) to confirm. After all cards "you'll have unraveled the mystery" (how, why, where) — [Opinionated Gamers: Dale Yu review](https://opinionatedgamers.com/2021/01/20/dale-yu-review-of-micromacro-crime-city/)
- There are 16 cases in two sealed decks, rated 1-5 stars in ascending difficulty. The 1-2 star cases are "ridiculously easy" but teach the format, and later cases are "much more complex." Most cases take 10 minutes or less with the cards — [Opinionated Gamers](https://opinionatedgamers.com/2021/01/20/dale-yu-review-of-micromacro-crime-city/)
- Optional expert mode: read only the first card and reconstruct the case from the map unaided. The reviewer recommends playing the first 2-3 cases by the rules, since it "would be brutally difficult" otherwise — [Opinionated Gamers](https://opinionatedgamers.com/2021/01/20/dale-yu-review-of-micromacro-crime-city/)
- Questions often trace a person's movements or actions through the day. Some players try to solve the case before reading the questions — [Crossexamining Crime review](https://crossexaminingcrime.com/2026/09/06/micromacro-crime-city-a-board-game-review/); [What's Eric Playing #988](https://whatsericplaying.com/2022/12/05/micromacro-crime-city/)
- Praise: art detail is "astounding," and the designers used no cheap tricks, so no clue felt unattainable. Criticism: "more of a puzzle than a game," and no big finale — [Meeple Mountain](https://www.meeplemountain.com/reviews/micromacro-crime-city/); [Tabletop Gaming review](https://www.tabletopgaming.co.uk/?p=7329); [Cooperative Gestalt review](https://coopgestalt.com/2021/08/21/a-review-of-micromacro-crime-city/)
- 2-3 players is the practical maximum because of the crowded map — [Opinionated Gamers](https://opinionatedgamers.com/2021/01/20/dale-yu-review-of-micromacro-crime-city/)

### Inferences (most directly transferable game)
- **The micro-to-macro question chain is exactly Cat Detective's room-board-to-final-board structure.** MicroMacro's lesson is that each micro step should be a *narrow, answerable, self-verifying* question, and that the macro answer should emerge rather than be asked cold. Cat Detective's per-room boards are the micro cards and the final timeline board is the emergent macro.
- **Instant self-check per step** (flip the card) prevents compound errors. Cat Detective's local solve plays that role. Keep local boards small and checkable.
- **Ascending star ratings plus easy teaching cases** mirror the tutorial case (Whisker Academy). Consider displaying a difficulty rating per case or per room.
- **Expert mode (questions hidden)** suggests an optional "hard mode": the final board without the sentence frame, or with fewer decoy-free hints.
- **Timeline-through-a-day questions** are MicroMacro's staple, confirming that "trace someone's evening" is a satisfying deduction format. This is the core of Cat Detective's TIME-clue mechanic.
- **The "no big finale" criticism** argues for Cat Detective's end scene and Rudebeak reveal beat. A question chain alone feels anticlimactic.

### Gaps
- I could not access the SUSD review (HTTP 403). I did not verify the exact card count per case from the rulebook. The rulebook PDF exists at [nastol.io MicroMacro rules](https://nastol.io/storage/games/files/micromacro_crime_city/662858/MIMA_Anleitung_EN_digital_V1_200914.pdf), but I did not fetch it.

## Murder mystery party games and subscription boxes (Hunt A Killer, How to Host a Murder, party kits): character packets, staged clue release, final accusation

### Takeaway
Party kits release information in rounds through private character booklets and numbered clue envelopes, ending in a round of individual accusations and a scripted reveal. Hunt A Killer stretches this over monthly boxes. Its season-1 reviews show the failure modes of no progress feedback, unclear "done" states, and every object looking like it might matter.

### Cited Findings
- How to Host a Murder (original): suspects reveal clues across rounds, each round has its own double-page booklet section, and at the end of round four each guest reveals who they think the murderer is and how it was done (archived page; the current edition may differ) — [How to Host a Murder (Wikipedia 2007 archive)](https://wikipedia2007.classicistranieri.com/h/o/w/How_to_Host_a_Murder_c3a0.html)
- Character booklets contain bios and per-round objectives or tasks — [Pender Host Booklet PDF](https://cdn.shopify.com/s/files/1/0469/8229/9803/files/Pender_Host_Booklet.pdf). Booklets give each guest two things to say or do per round — [Priscilla Tempest Murder Mystery Night](https://douglas-mcintyre.com/blogs/news/priscilla-tempest-murder-mystery-night)
- Kits commonly use 3 rounds of clues with numbered or colour-coded evidence cards and envelopes. For example, one kit has 17 evidence cards plus 6 secret clue envelopes — [Talking Tables host booklet PDF](https://trade.talkingtables.com/cdn/shop/files/HOST_MYSTERY_INSTRUCTION_BOOKLET_FRIENDLYCUST.pdf); [Talking Tables Mystery Dance PDF](https://cdn.shopify.com/s/files/1/2636/1632/files/HOST-MYSTERY-DANCE_INSTRUCTION_BOOKLET.pdf)
- Each guest accuses, and giving reasoning is optional — [Broadway Murder Mysteries: How to play](https://www.broadwaymurdermysteries.com/pages/how-to-play)
- Hunt A Killer Season 1: nine monthly boxes (about $165 per season). Players are pen pals of an incarcerated killer and the boxes hold letters, clippings, notebooks and physical items. "Inspector's Notes" in-character hints were added mid-season as "an essential addition." There are only 1-2 explicit puzzles per box, and they "almost never identified themselves" — [Room Escape Artist: Hunt A Killer S1](https://roomescapeartist.com/2017/11/27/hunt-killer-season-1-review/)
- Hunt A Killer S1 failures: "we never knew how much we had actually solved nor what we were still missing". Boxes "didn't conclude with any resolution," so every component seemed to need research ("sharpened [a pencil] until it was a freaking nub"). The reviewer also cites backtracking dread, rules changed mid-season, "too many logic leaps," and a story that "buckled" when it went epic. The epilogue's box-by-box recap was praised — [Room Escape Artist](https://roomescapeartist.com/2017/11/27/hunt-killer-season-1-review/)
- Later standalone Hunt A Killer games: some buyers report a missing or empty solution envelope, or a solution that requires the website (anecdotal, low-quality summary) — [ShopSavvy summary](https://shopsavvy.com/reviews-tldr/hunt-a-killer:-lakeside-slaughter-game); clue quality is praised, but there is "a lot to read" in box 1 — [Reviewed.com](https://reviewed.com/lifestyle/features/hunt-a-killer-review-ultimate-date-night-mystery-box)

### Inferences
- **Staged release per round** maps to Cat Detective's room order and `requiresSolve` topics: new information arrives only once a stage is closed, which keeps any phase from overloading the player.
- **Hunt A Killer's core failure, no "done" signal, is exactly what Cat Detective's playtest-3 HUD cues fix** (a SOLVE pulse when all clues are found, the room counter, "M/N confronted"). This is strong external validation to keep them.
- **"Is this pencil a clue?" anxiety**: Cat Detective's rule that every flavor object yields *some* clue, with explicit chip unlocks, removes the ambiguity. Physical-box games cannot do this.
- **The epilogue recap that explains each item** is a payoff players value. Cat Detective's end scene could include an optional "how it all fit" recap of key chips.
- **Individual accusations with optional reasoning** are weak verification. A party kit cannot check reasoning, while Cat Detective's slot boards check it structurally.

### Gaps
- I found no current official How to Host a Murder rulebook. Round counts are from an archive or other brands.

## Other deduction titles: Cryptid, Mysterium, Decrypto, Suspects (Studio H), Escape Tales, Dutch titles

### Takeaway
Suspects scores by *how few clue cards you read before guessing*, and the score selects the ending. Escape Tales gates exploration with scarce action tokens and branches the story. Cryptid shows pure incremental-elimination deduction. Mysterium and Decrypto were not covered by sources found.

### Cited Findings
- **Suspects (Studio H / Hachette)**: Agatha Christie-style, three cases per box. Scoring by cards drawn before the guess is 1-30 = 5 pts, 31-45 = 4, 46+ = 3, wrong = 0. Total score determines which story ending you get. Players can play together but guess individually, so a group can end with different scores. Con: little to no replayability — [GamingTrend Suspects review](https://gamingtrend.com/reviews/suspects-review-great-mystery-adventures-for-the-whole-group); [Tabletop Family](https://thetabletopfamily.com/?p=34883); [Meeple Mountain: MacGuffin Affair](https://www.meeplemountain.com/reviews/suspects-the-macguffin-affair/)
- **Escape Tales: The Awakening (Board&Dice)**: story-heavy card escape room with no time limit. You spend an action token to explore one area of a location (start with 6, 12 areas per location), and tokens are scarce. Choices change the story and the puzzles. There is no penalty for hints or wrong answers, and you can save anywhere. A replay repeats about 70% of the puzzles and story, with the endings differing most — [Punchboard review](https://punchboard.co.uk/review-escape-tales-the-awakening/); [Opinionated Gamers](https://opinionatedgamers.com/2019/01/09/dale-yu-review-of-escape-tales-the-awakening/); [Meeple Mountain](https://www.meeplemountain.com/reviews/escape-tales-the-awakening/)
- **Cryptid (Osprey)**: each player holds exactly one clue about the creature's location, which is pure deduction with "an engaging sense of incremental deduction." Criticisms: no room for bluffing, fragile (one mistake can collapse the game), weak theme — [Meeple Mountain Cryptid](https://meeplemountain.com/reviews/cryptid); [Tabletop Gaming Cryptid](https://tabletopgaming.co.uk/Reviews/cryptid-review); [Roll to Review](https://rolltoreview.com/cryptid-board-game-review/)
- **Dutch publisher**: Identity Games (Netherlands) makes Escape Room: The Game (see the escape-box section) — [Board Game Quest](https://boardgamequest.com/escape-room-the-game-review)

### Inferences
- **Suspects' "fewer clues read = higher score, and the score picks the ending"** is a cozy-friendly scoring model. Cat Detective could vary end-scene flavor (e.g. a closing line from Reyes) by first-try final submit or wrong-submit count, never blocking the true ending.
- **Escape Tales' no-penalty hints and save-anywhere** fit a cozy tone. Its scarce exploration tokens are the opposite of Cat Detective's free revisiting, so they are a known alternative, not a fit.
- **Cryptid's fragility** (one wrong fact poisons everything) warns against final boards where a single misread clue makes the whole chain unsolvable. Cat Detective's multi-source pairing (event and time stated once at the source, revisitable) mitigates this.

### Gaps
- No sources were retrieved for Mysterium or Decrypto (communication and clue-giving games, less relevant to fixed-solution mysteries). No Dutch-language detective gezelschapsspellen beyond Identity Games were researched.

## Cross-cutting: stuckness, hints, failure states, question-sheet solution checks, staged reveals, replay and score

### Takeaway
The genre converges on a few patterns. Answering a question set beats naming one culprit. Graded, opt-in hints beat binary give-up. Scoring is a sliding scale rather than pass or fail. Explicit progress and "done" signals are essential. The biggest recurring complaints are leaps of logic, author mind-reading, not knowing what matters, and brute-force fallbacks.

### Cited Findings
- **Question sheets give partial credit.** SHCD awards points per question, with tangential questions worth less — [The Stack](https://www.wurb.com/stack/?p=6720). Detective scores multiple choice out of 40, "a scale on how good of a detective you are" — [Zatu](https://zatu.com/blogs/reviews/detective-review). Chronicles of Crime ends with a boss questionnaire — [Board Game Quest](https://boardgamequest.com/chronicles-of-crime-review)
- **Hints**: EXIT has a 3-card ladder scored by hints used — [Meeple Mountain](https://www.meeplemountain.com/reviews/exit-the-game-mega-review-puzzling-out). Unlock! has app hints per card, sometimes up to the full solution — [Space Cowboys rules](https://cdn.svc.asmodee.net/production-spacecowboys/uploads/2025/12/SCUNL11EN_Rules_depliant_3volets-2.pdf). Hunt A Killer had to retrofit in-fiction hints ("Inspector's Notes") — [Room Escape Artist](https://roomescapeartist.com/2017/11/27/hunt-killer-season-1-review/). Escape Tales has no hint penalty — [Punchboard](https://punchboard.co.uk/review-escape-tales-the-awakening/)
- **Efficiency scoring**: SHCD charges 5 points per extra lead — [The Stack](https://www.wurb.com/stack/?p=6720). Chronicles of Crime charges 5 minutes per scan — [rules mirror](https://lehi-innovation.github.io/boardgame-rules/rules/chronicles-of-crime/). Suspects tiers points by cards read — [GamingTrend](https://gamingtrend.com/reviews/suspects-review-great-mystery-adventures-for-the-whole-group). A defense: efficiency scoring makes the game about reasoning, not reading — [Erik Twice](https://eriktwice.com/?p=6134)
- **Failure states**: Detective has a hard fail when overtime runs out — [Zatu](https://zatu.com/blogs/reviews/detective-review). Chronicles of Crime sends you back if evidence is insufficient — [Board Game Quest](https://boardgamequest.com/chronicles-of-crime-review)
- **Brute force when stuck**: in Chronicles of Crime, players "scan everything to see what sticks" — [Board Game Quest](https://boardgamequest.com/chronicles-of-crime-review). EXIT's answer cards prevent wheel brute-forcing — [Room Escape Artist](https://roomescapeartist.com/2017/07/06/exit-the-game-the-abandoned-cabin-the-secret-lab-the-pharaohs-tomb-review/)
- **Staged reveals and self-check**: MicroMacro has a per-card flip check — [Opinionated Gamers](https://opinionatedgamers.com/2021/01/20/dale-yu-review-of-micromacro-crime-city/). Party kits release information by rounds and envelopes — [Talking Tables PDF](https://trade.talkingtables.com/cdn/shop/files/HOST_MYSTERY_INSTRUCTION_BOOKLET_FRIENDLYCUST.pdf)
- **Replay**: limited replayability is a near-universal con (Detective, Suspects, Escape Tales) — [Zatu](https://zatu.com/blogs/reviews/detective-review); [GamingTrend](https://gamingtrend.com/reviews/suspects-review-great-mystery-adventures-for-the-whole-group); [Opinionated Gamers](https://opinionatedgamers.com/2019/01/09/dale-yu-review-of-escape-tales-the-awakening/)
- **Top complaints**: leaps of logic and author mind-reading (SHCD, Hunt A Killer) — [The Stack](https://www.wurb.com/stack/?p=6720); [Room Escape Artist](https://roomescapeartist.com/2017/11/27/hunt-killer-season-1-review/)

### Inferences (design lessons for Cat Detective)
1. **Keep the question-sheet philosophy.** Mad-libs boards are a structured questionnaire. Partial-fit feedback is the digital version of partial credit. Never move to a single accusation.
2. **Micro-to-macro (MicroMacro) is the backbone.** Each room board should be a narrow, self-checking micro question whose answer is reused, or recontextualized, on the final board.
3. **Add a graded, opt-in hint ladder** (EXIT and Unlock style), in fiction ("Dikkie's whiskers twitch..."). Tier 1 points to a room or character with unfound clues. Tier 2 names the slot category to reconsider. Tier 3 fills a slot. This addresses stuckness without breaking cozy tone.
4. **Score softly, never fail.** Use a Detective or Suspects scale ("Paw-fect deduction" vs "Got there in the end"), driven by wrong submits or hints, which at most varies an epilogue line. Avoid SHCD-style lead penalties, since they punish the revisiting that the timeline mechanic requires.
5. **Always signal "done".** Hunt A Killer shows that no-progress-feedback is fatal. The existing SOLVE pulse, counters and confrontation lock are correct.
6. **Resist brute force.** Report only the count of fitting slots, never which ones. Consider a small cooldown or in-fiction flavor after repeated wrong submits, rather than a hard penalty.
7. **No leaps of logic.** Every final answer must be derivable from visible chip text. The "half hour before his call" arithmetic is the right kind of step: explicit inputs, one operation.
8. **Give a finale.** MicroMacro's "no big finale" criticism and Hunt A Killer's praised epilogue recap both argue for a payoff scene that explains how the chips fit.
9. **Interrogation as a matrix** (Chronicles of Crime): selective "react to evidence X" topics make characters feel systemic. Cat Detective's `requiresClue` topics are this, curated.

### Gaps
- No quantitative player data (completion rates, hint usage) was found for any title.
- Designer interviews on clue-flow authoring (Portal, Lucky Duck, Space Cowboys, Pegasus) were not found in this pass.
