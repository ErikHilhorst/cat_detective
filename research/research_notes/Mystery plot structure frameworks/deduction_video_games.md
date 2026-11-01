# How Deduction / Detective Video Games Turn Mystery Structure into Mechanics

Scope note: research budget covered Golden Idol, Obra Dinn, Ace Attorney, Her Story, Overboard!, Paradise Killer, L.A. Noire, Duck Detective and the GMTK framing videos in depth. Disco Elysium, Danganronpa, Shadows of Doubt, Sherlock Holmes (Frogwares), Murder by Numbers, Strange Horticulture, Lorelei, Type Help, Botany Manor were NOT verified with sources this round - see Gaps sections; anything said about them there is from memory and unverified.

## The Case of the Golden Idol / Rise of the Golden Idol (Color Gray Games) - closest analogue to our mad-libs boards

### Takeaway
Golden Idol's word-bank fill-in-the-blank boards were tuned by playtesting into a specific feedback granularity: per-slot checking frustrated players ("so many places they might be wrong"), whole-board right/wrong also frustrated them, and the shipped compromise ("two or fewer slots are incorrect" indicator) reduced difficulty and length - a tradeoff the designer names explicitly. Scenarios are built as self-contained mysteries that each also serve a macro story beat.

### Cited Findings
- Designer Andrejs Klavins: the genre lacked "deduction games that make the player feel like a detective through gameplay"; cites Obra Dinn and Her Story as games that do it well. — [Game Developer: Pursuing the "Aha!" moment](https://www.gamedeveloper.com/design/case-of-the-golden-idol)
- Feedback iteration: with early per-slot checking players "would get very frustrated" because "there were so many places they might be wrong"; then "evaluating the whole puzzle as wrong or right frustrated the players"; the shipped indicator that "two or fewer slots are incorrect" rewarded being close but "this reduced the difficulty and length of the game." — [Game Developer](https://www.gamedeveloper.com/design/case-of-the-golden-idol)
- Brute-force awareness: Klavins quotes the maxim "Given the opportunity, players will optimize the fun out of a game"; the team kept input constrained and did not validate slots individually. — [Game Developer](https://www.gamedeveloper.com/design/case-of-the-golden-idol)
- "Those precious 'Aha!' moments are something that we strived for"; testers would get "an insight with a loud 'Aha!'" after being stuck for a long time. — [Game Developer](https://www.gamedeveloper.com/design/case-of-the-golden-idol)
- Playtesting: tested "at least 5-7 times," developers sat silently while players struggled; "making such a game is a matter of trusting the players, which is not often easy." — [Game Developer](https://www.gamedeveloper.com/design/case-of-the-golden-idol)
- Scenario design: "Each scenario had to serve a macro story beat for the narrative" while also being a self-consistent mystery; clues kept to what is "necessary to the solution" or useful as misdirection; some scenarios were too clear and had to be obfuscated (e.g. a written will was removed because it made a case trivial). — [Game Developer](https://www.gamedeveloper.com/design/case-of-the-golden-idol)
- Two scenario types: "whodunnit" vs "what the hell is happening here," the latter being the team's stronger kind. Klavins: "we have to admit we failed to create a reliable framework for mystery building." — [Game Developer](https://www.gamedeveloper.com/design/case-of-the-golden-idol)
- "Thought Path" clue balancing: "if players would suddenly bypass many of these steps with some other clue, we knew that we had to remove that clue." Difficulty failure mode to avoid: "if a player understands the main mystery immediately and then is stuck for thirty minutes on some technicality." Each scenario was worked on "as an individual story with its own mystery"; narrative "elevates the puzzle-solving, grounding it in reality and making it more purposeful." — [Game Developer: frequent testing](https://www.gamedeveloper.com/road-to-igf-2023/-the-case-of-the-golden-idol-i-used-frequent-testing-to-improve-its-mystery-solving)
- Designing puzzle-by-puzzle and chapter-by-chapter distinguishes it from Obra Dinn's single large mystery (search-snippet summary). — [Game Developer](https://gamedeveloper.com/business/-the-case-of-the-golden-idol-i-used-frequent-testing-to-improve-its-mystery-solving)
- Rise of the Golden Idol: five chapters of four scenarios each; goal per scenario = what, where, when, why. — [Gameluster review](https://gameluster.com/the-rise-of-the-golden-idol-review-the-rise-and-fall-of-the-golden-idol/); sequel trades for greater level density and a more interconnected design where players jump between scenarios to check details — [Slant](https://www.slantmagazine.com/games/rise-of-the-golden-idol-review); includes hints, but authors discourage using them so as not to "rob yourself of the joy of solving." — [fold.lv](https://www.fold.lv/en/?p=109149) (snippet-level; not fully read)

### Inferences
- Our "2/3 fit" partial count is the same family as Golden Idol's "two or fewer wrong" indicator, but more granular (exact count). Golden Idol's designer reports that even the coarse version measurably lowered difficulty; an exact count on a 3-slot board lets a player brute-force by single-chip swaps. Consider: exact counts only on small local boards (low stakes, tutorial-ish), a coarser signal ("close" / "not yet") on the final board, or a submission budget/cooldown.
- The "Thought Path" rule maps directly to our "one clue = one fact" / "board sentences pose the tension" rules: audit each board for any single chip that short-circuits several reasoning steps.
- "Each scenario serves a macro story beat" = our per-room local solves that feed the final board; Golden Idol validates this micro-to-macro structure.

### Gaps
- Could not read the Thinky Games feature (HTTP 403) or the Draknek & Friends podcast; no Rise-specific designer statements on hint design or the connected "big picture" final scenarios were verified.
- No verified source on how Golden Idol's word bank is populated (words collected from scenes) beyond the general mechanic.

## Return of the Obra Dinn (Lucas Pope)

### Takeaway
Obra Dinn confirms fates only in batches of three correct (identity + fate) entries, which is widely reported to discourage guesswork, though players note it still allows "two sure + guess the third." Pope's own statements focus on structure: deaths placed on two axes (chronology vs. order of discovery), disasters as chapters, and the book as the tool that holds out-of-order events.

### Cited Findings
- Correct fates are validated only in sets of three, meant to discourage guesswork. — [Wikipedia](https://en.wikipedia.org/wiki/Return_of_the_Obra_Dinn)
- Players note the batching still lets you be sure of two and guess the third until accepted; reviewers say you need to be "90% sure" or go in circles. — [Let's Play Archive](https://lparchive.org/Return-of-the-Obra-Dinn); a player in Pope's June 2019 devlog thread complained it was "too easy to brute force many of the fates of the topmen and seamen" (Pope's reply not verified). — [dukope.com TIGSource mirror](https://dukope.com/devlogs/obra-dinn/tig-37/)
- Pope broke the story into "discrete disasters that each wiped out a few characters"; each death placed on two axes, "chronologically in time, and order of player discovery"; the book is "the perfect tool for setting down both the sequence of events" experienced out of order; flashback music themes are "subtle landmarks to help the player orient themselves." — [Game Developer: Road to the IGF](https://www.gamedeveloper.com/business/road-to-the-igf-lucas-pope-s-i-return-of-the-obra-dinn-i-)
- Pacing: "My goal for Obra Dinn was to overwhelm the player initially, then give them time to get comfortable with the core loop," then rely on "their internal compulsions to motivate them into solving all the fates"; "I could focus on the mechanical aspects of clues somewhat separately from how the story is revealed." — [Game Developer: Road to the IGF](https://www.gamedeveloper.com/business/road-to-the-igf-lucas-pope-s-i-return-of-the-obra-dinn-i-)
- Core activity is "filling out a matrix of features for your list of identities," a "rigid core mechanic"; he realized late "the real fun part would be figuring out who they are"; structure was hard because "you can only tell the story when somebody dies," needing "this almost unbroken chain of bodies going back through time." — [Game Developer: appealing design problems](https://www.gamedeveloper.com/design/for-lucas-pope-i-return-of-the-obra-dinn-i-was-a-bunch-of-appealing-design-problems)
- Pope considers lack of approachability one of the game's weakest points; disorienting at the start. — [Gaming Bible interview](https://www.gamingbible.com/features/lucas-pope-papers-please-return-of-the-obra-dinn-and-no-sequels-20220523) (via search summary)

### Inferences
- Batch confirmation decouples feedback from any single answer; it is the strongest anti-guess pattern found. Our boards could adopt "the board only reports progress once N slots are right" or confirm per-board instead of per-slot - but the Golden Idol data shows too-coarse feedback frustrates.
- Two-axis structure (true chronology vs discovery order) is exactly our TIME-clue design: the evening's chronology is reconstructed from rooms visited in a forced/free order. Obra Dinn's book = our Case Notes "The Evening" timeline.
- "Overwhelm then let the loop settle" supports our tutorial-first design and the forced bedroom-before-living-room order.

### Gaps
- No primary Pope statement found explaining the specific "three" threshold.

## Ace Attorney (Shu Takumi, Capcom)

### Takeaway
The contradiction mechanic came from a failed real-time prototype; the fix was to block progress until the player presents the correct contradiction, which "achieved the sense that you're solving the case with your own reasoning." Takumi openly prioritizes entertainment and surprise over strict fair play.

### Cited Findings
- Takumi sought a game where the player discovers lies/contradictions in statements; early prototype required pointing out the contradiction in real time or game over - "no one was able to solve it," it "wasn't interesting," and it was "very easy to read past the contradiction"; the fix: by not letting you proceed until you present the correct contradiction, the game "achieved the sense that you're solving the case with your own reasoning." — [Gyakuten Saiban Library translation, via Nintendo Everything](https://nintendoeverything.com/ace-attorney-creator-on-initial-prototype-and-nearly-suspending-development-maya-and-mias-origins-more/) (fan translation; secondary)
- Takumi: "we have to give the player the feeling they solved it themselves, but at the same time also surprise them at the end"; "Like we let you solve the mysteries, but we also keep things hidden away from you"; asked if the series is fair or unfair, "of course the latter"; "I don't really care much about fair or unfair as long it's entertaining." — [Gyakuten Saiban Library long interview](https://gyakutensaibanlibrary.blogspot.com/2021/07/a-long-interview-with-developers-core.html)
- Pacing: "You'd have episode 1, then 2, and the third episode was to provide a break." Turnabouts get predictable: "there are times where people start to predict those changes because I'm always writing them like that... So you also have to have characters who don't change." Foreshadowing "planned out from the start"; wants replays to reveal missed clues. — [Gyakuten Saiban Library](https://gyakutensaibanlibrary.blogspot.com/2021/07/a-long-interview-with-developers-core.html)

### Inferences
- Our `requiresSolve` confrontation topics are a lighter version of "present evidence to advance": the local solve is the presented proof. A small "present a chip to a character" interaction would be the next step if we want Ace Attorney-style confrontations.
- Takumi's warning about predictable reversals supports keeping some characters straightforward (e.g., Officer Reyes) so not every NPC flips.

### Gaps
- Not verified: penalty bar design, Psyche-Lock/Perceive mechanics, investigation/trial split details from a designer source.

## Her Story / Telling Lies (Sam Barlow)

### Takeaway
A searchable database of ~300 short clips viewed out of order; Barlow deliberately did not nudge players toward search terms, and there is no formal confirm step - the player's understanding is the win state.

### Cited Findings
- Kept interview clips short and built a database the player searches as the mystery unfolds; Barlow did not nudge players to specific words, focusing on layering and hidden secrets. — [MCV: Crime pays](https://mcvuk.com/development-news/crime-pays-the-making-of-her-story/) (via search summary)
- Players cannot access clips in chronological sequence; Barlow described building the "database story" as more like sculpting. — [PC Gamer: The story behind Her Story](https://www.pcgamer.com/the-story-behind-her-story)
- Barlow wasn't always sure the game would even have an ending. — [Game Informer](https://www.gameinformer.com/b/features/archive/2015/07/03/inside-her-story-the-indie-mystery-game-that-people-can-t-stop-analyzing.aspx); "Powerful stories require ambiguity." — [Game Developer](https://gamedeveloper.com/design/-powerful-stories-require-ambiguity-a-story-about-i-her-story-i-)

### Inferences
- Our bracketed-keyword unlocks are a curated, safer version of Her Story's free-text search; the lesson is that player-driven discovery order works if each fragment is self-contained (our "one clue = one fact").
- A no-confirm design suits ambiguous literary mysteries, not a cozy game with a satisfying culprit reveal; we should keep explicit confirmation.

### Gaps
- Telling Lies specifics not researched.

## Overboard! (inkle) - inverted detective

### Takeaway
You play the murderer; an NPC detective "assembles the best case he can against the most likely suspect (usually you)," built as a non-linear "core spine" with a final scene where everyone shares what they know. Short (~20 min) and replay-centric, with a fast-forward mode born from playtests.

### Cited Findings
- Ingold: the detective "assembles the best case he can against the most likely suspect (usually you)"; final scene: "all the characters come together, and share what they know"; "the spine and patching system is primarily for the final scene"; NPC logic "felt a lot like writing a classic game AI decision tree"; "the other characters can't be waiting for you to act the whole time, they've got to be out in front." — [Game Developer: How to get away with murder](https://www.gamedeveloper.com/design/how-get-away-murder-overboard)
- Recorded playtests "informed so much of the story and interface. The one bit of design work we did was the fast forward mode. That arose from playtests." Built in ~3 months. — [PC Gamer](https://www.pcgamer.com/how-the-studio-behind-80-days-made-one-of-the-best-games-of-2021-in-just-3-months)

### Inferences
- "Characters out in front" (NPCs with their own schedules) is relevant to our timeline: characters' testimony should reflect that they lived the evening independently. The gathering final scene is a model for our end scene beats.

### Gaps
- None critical.

## Paradise Killer (Kaizen Game Works)

### Takeaway
Fully open investigation; you can accuse anyone at the end-of-game trial and must make the case, with designers emphasizing player-authored truth - though reviewers note a singular true series of events exists.

### Cited Findings
- You can accuse anyone but must prove the case at trial; it's up to you who's guilty. — [LauraKBuzz](https://laurakbuzz.com/2020/10/02/paradise-killer-is-a-whodunnit-where-anybody-coulda-dunnit/)
- Designer Oli Clarke Smith: the team "never wanted to force our beliefs and our stories on you, make your own one up, there's so much more power in the imagination"; intro was rewritten many times. — [MCV](https://www.mcvuk.com/business-news/we-never-wanted-to-force-our-beliefs-and-our-stories-on-you-make-your-own-one-up-theres-so-much-more-power-in-the-imagination-behind-the-development-of-paradise-killer/)
- Very open-ended, but "there is a singular series of events to piece together." — [Hardcore Gaming 101](https://www.hardcoregaming101.net/paradise-killer/)

### Inferences
- The "trial at the end where you can be wrong" is the opposite of our validated final board; a softer cozy borrow: let the final board accept submission only when correct but let the end scene acknowledge red herrings the player pursued.

### Gaps
- No verified designer quote on trial mechanics/case-strength scoring.

## L.A. Noire (Team Bondi) - cautionary interrogation design

### Takeaway
The Truth/Doubt/Lie interrogation system is a canonical example of label-vs-outcome mismatch: "Doubt" was originally "Force," so the recorded aggressive performances surprised players; the 2017 remaster relabeled to Good Cop / Bad Cop / Accuse.

### Cited Findings
- McNamara: "When we originally wrote the game the questions you asked were coax, force and lie... It was actually force because it was a more aggressive answer." — [Engadget](https://www.engadget.com/2011/11/10/mcnamara-explains-why-la-noires-cole-seemed-like-a-psycho/); [GamesRadar](https://www.gamesradar.com/la-noire-actor-explains-why-the-doubt-tactic-is-so-aggressive)
- Remaster replaced Truth/Doubt/Lie with Good Cop/Bad Cop/Accuse. — [Game Rant](https://gamerant.com/la-noire-different-interrogation-options-tao-115919/)

### Inferences
- Our cat-action prompts ("Stare at him. Do not blink.") describe exactly what the cat does - this avoids L.A. Noire's mismatch. Keep prompts literal so the response never surprises in tone.

### Gaps
- Primary Team Bondi statement on rename not found; reports vary on "last minute."

## Duck Detective: The Secret Salami (Happy Broccoli Games) - cozy animal detective with word-fill deductions

### Takeaway
The nearest tonal analogue: isometric, cozy-noir duck detective whose "deducktions" are phrasal templates filled with words from clues - openly lifted from Golden Idol - with an approximate wrong-count (exact highlight in Story Mode), a hint button, and a fixed solution but chosen ending. Reviews split on whether deductions felt mechanical/trial-and-error.

### Cited Findings
- Deducktions = phrasal templates filled with words from clues; after completing a sentence the game reveals an approximate count of incorrect words; Story Mode highlights incorrect words; hint button; mini-puzzles; solution fixed but player chooses the ending and sees % of players per choice; ~2-3 hours. Director Annika Maar said the mechanic was "stole[n]" from Golden Idol, aiming for a more casual tone; made in six months after a publisher pulled funding; misdirection via an unrelated plot and a red-herring subplot; removed ashtrays and alcohol to keep noir cozy. Reviewers divided: some found deductions relaxed, others unintuitive. — [Wikipedia](https://en.wikipedia.org/wiki/Duck_Detective:_The_Secret_Salami)
- One reviewer: process so mechanical it's "extremely jarring" when a real deduction is asked; another: trial and error felt like "fighting to prove to the game that they had worked things out." — [Adventure Game Hotspot](https://adventuregamehotspot.com/review/1705/duck-detective-the-secret-salami) / [Press Start](https://press-start.com.au/reviews/nintendo-switch/2024/05/24/duck-detective-the-secret-salami-review-hard-boiled-deductions/) (via search summary; attribution between these two not confirmed)
- Sequel: Duck Detective: The Ghost of Glamping. — [gaming.net interview](https://www.gaming.net/da/happy-broccoli-games-on-duck-detective-the-ghost-of-glamping-interview-series/) (not read)

### Inferences
- Duck Detective validates our exact formula commercially (79 Metacritic) and shows the main risk: when most slots are trivially fillable, the occasional real deduction feels jarring, and partial-count feedback invites trial-and-error. Our "no single-option slots" and decoy rules address this; a Story Mode-style accessibility toggle (highlight wrong slots) is a cheap cozy-audience add.
- Removing ashtrays/alcohol to keep noir cozy parallels our "light on noir" intro rule.

### Gaps
- No designer interview text on their feedback tuning.

## Cross-cutting design lessons

### Takeaway
Across designer-sourced material: (1) the player must make the deduction (Takumi: block progress until the player presents the contradiction; Klavins: "make the player feel like a detective through gameplay"); (2) feedback granularity is the central knob trading frustration against brute-force; (3) aggressively remove clues that skip reasoning steps; (4) structure the macro story out of self-contained micro mysteries; (5) playtest silently and often.

### Cited Findings
- GMTK's 2017 "What Makes a Good Detective Game?" asks why detective games fail to make players into Sherlock Holmes; 2023 follow-up "The 3 Types of Detective Game" revisits after the genre's boom. — [TheTVDB listing](https://thetvdb.com/series/game-makers-toolkit/episodes/6359251) (description only; content not verified)
- Feedback-granularity evidence: Golden Idol (per-slot and all-or-nothing both frustrated; "two or fewer wrong" eased but shortened) — [Game Developer](https://www.gamedeveloper.com/design/case-of-the-golden-idol); Obra Dinn (batches of three) — [Wikipedia](https://en.wikipedia.org/wiki/Return_of_the_Obra_Dinn); Duck Detective (approximate count; exact in Story Mode) — [Wikipedia](https://en.wikipedia.org/wiki/Duck_Detective:_The_Secret_Salami)
- Fair-play vs surprise: Takumi prioritizes the player feeling they solved it plus a final surprise over strict fairness. — [Gyakuten Saiban Library](https://gyakutensaibanlibrary.blogspot.com/2021/07/a-long-interview-with-developers-core.html)

### Inferences
- Recommended for Cat Detective: keep "N/3 fit" on local boards but add friction (e.g., brief cooldown, or require changing at least one chip between submits - already implicit) and consider a coarser or batched signal on the final 7-slot board, since an exact count on 7 slots enables chip-by-chip search.
- Golden Idol's "stuck 30 minutes on a technicality" is a test metric we can apply: if playtesters state the culprit but fail the board, the board's wording, not the mystery, is the problem.
- An optional accessible mode (highlight wrong slots, as Duck Detective's Story Mode) fits the cozy audience without changing the default challenge.
- Culprit reveal: Takumi's "solved it yourself + surprise" suggests the end scene should add one new emotional beat (e.g., the rudebeak reveal) the player did not deduce, rather than only replaying the deduction.

### Gaps
- Not researched with sources this round (from memory, unverified - do not cite as fact): Disco Elysium (Thought Cabinet, skill-check interrogations, the Tribunal); Danganronpa (Nonstop Debate "truth bullets", Closing Argument comic reconstruction - a direct analogue to our final timeline board); Sherlock Holmes Crimes and Punishments / Chapter One (deduction board linking clues into conclusions, can reach wrong conclusions, moral absolve/condemn choice); Shadows of Doubt (procedural sim, no authored solution, submit-a-case form); Murder by Numbers (picross + visual-novel mystery); Strange Horticulture, Lorelei, Type Help, Botany Manor (cozy/thinky deduction and identification structures). A follow-up pass should source these, especially Danganronpa's Closing Argument and Frogwares' deduction board.
- GMTK video content itself was not transcribed.
