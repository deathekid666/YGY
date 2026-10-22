# AR Yu-Gi-Oh Prototype — Project Scaffold

## The easier setup path (recommended — no account, no card photography)

Recognition works by sending the current **live phone camera frame** to a
vision-capable AI model and asking "which card is this?" — instead of
pre-registering all 10 cards as Vuforia Image Targets beforehand. This means:

- **No Vuforia account, no license key, no image-target database to build**
- **No need to photograph your cards ahead of time**
- Just point your phone's camera at a card and tap the matching "Scan"
  button — `CameraFeedBackground` shows the live camera feed full-screen so
  you can see what you're aiming at

The trade-off is honest, not hidden: each scan needs a network call (a
second or two of latency) and costs a small amount per call, and it can
misread a card in bad lighting. See "Recognition: two options" below for
the full comparison before you commit either way.

There's no real-world AR tracking in this scaffold (no ARFoundation plane
anchoring, no marker tracking) — the 11-per-side zones exist as game-state
labels, not as things physically overlaid on your table through the
camera. The input model is simple instead: point the phone at one physical
card, tap the button for whichever zone that card is sitting in. That's
the whole interaction.

### Setup

1. Install **Unity Hub** + Unity **2022 LTS**.
2. Create a new **3D (Core)** project.
3. Copy every file under `Assets/Scripts/` and `Assets/Editor/` from this
   scaffold into your project at the same paths.
4. In the Unity menu bar, run **YugiohAR → Auto-Build Scene**.
5. Select the `CardVisionScanner` GameObject in the Hierarchy and paste
   your **Anthropic API key** into its `apiKey` field.
6. Press Play (in-Editor testing uses your webcam if you have one; the real
   experience is on a phone — see the Android build steps below).
7. Point the camera at a physical card, and tap the matching
   `Scan Player Monster 1` (etc.) button on the left side of the screen
   (the right side holds the Opponent's buttons — see "Two-player fields"
   below).

That's it — no external account setup, no image database to build.

### Building the actual Android APK

This part is genuinely Unity's own build pipeline — nothing a script can
trigger for you. Once the steps above work in the Editor:

1. In Unity Hub, make sure your install has the **Android Build Support**
   module (plus its Android SDK/NDK/OpenJDK sub-items) — add it via
   Unity Hub → Installs → your version → gear icon → Add Modules if it's
   missing.
2. **File → Build Settings → Android → Switch Platform.**
3. **Edit → Project Settings → Player** (Android tab):
   - Set a unique **Package Name** (e.g. `com.yourname.yugiohar`).
   - Set **Minimum API Level** to something reasonably current (API 26+ is
     a safe floor for `WebCamTexture` + networking).
   - Under **Other Settings → Configuration**, confirm **Internet Access**
     is set to **Require** (needed for the API calls).
4. Connect your Android phone via USB with **Developer Options** and
   **USB Debugging** enabled (Settings → About Phone → tap Build Number 7
   times, then Settings → Developer Options).
5. **File → Build Settings → Build And Run** — Unity compiles the APK,
   installs it to your connected phone, and launches it.
6. On first launch, Android will prompt for **Camera permission** — this
   is `CameraFeedBackground` requesting it at runtime; accept it.

If you'd rather just get a `.apk` file to install manually (no cable),
use **Build** instead of **Build And Run**, then transfer the resulting
`.apk` to your phone and open it (you'll need to allow "install from
unknown sources" for whichever app you use to open it).

iOS is possible too but needs a Mac, Xcode, and an Apple Developer account
to sign the build — a bigger lift than Android for a first test.

### What Auto-Build Scene does for you

The `SceneAutoSetup.cs` editor script (in `Assets/Editor/`) programmatically
creates and wires:
- `GameFieldManager`, `DeckState`, `GraveyardState`, `MonsterFieldState`,
  `TargetSelectionManager`, `CardEffectRouter`, `LifePointsState`,
  `TurnManager`, `CardVisionScanner` — all correctly cross-referenced
- `CameraFeedBackground` — a full-screen `RawImage` showing the phone's
  live rear camera, with runtime Camera-permission requesting on Android
- `FieldZoneManager` plus **22 zone GameObjects — a full field for each
  player** (5 Monster, 5 Spell/Trap, 1 Graveyard each), laid out as two
  facing mats you can reposition to match your real setup
- A UI Canvas with the `TrapIdentityPanel`, `TargetSelectPanel`, a
  `Trigger Traps (test)` button, a `Next Phase` button, a
  `Declare Attack (Battle)` panel, a live life-points display, **and one
  `Scan` button per zone, split into a Player column and an Opponent
  column**

It's idempotent — re-running it won't duplicate anything that already
exists by name.

## Optional: real AR overlay via AR Foundation

Everything above uses a flat live camera background (`CameraFeedBackground`)
— good enough to point-and-scan, but it doesn't overlay the field grid onto
your actual table the way the reference video did. For that, add AR
Foundation:

1. **Window → Package Manager** → install **AR Foundation**, plus
   **ARCore XR Plugin** (Android) or **ARKit XR Plugin** (iOS) for
   whichever platform you're targeting.
2. **Edit → Project Settings → Player → Other Settings → Scripting Define
   Symbols** → add `AR_FOUNDATION_ENABLED`.
3. Re-run **YugiohAR → Auto-Build Scene**. With that symbol present, it now
   builds an `AR Session` + `AR Session Origin` (AR camera, plane detection,
   raycasting) instead of the flat `CameraFeedBackground`, and adds
   `ARFieldPlacer` — tap any detected flat surface and the entire 22-zone
   field snaps onto it at that real-world position and orientation.
4. Every zone now also has a visible **colored outline** (teal for Monster
   zones, orange for Spell/Trap, a faint white for Graveyard) so the grid
   actually reads as an overlay on your table, matching the look from the
   earlier mockups — not just invisible trigger volumes.
5. `CardVisionScanner` automatically switches its capture method too —
   with AR Foundation active it renders straight from the AR camera
   (which includes the real passthrough video), no code changes needed.

**Known rough edges, worth knowing before you rely on this:**
- Plane detection needs a reasonably textured, well-lit flat surface and an
  ARCore-capable (or ARKit-capable) device — it can take a few seconds of
  moving the phone around before a plane is found.
- The AR Camera needs a **pose driver** to actually move with the device.
  Which component that is (legacy `TrackedPoseDriver` vs. the Input
  System package's version) depends on your project's **Active Input
  Handling** setting (Project Settings → Player) — `SceneAutoSetup`
  deliberately doesn't guess this, since guessing wrong breaks the build.
  Add whichever one matches to the `AR Camera` GameObject.
- AR Foundation's API has shifted between versions (`ARSessionOrigin` vs.
  the newer `XROrigin`) — this scaffold targets the more broadly
  compatible `ARSessionOrigin` API. If your installed AR Foundation
  version has fully removed it, follow AR Foundation's own upgrade guide
  for the renamed equivalents.
- The field is placed once per session, at a fixed real-world pose. There's
  a `ResetPlacement()` method on `ARFieldPlacer` if you want to wire a
  "reposition" button.

This is genuinely more fragile than the default WebCamTexture path — start
with the default, and only add this once the rest of the game is working
the way you want.

## Two-player fields

Every zone belongs to exactly one player — `FieldZone.owner` — and that's
the *entire* mechanism for "whose card is this." A monster scanned into a
zone owned by `PlayerSide.Opponent` simply is the opponent's monster; no
separate bookkeeping needed anywhere else.

This drives real two-player behavior throughout:
- **`TurnManager.DeclareAttack`** rejects an attack if the attacker isn't
  in a zone owned by the current turn player, or the defender isn't in a
  zone owned by the opponent.
- **Mirror Force / Trap Hole** always resolve against the trap's own
  *opponent* — derived from the trap's zone owner, never hardcoded.
- **`DeckState` and `GraveyardState`** are per-side: each player has their
  own deck/hand counters and their own graveyard. Monster Reborn can
  target either graveyard, but always revives onto the caster's own side.
- **`BattleDeclareUI`** (the `Declare Attack (Battle)` panel) only offers
  the current turn player's monsters as attackers and the opponent's as
  defenders, refreshing automatically when the Battle phase begins.

What this does NOT do: enforce that only the current turn player can
Normal Summon (any zone can be scanned into any time) — that's a
deliberate simplification, tracked under "still a placeholder" below,
since hand/cost/tribute rules are a bigger, separate piece of scope.

## Recognition: two options

| | Vision-API scan (default, easier) | Vuforia Image Targets (legacy/offline) |
|---|---|---|
| Setup | None — just an API key | Account + license key + photograph & upload all 10 cards |
| Works offline | No, needs network | Yes, after initial setup |
| Card coverage | Any card, zero prep | Only the ones you registered |
| Speed | ~1-2s latency per scan | Instant, every frame |
| Cost | Small per-call API cost | Free (Vuforia free tier) |
| Reliability | Depends on lighting/angle | Very high once registered |
| Trigger | Player taps a "Scan" button | Automatic the instant the camera sees the card |

**Start with the vision-API path** — it gets you playing today with zero
external setup. If you outgrow it (want instant recognition with no
per-scan cost or delay, or want it to work with no signal), the Vuforia
scripts are still included and documented further down — nothing about
the game logic changes, only how a card gets identified.

### How the vision-API scan flow works

`CardVisionScanner.ScanZone(zoneName)` — wired to each `Scan` button by
`SceneAutoSetup` — does the following:

1. Waits for the end of the current frame, then captures a screenshot.
2. Crops it around the on-screen position of the given zone (so it's
   scanning the right physical spot, not the whole table).
3. Sends the crop to the Anthropic API with a prompt listing all 10 valid
   card ids, plus `card_back` and `empty` as possible answers.
4. Parses the model's one-word reply and calls the *exact same*
   `GameFieldManager` methods (`OnCardRecognized`, `OnFaceDownCardPlaced`)
   that Vuforia's `CardTargetHandler` used to call. Nothing downstream —
   zones, summon FX, effects, battle — cares which recognition method fired
   it.

### Cost and reliability notes

- Keep the physical card well-lit and reasonably centered in frame when you
  tap Scan — the model sees the whole live frame, not a cropped region, so
  a cluttered background or multiple visible cards can confuse it.
- For a real (non-prototype) build, **do not ship your API key inside the
  app** — anyone can extract a hardcoded key from a compiled build and rack
  up charges on your account. Route the request through your own small
  backend instead, and have the app call that backend without the key ever
  touching the device. The prototype embeds the key directly for
  simplicity only.
- A misread (wrong card, or "empty" when a card is present) is possible.
  Consider adding a manual "not right — rescan" button if this bothers you
  in testing.

## Test everything for free before paying for a single API call

The API key only matters the moment you tap a real `Scan` button. Everything
else — summons, effects, traps, turns, battles, life points — is testable
right now with **zero cost**, using a debug panel built automatically by
Auto-Build Scene.

`DebugRecognitionUI` gives you a small on-screen panel with two
`< Label >` cyclers (pick a zone, pick a card) and three buttons:

- **Simulate scan** — acts exactly like a real successful `CardVisionScanner`
  scan of the selected card in the selected zone.
- **Simulate set (face-down)** — acts like scanning a `card_back`, the same
  as setting a trap or a flip-effect monster face-down.
- **Simulate remove** — acts like the camera losing sight of whatever's in
  that zone (picking the card up).

All three call the *exact same* `GameFieldManager` methods the real
`CardVisionScanner` calls — so anything that works here behaves identically
once you plug in a real API key. Build the whole game, test every card,
every trap, every battle, entirely offline — and only add billing once
you're happy with how it all plays.

## How the scripts fit together

- **CardDatabase.cs** — static data for the 10 starter cards (id, name,
  effect category, ATK/DEF, and level — level drives the tribute-summon
  rule via `CardDatabase.TributesRequired(level)`).
- **CameraFeedBackground.cs** — default (non-AR) path: shows the phone's
  live rear camera full-screen via a `RawImage`, requests Camera permission
  at runtime on Android, and exposes the live `WebCamTexture` frame that
  `CardVisionScanner` captures for each scan. Without this (or AR
  Foundation), there's nothing real for the app to point at.
- **ARFieldPlacer.cs** *(compiles only with `AR_FOUNDATION_ENABLED`)* —
  the real AR overlay path: plane detection + tap-to-place, anchoring the
  whole field to a real-world surface. See "Optional: real AR overlay"
  above.
- **CardVisionScanner.cs** — the default recognition path: grabs the
  current frame from `CameraFeedBackground` (or, in AR mode, renders
  straight from the AR camera), asks a vision model what card it is,
  dispatches into `GameFieldManager` (see above).
- **ZoneScanButton.cs** — thin UI wrapper, one per zone, calls
  `CardVisionScanner.ScanZone(zoneName)` when tapped.
- **DebugRecognitionUI.cs** / **OptionCycler.cs** — free, offline stand-in
  for `CardVisionScanner`. Lets you simulate any scan result (a specific
  card, a card-back, or a card being removed) via on-screen `< Label >`
  cyclers instead of a real camera scan — see "Test everything for free"
  above.
- **BattleDeclareUI.cs** — the two-player-aware "Declare Attack (Battle)"
  panel: cyclers show only the current turn player's monsters as possible
  attackers and only the opponent's as possible defenders, refreshed
  automatically each Battle phase.
- **FieldZoneManager.cs** / **FieldZone.cs** — defines Monster / Spell-Trap
  / Graveyard zones as trigger volumes, each tagged with an owning
  `PlayerSide`, and answers "which zone is this position in, and whose is
  it?"
- **GameFieldManager.cs** — the central brain. Tracks active cards, tracks
  which zones currently hold an unidentified face-down card, and tells
  `CardEffectRouter` whether a given recognition is a fresh placement or a
  genuine flip.
- **CardEffectRouter.cs** — routes each card to behavior by
  `EffectCategory`, not by name:
  - **NormalSummon** → checks `TurnManager.CanNormalSummon` (your Main
    Phase, once per turn); level 5+ monsters then prompt for the correct
    number of tributes (destroyed via `MonsterFieldState`) before the
    particle + sound burst plays and the monster actually joins the board
    (Step 5 / Step 14)
  - **FlipEffect** → only fires when `wasFlip == true`. Man-Eater Bug
    specifically asks the player to pick another monster **by zone** (so
    duplicate cards don't collide) and destroys it via `MonsterFieldState`
    (Step 6)
  - **SpellTarget** → Monster Reborn pulls valid targets straight from
    `GraveyardState`, and revives the chosen one into the first empty
    Monster zone via `MonsterFieldState` (Step 7)
  - **SpellSimple** → resolves immediately; Pot of Greed calls
    `DeckState.Draw(2)` (Step 7)
  - **TrapSetTrigger** → armed via `ArmKnownTrap()`, fires on
    `GameEvents.DeclareAttack()`. Mirror Force destroys every monster on
    the field; Trap Hole destroys whichever monster was most recently
    summoned — both via `MonsterFieldState` (Step 8)
- **MonsterFieldState.cs** — the real board state: which card id (and
  Attack/Defense position) sits in which Monster zone. Destroy/revive
  effects all go through this rather than guessing from the camera, since
  a "destroyed" card's physical copy is still sitting on the table.
- **GraveyardState.cs** — list of card ids that have been
  destroyed/discarded; Monster Reborn reads from this to know what's legal
  to target.
- **DeckState.cs** — minimal deck/hand counters so Pot of Greed has
  something to affect.
- **TargetSelectionManager.cs** / **TargetSelectionUI.cs** — request/confirm
  flow for "player must pick something," with a working uGUI panel.
  `ShowFor` labels options by card id (used where the target genuinely is a
  card, like Monster Reborn's graveyard); `ShowForCustom` takes explicit
  labels (used for zone-based targets like tributes and Man-Eater Bug, so
  duplicate cards in different zones stay distinguishable).
- **TrapIdentityPromptUI.cs** — pops up automatically when a card is set
  face-down in a Spell/Trap zone, since a trap can't be identified by the
  camera (see "A note on traps" below).
- **GameEvents.cs** — global event bus. `TurnManager.DeclareAttack()` fires
  this before resolving battle damage, so any armed trap gets a chance to
  respond first.
- **LifePointsState.cs** — tracks both players' life points (8000 start),
  raises events on change and on game-over.
- **TurnManager.cs** — real turn structure: Draw → Standby → Main1 →
  Battle → Main2 → End, cycling between players. `DeclareAttack()` resolves
  actual ATK-vs-ATK/DEF battle math (Step 10). `CanNormalSummon()` /
  `MarkNormalSummonUsed()` enforce the one-Normal-Summon-per-turn rule,
  reset automatically each side's Draw phase (Step 14).
- **LifePointsUI.cs** / **NextPhaseButton.cs** / **AttackDeclareButton.cs**
  — UI bindings for the above.
- **SceneAutoSetup.cs** *(Editor-only, in `Assets/Editor/`)* — the
  one-click scene builder described above.

### Legacy/offline option: Vuforia Image Targets

If you later want instant, offline, no-cost recognition instead, the
original Vuforia-based scripts are still in this scaffold:

- **CardTargetHandler.cs** — goes on a Vuforia Image Target per card front.
- **FaceDownMarkerHandler.cs** — goes on a Vuforia Image Target trained on
  the plain card back, for detecting face-down sets/flips.

Both call the exact same `GameFieldManager.OnCardRecognized` /
`OnFaceDownCardPlaced` methods `CardVisionScanner` does — so you can swap
recognition methods without touching any game logic. To use this path
instead: install the Vuforia Engine AR package, get a free license key,
photograph and upload your 10 card fronts + 1 card back to Vuforia's
Target Manager, then attach these two scripts to the resulting Image
Targets as described in Vuforia's own documentation. You'd disable/remove
the `CardVisionScanner` + scan buttons in that case, since both paths
feeding the same zone at once would conflict.

## A note on traps specifically

A trap **stays face-down** even when triggered — a photo of its back looks
identical to every other trap's back, so neither recognition method can
know *which* trap it is just by looking. The flow is:

1. Player sets a card face-down in a Spell/Trap zone (scanned as
   `card_back`) → `HandleFaceDownSet()` fires → the `TrapIdentityPanel`
   pops up automatically.
2. Player taps which trap they set → `ArmKnownTrap()` is called for you.
3. When `Declare Attack` is pressed, every armed trap resolves via
   `FireTrap()`.

## Build order — all 14 steps are scaffolded

1. ✅ Card recognition — vision-API scan of the **real live camera feed**
   by default (Vuforia available as an offline alternative), plus a free
   `DebugRecognitionUI` stand-in so you can test everything before paying
   for a single scan
2. ✅ All 10 cards defined in `CardDatabase`, with real ATK/DEF stats
3. ✅ `GameFieldManager` logs recognized cards
4. ✅ `FieldZoneManager` — zone-aware recognition (monster only summons in
   Monster zone)
5. ✅ Real particle + sound summon FX (assign your own assets in the
   Inspector)
6. ✅ Flip-effect detection via the `card_back` → specific-card transition
7. ✅ Spell targeting (`TargetSelectionManager`) + simple resolution
   (`DeckState`)
8. ✅ Trap set → arm → trigger flow via `GameEvents` + `ArmKnownTrap`
9. ✅ Scene assembly automated via `SceneAutoSetup.cs` (Auto-Build Scene
   menu command)
10. ✅ Real turn structure and battle resolution via `TurnManager` +
    `LifePointsState`
11. ✅ Full two-player fields — every zone owned by a specific `PlayerSide`,
    driving attack validation, trap targeting, and per-side decks/graveyards
    (see "Two-player fields" above)
12. ✅ Live phone camera feed (`CameraFeedBackground`) wired as the actual
    thing the app points at and scans — an earlier version of this scaffold
    was scanning an empty 3D scene instead of the real camera; fixed now
13. ✅ Optional real AR overlay via `ARFieldPlacer` — plane detection,
    tap-to-place, and visible colored zone outlines, once AR Foundation
    packages are installed (see "Optional: real AR overlay" above)
14. ✅ Summon legality and tribute cost — one Normal Summon per turn, during
    your own Main Phase, with level 5+ monsters requiring 1 or 2 tributes
    (destroyed via `MonsterFieldState`) before they join the board

## What's still a placeholder / your next real work

- All FX use generic particle/sound assets you supply — nothing
  card-specific yet (e.g. Blue-Eyes doesn't look different from Dark
  Magician on summon). Assign `summonParticlesPrefab` / `summonSound` etc.
  on `CardEffectRouter` in the Inspector.
- The zone layout Auto-Build Scene creates is a placeholder grid —
  reposition the 11 zone GameObjects' Transforms to match your actual
  physical playmat.
- Two physical mats are assumed (Player's and Opponent's), matching the 22
  auto-built zones. If you'd rather share one physical table/mat between
  two people, you'll want to reposition the Opponent zones to sit on the
  same physical squares as the Player zones (facing the other direction)
  rather than the default side-by-side placeholder layout.
- Monster Reborn and destroy effects update **game state** correctly but
  can't move the *physical* card for the player — reviving a monster logs
  which zone it landed in and expects the player to place the real card
  there next.
- ~~Tribute selection matches candidates by card id, not by specific zone~~
  — **fixed.** Field targeting (tributes, Man-Eater Bug) now selects by
  **zone**, not card id, via `TargetSelectionUI.ShowForCustom`. Two
  identical cards in different zones now show as distinct, correctly
  labeled options instead of colliding into one. (Monster Reborn's
  graveyard targeting still matches by card id, which is fine there —
  a destroyed card in the graveyard has no zone to distinguish it by, and
  any physically-identical copy is interchangeable.)
- There's still no true "hand" of undrawn cards — `DeckState` tracks a
  hand *count* per side (for Pot of Greed's draw-2 to have something to
  affect) but not which specific cards are in it. Since the physical cards
  are your actual hand in real life, the app was never going to police
  which cards you're "allowed" to hold — what it now does enforce is the
  *summon-legality* rules (Main Phase, once per turn, tribute cost), which
  is the part a physical playgroup actually needs a referee for.
- The scan flow is manual (tap a button) rather than continuous — good
  enough for turn-based play, but you may want a "auto-rescan every N
  seconds" option later for a more magical feel, at the cost of more API
  calls.

## Copyright note (real, not hypothetical)

If you use actual Yu-Gi-Oh card art or 3D character likenesses (Konami's
IP), keep it to private/personal use. Publishing or shipping that art —
even in a free hobby app — is a real copyright exposure. If you want to
share or publish this project eventually, plan on either original monster
designs or abstract/particle-only summon effects instead of reproduced
card art.
