# Ashford Hill — how it works, and how to talk about it

A companion to `README.md`, written for standing next to the machine and explaining the thing.
Plain English first, code second. Every claim below was checked against the code.

---

## 1. The one-paragraph pitch

Ashford Hill is a short film — about a minute and a half — made inside Unity. A steam train
arrives at a country railway station at dusk, waits while another train clears the line ahead,
picks up its passengers, and pulls away into the fog. There are no downloaded models, no
photographs used as textures, and no sound files. Every object in it is assembled out of Unity's
six built-in shapes (cube, sphere, cylinder, capsule, plane, quad); every colour and surface is
defined in code; and every sound — the whistle, the exhaust beats, the brake squeal, the door
slams, the station room tone — is generated as raw numbers while the film is running. The film is
not a game: nothing is interactive. It is a piece of camerawork that plays itself, and it can be
rendered to an MP4 from a single command with no one touching the editor.

---

## 2. The big idea: the scenes are output, not source

This is the one decision that explains almost everything else, so it is worth being able to state
it cleanly.

In a normal Unity project you build the world **by hand**: you drag objects into the scene window,
nudge them into place, and Unity saves the result into a `.unity` file. That file *is* the work.
Lose it and you have lost the level.

Here it is the other way round. The `.unity` files are written by C# programs that run inside the
editor (`Assets/Scripts/Editor/`). You press **Tools ▸ Train Station ▸ Build All Scenes**
(`Ctrl+Shift+T`), `BuildAll.Run()` executes, and all five scene files are deleted and rewritten
from scratch in a couple of seconds. If you want the platform two metres wider, you do not drag
the platform — you change one number in `StationKit.cs` and rebuild.

**What that buys:**

| Benefit | Why it follows |
|---|---|
| The four scenes can never drift apart | There is one station, described once in `StationKit.BuildWorld`, and all four scenes call it. There is no way for the lamps to be in one place in scene 1 and somewhere else in scene 3, because nobody ever placed them twice. |
| The whole film is reproducible | Delete every scene file and the entire film comes back from the code. Nothing is trapped inside an opaque binary. |
| Everything is reviewable as text | The real source is ~4,300 lines of commented C#. A change shows up in version control as a readable diff — `sun.intensity 1.25 → 1.05` — rather than as three thousand changed lines of scene YAML. |
| Every number has a reason | Because positions are written as expressions rather than dragged, they can be *derived*. The most important example: where the train starts is computed from how hard it brakes (see §4, `TrainMotion`). |

**What it costs:**

- You cannot nudge anything by eye. Every adjustment is edit-code, rebuild, look.
- The feedback loop is slower than dragging a box, which is why the project grew two checking
  tools (`SceneSnapshot` and `Playtest`) to replace "just look at it in the editor".
- Any change made by hand in the scene window is destroyed by the next build. The README says it
  in capitals for a reason: **do not hand-edit the scenes.**

The build order in `BuildAll.Run()` even has a small courtesy in it: the four film scenes are
built first and `00_Bootstrap` last, so that when the build finishes the editor is left sitting on
the scene you actually want to press Play from.

---

## 3. The shape of the project in three sentences

- **Build time** (editor scripts): generate materials, generate five scene files, register them in
  Unity's Build Settings, and set project-wide options like colour space.
- **Run time** (runtime scripts): a flow manager loads the four scenes in order behind a fade; in
  each scene a train moves itself, its wheels and steam and sound follow from its speed, passengers
  walk and board, a camera watches lazily.
- **Capture** (both): either render stills to check framing, run the film headlessly to prove it
  does not crash, or render it to a JPEG sequence plus a WAV that `ffmpeg` muxes into an MP4.

---

## 4. Script by script

There are 22 C# files (11 that run in the finished film, 11 that only run inside the editor).
They are covered here in reading order — the film first, then the machinery that builds it.

Two pieces of Unity vocabulary used throughout, defined once:

- **Component** — a script attached to an object in the scene; Unity calls its `Awake` once at the
  start and its `Update` once per frame.
- **Material** — the description of a surface (colour, how metallic, how shiny). "Emissive" means
  the surface gives off light of its own, which is how the lit windows and signal lenses work.

### The film runs itself

#### `SceneFlowManager.cs` — the projectionist

Lives in `00_Bootstrap`, the only scene with no scenery in it. It loads `01_Arrival`,
`02_Waiting`, `03_Boarding` and `04_Departure` one after another with fixed durations
(25 s, 18 s, 28 s, 24 s), fading to black in between. It marks itself `DontDestroyOnLoad`, meaning
Unity does not destroy it when a new scene loads, so it is the one object alive for the whole
running time. The sequence is written as a **coroutine** — a method that can pause itself, wait a
number of seconds, and carry on where it left off on a later frame.

**The one idea:** *the fade is not decoration, it is engineering.* Loading a scene takes an
unpredictable moment. If you cut straight, the audience sees a stutter. If you cut on black, the
same hitch reads as a deliberate edit. The comment says exactly this. There is a second detail in
the same spirit: after the load completes it waits **one extra frame** before fading up, so the new
scene's `Awake`/`Start` code has run and the first visible frame does not show objects at their
default, un-posed positions.

#### `CameraDolly.cs` — the person holding the camera

Aims the camera at a target with **damping** (it turns towards where it should be pointing a little
each frame instead of snapping there), drifts the camera position slowly, and adds a small
wobble from Perlin noise — a smooth random signal, so the shake looks like a hand rather than a
vibration.

**The one idea:** *the lag is the point.* A camera welded to its subject reads as a video game.
One that turns a beat late lets the train pull ahead of frame, which is what a person standing on
a platform actually produces. Two supporting details worth knowing: it aims correctly on frame one
in `Awake` (otherwise the damping would spend the first second of every shot swinging round from
whatever rotation the builder left behind), and `releaseDistance` lets the operator "give up" —
in the departure scene, past 320 m the camera stops following and lets the train become a dot in
the fog rather than creeping round after it.

#### `SignalLight.cs` — the reason for scene 2

A two-aspect colour-light signal. Red while our train is held, then it fades over to green at a
set time. The lens brightness is driven through a **MaterialPropertyBlock** — a way of overriding
one renderer's colour without editing the shared material.

**The one idea:** *scene 2 needs a reason to exist, and the signal is it.* Without it the middle
scene is just a pause. With it, the audience is told the line ahead is blocked, watches the other
train clear, sees the light come off, and understands why the wait ended. The property block is
there so two signals in the same scene do not share one mutated material and change aspect
together.

### The train

#### `TrainMotion.cs` — the source of truth

Moves the whole train along one axis. Three phases: **Arrive** (rolls in braking and stops),
**Hold** (stands still), **Depart** (waits out a dwell, then accelerates away on a curve that is
deliberately flat at the start so it reads as a heavy train). It also drives the chimney steam:
effort peaks early in the pull-away and eases off once she is rolling, because a steam engine works
hardest getting a train moving, not at line speed.

**The one idea:** *speed is the single source of truth.* Nothing in this project is animated
independently. Wheel rotation, exhaust rhythm, steam volume and Doppler pitch are all computed
from the one `Speed` value, which means they cannot drift out of sync with each other — there is
no second clock to disagree with the first.

The second idea, and the best thing in the project to be asked about: **`ArrivalDistance`.**
The braking curve is `speed = entrySpeed × (1 − u)²` where `u` runs 0→1 across the braking time.
The exact distance that curve covers is its integral, which works out to `entrySpeed × arriveTime / 3`.
In `01_Arrival` that is 16 × 13.5 ÷ 3 = **72 m**, so the builder places the nose 72 m back from the
stop mark at x = 26, i.e. at x = −46. Change the braking curve or the entry speed and the stop
stays correct, because the start position is derived from them rather than guessed.

#### `TrainWheel.cs` — 27 lines, one bug class removed

One wheel that stores its own radius and converts metres travelled into degrees of rotation.

**The one idea:** *a shared rotation rate would be wrong.* The driving wheels are 0.95 m radius
and the carriage and tender wheels are 0.55 m. Rolling them all at the same rate would leave half
the train visibly skating along the rail. Because each wheel does its own arithmetic from the same
distance, they are all correct at once and there is no way to get it wrong later.

#### `TrainAudio.cs` — the rhythm section

Turns `TrainMotion`'s numbers into sound: exhaust beats, a whistle before departure, brake squeal
on the way in, a safety-valve hiss when standing, and station room tone. It keeps a small pool of
six audio players for the chuffs so a fast beat does not cut off the tail of the one before it.

**The one idea:** *chuffs are scheduled by distance, not by a timer.* A two-cylinder steam
locomotive fires four exhaust beats per revolution of its driving wheels, so the code emits one
beat every quarter-circumference of travel. The rhythm therefore accelerates with the train for
free, and can never fall out of step with the wheels the audience is watching. Note the small,
telling detail: it uses a `while` loop rather than an `if`, because at line speed more than one
beat can fall inside a single frame and an `if` would silently thin the rhythm out exactly when it
should be densest.

Everything else in the file is character: beats get shorter and brighter as she picks up
(`pitch` 0.82 → 1.25) but *quieter*, because the blast is heaviest when the engine is fighting the
load; the safety valve is loudest standing still and gone once she is working; the room tone is
deliberately non-directional so it does not swing around the listener's head when the camera turns.

#### `ProceduralAudio.cs` — every sound in the film

A static library that generates seven `AudioClip`s as arrays of numbers, 44,100 of them per second,
at the moment the film starts. Nothing is loaded from disk.

**The one idea:** *this is what makes "no downloaded assets" literally true, and it works because
the real sounds are simple.* A steam whistle really is a few slightly out-of-tune tones plus
breath, so the code sums four sine partials (523.3, 659.3, 784.0, 1046.5 Hz), detunes them so they
beat against one another, wobbles them with vibrato, and adds filtered noise underneath. A chuff
really is a burst of noise with a near-instant attack and a long tail, band-passed through a filter
that sweeps downwards as the blast loses pressure. A brake squeal is noise pushed through **two**
stacked resonant filters — the comment notes that one filter alone just sounds like noise; it takes
two for it to ring. A station chime is a falling fourth, with each bell built from a fundamental
plus a quiet, slightly sharp overtone, which is the difference between a bell and a beep.

The shared building block is `Svf`, a state-variable filter: five lines of arithmetic that yield a
low-pass and a band-pass from the same two running values.

#### `CarriageDoor.cs` — one hinged door

Swings a door out over the platform and back. Opening is unhurried (90°/s), slamming is not
(420°/s).

**The one idea:** *without it, the boarding scene is people walking into a painted wall.* The
comment is blunt about this: a passenger disappearing into a closed door rather undermines the one
scene whose whole subject is people getting on a train. The doors also give that scene an ending —
slammed one at a time down the train is the sound of a train about to leave.

### The people

#### `PassengerWalker.cs` — a walk made of sine waves

Walks a blocky figure along a list of waypoints and puts them on the train, then shrinks them away
into the doorway.

**The one idea:** *one stride phase drives everything.* Legs, arms, knee bend, hip bob and body
lean are all sine waves off a single number, and that number advances with **distance covered**
rather than with time — so the feet stay planted whatever speed the figure walks at, and there is
no skating. Three further touches that earn their place: the knee only bends on the swing-through
(when the leg is travelling forwards) and stays near straight while the foot carries weight; a
`weight` parameter blends the swing out so a stopping figure settles instead of snapping to
attention; and standing passengers get a small sway and an occasional glance down the line, because
a perfectly still figure reads as a prop.

Boarding is a trick worth naming: the figure steps up 0.22 m, walks 1.15 m forward, and scales
down to 70% as it goes. The shrink sells passing through a doorway into a dark interior **without
there being an interior to walk into**.

#### `BoardingDirector.cs` — the stage manager

Runs scene 3 on a clock: chime at 1.2 s, doors open at 0.4 s, door slams start at 20.5 s and work
down the train, guard's whistle at 25.5 s. It also plays a slam when a passenger vanishes inside —
but only 75% of the time, and never twice within 0.4 s, because not everybody shuts a door behind
them and two bangs at once read as a glitch rather than a busy platform.

**The one idea:** *it exists because a delegate cannot be saved into a scene file.*
`PassengerWalker` announces boarding through a C# callback (`onBoarded`). Unity can serialise
numbers and object references into a `.unity` file, but not a function reference, so the build
script cannot wire that connection at build time. Something has to connect the passengers to the
audio at runtime, and this is that something.

### The world builders (editor only)

#### `Prim.cs` — the vocabulary

Helper functions every other builder is written in: make a material, spawn a cube, spawn a
cylinder of a given radius and length, make a wheel, make a piece of 3D text.

**The one idea:** *it hides the traps so nobody has to remember them.* Chief among them: Unity's
cylinder mesh is 2 units tall and 1 across, so a cylinder of radius `r` and length `L` needs a
scale of `(2r, L/2, 2r)`. Bypass `Prim.Cyl` and you get a boiler of the wrong length with no error
to tell you. It also strips the collider off everything it spawns (no physics here), caches
materials so five scenes share one palette, and — importantly — **updates material assets in place
rather than recreating them**, because `AssetDatabase.CreateAsset` deletes whatever is already at
the path, which would leave the scenes built earlier in the same run pointing at destroyed objects.

The wheel builder has a nice piece of observation in it: each wheel gets a counterweight bar across
each face, because a perfectly smooth cylinder gives the eye nothing to track and a rolling wheel
then reads as a sliding one.

#### `StationKit.cs` — the station, and the look

Ground, both running lines, platform, canopy, lamps, benches, name boards, luggage trolley, station
building, signals and the scenery that fades off into the fog. It also owns the palette (every
material name and colour in one place) and `BuildEnvironment`, which sets the sky, the ambient
light, the fog and the sun.

**The one idea:** *there is one station, described once, and every scene calls it — so the four
scenes cannot drift apart.* Beyond that, this file is where the dusk lives, and the dusk is doing
real work: the fog (exponential-squared, density 0.0072) hides where the world stops, separates the
train from the background as it recedes, and catches the low sun. The sun sits at 7.5° elevation
raking straight down the platform so everything throws a long shadow. Two comments record
adjustments made from looking at the results: the sun was pulled back from 1.25 to 1.05 once ACES
tonemapping was added, because it was clipping the carriage roofs to white; and the ambient sky
colour was lifted because the waiting scene looks *away* from the sun and its foreground was
crushing to black while the two sunlit shots looked fine.

A cost-control decision worth quoting: sleepers are laid only across the stretch the fog lets you
see (x = −190 to 330 of a 900 m track), because the full length would be several thousand invisible
objects. Likewise only the sun casts shadows; the dozen lamps do not.

#### `TrainFactory.cs` — the locomotive

Builds a steam engine, tender and carriages out of primitives, from a small `TrainSpec` describing
livery and carriage count, and hands back a `TrainRig` listing everything a scene builder needs to
wire up: every wheel, the steam emitter, the nose, the tail, and the platform-side doorways in order
down the train.

**The one idea:** *the train is built nose-first at local X = 0 and runs backwards down −X, so a
scene builder can place the whole rig by its nose and know exactly where the front buffer beam is.*
That is what makes "the nose stops at x = 26" a statement you can trust. Internally a single
`cursor` variable walks backwards along the train, each sub-builder returning where it finished, so
adding a carriage cannot leave a gap.

Several comments in here are records of mistakes, and they are good viva material:

- Loco paint is at 0.20 smoothness on purpose. At 0.42 the flat cab side acted as a mirror for the
  low sun and blew the cab out to white.
- The firebox glow used to sit *inside* the cab. An unshadowed point light does not care about
  walls, so it lit the cab sides from within at point-blank range. It now hangs below the
  footplate, small and short-range, spilling onto the track instead.
- Carriage doors are painted the body colour knocked back a shade, not the gold lining. In lining
  gold they read as decorative panels rather than doors.
- Every doorway gets a dark recess behind it, so an open door reveals a hole in the side of the
  coach rather than the coach's own paintwork.
- The **last** carriage must not get a gangway connector, or the back of the train is a black slab
  hanging in mid-air. It gets a proper tail end instead — end window, buffer beam, red tail lamp —
  because in the departure scene the back of the train is what the camera watches for twenty
  seconds.
- The chimney steam is simulated in **world space**, or the plume would ride along with the train
  instead of being left hanging behind it.

#### `PassengerFactory.cs` — one figure

Builds an articulated passenger: hip, torso, head, two arms, two jointed legs, sometimes a hat,
sometimes a suitcase, from a randomised palette of seven coats and five skin tones with a height
variation of ±10% (because a crowd of identical heights looks like a row of bollards).

**The one idea:** *each limb's pivot sits at the joint, with the box hanging below it.* Rotate a
pivot placed at the joint and the limb swings from the joint, like a limb. Put the pivot in the
middle of the box — the obvious way — and the leg rotates about its own centre, which reads as
swimming rather than walking. The shin's pivot hangs off the thigh's pivot, so a knee bend is
relative to the thigh automatically. Two smaller notes: sleeves are a shade darker than the coat,
because identical colouring made the arms vanish into the torso and the figures read as legs with a
box on top; and a carried suitcase is parented under the arm pivot so it swings with the arm.

#### `SceneBuilders.cs` — the five scenes and the staging

Defines the two liveries (ours: lined green loco, crimson coaches, 3 carriages; theirs: plain black,
blue stock, 2 carriages), then builds each scene: world, train(s), motion settings, audio settings,
crowd, camera.

**The one idea:** *this is the shot list, expressed as code.* Everything that makes each of the
four scenes a different piece of filmmaking is a handful of numbers here — where the camera stands,
what it looks at, its focal length, how lazy its damping is, how it drifts. Arrival is head-on at
40° with tight 1.2 damping so she comes out of the fog straight at you. Waiting is wide at 42° from
beyond the platform end so both trains are in shot. Boarding is down the platform at head height at
46°, aimed at a fixed point rather than a person, so the doors and the queue are both in frame.
Departure is from the far end at 38° with lazy 2.6 damping and a 320 m release, watching her go.

The crowd routing is a small piece of cleverness: every passenger walks out to a shared lane
running parallel to the platform edge, along that lane to their nearest door, then in. Routing
everyone through the middle lane is what stops them cutting diagonally through one another. Their
wait-before-setting-off times are spread across 1–17.5 s, and the comment records why: at the
earlier 1.5–12.5 s everybody was aboard by halfway and the shot played out over an empty platform.

#### `PostFx.cs` — the grade

Adds ACES tonemapping, bloom and a vignette. **Tonemapping** is the step that squashes very bright
values into what a screen can show; ACES is a film-industry curve that rolls highlights off
gracefully instead of chopping them.

**The one idea:** *the tonemapper is load-bearing, not decoration.* The scene is lit by a very low
sun and is full of small bright sources — lamps, lit carriage windows, a firebox. Without a
tonemapper those clip to flat white, which is exactly what the carriage roofs were doing before
this file existed. It also degrades gracefully: if the post-processing package resources cannot be
found, it logs a warning and the scene still builds and still renders, just without the polish.

#### `BuildAll.cs` — the button

The menu items, and the project-wide settings. Builds all five scenes, registers them in Build
Settings, and has a command-line entry point that exits with a non-zero code if the build threw,
so a failed build cannot look like a pass.

**The one idea:** *some settings are not per-scene and must be set here or the film is wrong
everywhere at once.* Colour space is forced to **Linear** — gamma would wash the dusk lighting out
and make the emissive windows look like flat stickers. The per-pixel light count is raised from
Unity's default of 4 to 8, because the platform has a row of lamps plus lit windows plus a firebox
and the default leaves most of them falling back to cruder vertex lighting. Registering the scenes
in Build Settings is not optional either: without it `SceneFlowManager`'s load call fails at runtime
with nothing to show for it but a console warning.

### The checking and capture tools (editor only)

#### `SceneSnapshot.cs` — the fast check

Opens each scene, renders one still from that scene's own camera into `Snapshots/`.

**The one idea:** *a camera's render callbacks fire in edit mode, so these stills include the full
grade* — ACES, bloom and vignette are all present, which makes them a fair guide to how the film
will actually look. What they cannot show is motion: no script has run, so the train sits at its
start position. In `01_Arrival` that is correctly just a dot in the fog 72 m down the track.

#### `Playtest.cs` — the real check

Enters play mode, runs the whole 95 seconds, grabs nine frames at chosen moments into `Playtest/`,
listens for any runtime error, and reports pass or fail. It can be run from the command line and
exits with the result as its exit code.

**The one idea:** *everything else verifies the film statically; this is the only thing that proves
it runs.* That the scenes hand over to one another, that the train brakes onto its mark, that the
passengers reach a door — a still image cannot tell you any of that, and a null reference inside a
scene transition is exactly the sort of thing a static render would never catch. Two details:
domain reload has to be disabled or entering play mode wipes the static variables tracking where
the run is up to; and it times captures off `Time.time` (game time) rather than editor wall-clock,
because a run that stalled once had its captures firing 70 minutes apart while the film itself had
barely advanced.

#### `FilmCapture.cs` — the headless renderer

Runs in play mode and writes a numbered JPEG per frame plus a WAV of the soundtrack.

**The one idea:** *two Unity features make a headless film possible, and one memory discipline makes
it survivable.* `Time.captureFramerate` pins `Time.deltaTime` to exactly 1/30 s and lets the game
run as fast as it can render — so the output plays at real speed however slow the machine is.
`AudioRenderer` taps the audio mixer, which is the only way to get the synthesised whistle and
chuffs out of a run with no speakers attached. And the discipline: an earlier version accumulated
the whole soundtrack in a list and allocated two full-frame colour arrays per frame; the machine
ran out of RAM and killed the editor halfway through. Audio now streams straight to disk as it is
produced, and the fade is applied by scaling the texture's raw bytes in place — RGB24 has no alpha
and no padding, so every byte is a colour component and a uniform scale *is* a fade to black, with
nothing allocated.

One consequence worth understanding: it uses `Camera.Render` into an offscreen texture rather than
grabbing the screen. The screen fade is drawn in `OnGUI`, which an offscreen render never sees — so
the capture reads `SceneFlowManager.FadeAlpha` and composites the fade back in itself.

#### `RenderFilm.cs` — the command-line driver

Sets up `FilmCapture` with the requested resolution and length and starts play mode. Offers a full
render (96 s at 1280×720) and an 8-second 640×360 probe.

**The one idea:** *two ordering traps, both of which silently produced a full-length 720p render
when a short probe was asked for.* First, `AddComponent` runs `Awake` immediately on an active
GameObject, so setting the fields after adding the component is too late — the object is created
**inactive**, configured, then switched on. Second, entering play mode can trigger a domain reload
which resets static variables and drops the update hook, so the settings live in `SessionState`
(which survives reloads) and an `[InitializeOnLoadMethod]` puts the hook back if a reload lands
mid-capture. It also deletes the old frame folder first, because leftover frames would be picked up
by ffmpeg's filename pattern and spliced into the new film.

#### `RecordSequence.cs` — the alternative, via Unity Recorder

Configures Unity's official Recorder for a 1920×1080, 60 fps MP4 with audio, stopping itself at 96
seconds.

**The one idea:** *a scene load does not interrupt a Recorder capture.* That is why four separate
scene files still produce one continuous take with no seams beyond the fades. The settings are
pushed into the Recorder *window* rather than driven through `RecorderController` directly, because
that controller can only be prepared from play mode and would not survive the domain reload that
entering play mode triggers — the window already solves both problems.

#### `render-film.ps1` — the one command (not a C# file, but part of the story)

Finds Unity and ffmpeg wherever they happen to be installed, runs the build in batch mode, runs the
render in batch mode, checks that frames and audio actually appeared, and muxes them into
`Recordings/AshfordHill.mp4`. It searches several install roots because Unity Hub on a machine
without admin rights does not put the editor under Program Files, and it hunts down ffmpeg inside
winget's package folder because a freshly installed ffmpeg is invisible to the shell that installed
it.

---

## 5. Questions you will be asked, and honest answers

**How long is it, and how many scenes?**
About 95 seconds. Five scene files: a bootstrap scene with no scenery that drives the film, and
four film scenes at 25, 18, 28 and 24 seconds, plus a 0.6 s hold on black at the start.

**Why did you not use any Asset Store models or textures?**
Two reasons. Practically, it means everything in the film is mine and I can explain every object in
it — nothing is a black box I downloaded. Technically, it forces the whole project to be described
in code, which is what makes the scenes regenerable and the four shots consistent with each other.
To be precise about what "no assets" means: there are no downloaded models, textures, audio files
or Asset Store packages. Two *official Unity* packages are used as tooling — Post Processing (for
the ACES grade) and Recorder (as an alternative MP4 path) — and they supply no content, only effects
and capture.

**Why are the scene files generated instead of built by hand?**
Because the same station appears in four scenes. If I placed it by hand four times, the four would
drift apart and any change would have to be made four times. Generated from one description, they
cannot drift. It also means the real source is readable text in version control, and it means
positions can be *derived* rather than eyeballed — the arrival start position is calculated from the
braking curve, not dragged into place.

**What is the cost of doing it that way?**
I cannot nudge anything by eye, and any hand edit to a scene is destroyed by the next rebuild.
That is why there is a snapshot tool and a playtest tool — they replace the thing you would
normally do by dragging and looking.

**How is the sound made if there are no audio files?**
`ProceduralAudio.cs` generates every clip as raw numbers when the film starts — 44,100 numbers per
second of sound. The whistle is four sine tones tuned slightly apart so they beat against each
other, with vibrato and filtered noise for breath. A chuff is a burst of white noise with an
instant attack and a long tail, pushed through a band-pass filter that sweeps downwards as the
blast loses pressure. The brake squeal is noise through two stacked resonant filters — one alone
just sounds like noise; two ring. The door slam is a low thump that drops in pitch as it seats,
plus a short bright rattle for the latch.

**How does the exhaust beat stay in time with the wheels?**
It is not timed at all. A two-cylinder steam locomotive gives four exhaust beats per revolution of
its driving wheels, so the code fires one beat for every quarter of a wheel circumference the train
travels. Because it is keyed to distance rather than to a clock, it speeds up with the train
automatically and cannot drift out of step with the wheels on screen.

**Why do the wheels have different sizes handled separately?**
Because they are different sizes: 0.95 m radius drivers, 0.55 m carriage and tender wheels. If they
all turned at one shared rate, the ones with the wrong radius would visibly skate along the rail.
Each wheel stores its radius and converts distance to rotation itself, so they are all right at
once.

**Why is it set at dusk?**
Dusk does three jobs at once. The fog hides where the world stops — the ground is finite and dusk
plus fog means you never see the edge. It gives the film small bright points (lamps, lit windows,
the firebox, the signal lens) against a dark ground, which is what makes emissive materials carry
so much of the detail cheaply. And it means a single very low sun rakes down the platform and
everything throws a long shadow, which is far more three-dimensional than flat daylight on grey
boxes. The trade is that it made a tonemapper mandatory — see the next question.

**How do you know the train stops in exactly the right place?**
The start position is derived, not chosen. The braking curve is `speed = entrySpeed × (1 − u)²`,
and the exact ground that curve covers is its integral, `entrySpeed × arriveTime ÷ 3` — 72 m for the
values in the arrival scene. `TrainMotion` exposes that as `ArrivalDistance` and the scene builder
places the nose exactly that far back from the stop mark at x = 26. Change the braking curve and the
stop stays correct. Being fully honest: the movement is integrated frame by frame, so the numerical
result lands a fraction of a metre short of the analytic answer at 30 fps, and it errs the same way
every time. On a locomotive over 20 m long, at a station whose platform is 98 m, that is invisible —
and during capture the timestep is pinned to exactly 1/30 s, so it is also deterministic.

**What happens if I press Play in the middle scene instead of the first one?**
That single shot plays, correctly and on its own. Every film scene is self-contained: it has its
own station, its own train, its own camera and its own audio, and its timings run from the moment
that scene starts. What you do *not* get is the fade in, the fade out, or the handover to the next
scene, because `SceneFlowManager` lives only in `00_Bootstrap`. The shot will simply keep running
past its intended out point. That is the normal way to work on one shot in isolation.

**Why are there no colliders or physics?**
Nothing in this film reacts to anything. The train follows a computed speed curve, the passengers
follow waypoints, the doors rotate to an angle. Adding collision would add cost and a whole class of
bug (jitter, objects pushing each other) in exchange for nothing, so `Prim.Spawn` deletes the
collider off every primitive it creates as it makes it. It matters because a scene of this size
would otherwise carry several thousand colliders that never do anything.

**Why is the post-processing described as "load-bearing"?**
Because the scene is lit by a very low sun and is full of small very bright sources. Without a
tonemapper those brightness values simply clip to flat white — the carriage roofs were doing exactly
that before `PostFx.cs` existed. ACES rolls the highlights off instead of chopping them, and bloom
lets the lamps bleed the way a real lamp does at dusk. It is the difference between "dusk" and
"an underexposed afternoon".

**Why four separate scenes rather than one long one?**
Each shot is a different world state — in one the train is 72 m down the track, in another it is
standing with eighteen passengers on the platform, in another there are two trains. Building them
as separate scenes means each one only contains what it needs and each can be worked on alone. The
loads are hidden inside the fades, and Unity Recorder is not interrupted by a scene load, so the
four files still record as one continuous take.

**How do you know it actually works?**
Three levels. The build fails loudly and exits non-zero if anything throws. **Snapshot Scenes**
renders a still from each scene's own camera, complete with the grade, for checking framing and
lighting. **Playtest** enters play mode, runs the full 95 seconds headlessly, captures nine frames
across the four scenes, treats any runtime error as a failure, and reports pass or fail with an exit
code. That last one is the only thing that proves the scenes hand over, the train stops on its mark,
and the passengers reach a door.

**Do the passengers avoid each other?**
Not by collision — there is no collision detection anywhere in the project. They avoid looking wrong
by design instead: everyone walks out to one shared lane parallel to the platform edge, along it to
their nearest door, then in, which stops them cutting diagonally across one another. Their start
times are staggered randomly across the first 17.5 seconds and their walking speeds vary, so they do
not move as a block. The nearest-door rule stops anybody walking the length of the train past three
empty doors.

**Where do the passengers go when they board?**
There is no carriage interior. As a passenger steps up and walks forward through the doorway, the
figure scales down to 70% and then deactivates. The shrink sells passing into a dark interior
without there being one to walk into.

**Could you add another carriage, or a third train?**
Yes, and it is a one-line change. `TrainSpec.carriages` controls it, the factory walks a cursor
backwards down the train so nothing overlaps, and everything downstream — the wheel list, the door
list, the tail marker used as a camera target — is collected from what was actually built rather
than written down separately. Rebuild and every scene picks it up.

**What was the hardest thing to get right?**
The headless render. The first version held the entire soundtrack in memory and allocated two
full-frame pixel arrays per frame; it ran the machine out of RAM and killed the editor halfway
through. It now streams audio to disk as it is produced and fades the frame by scaling the texture's
raw bytes in place. There was also a subtle ordering bug where asking for a short 640×360 test
render silently produced a full-length 720p one, because adding a component to an active object runs
its startup code immediately, before the settings had been applied.

**Why does the camera wobble and lag?**
So it reads as somebody standing on a platform rather than a camera bolted to the train. The look-at
is damped, so the train slides off centre before the camera catches up; there is a slow positional
drift; and there is a small Perlin-noise shake. In the departure scene the camera also gives up
following past 320 m, rather than creeping round after a dot in the fog.

**What is `00_Bootstrap` for, if it has no scenery?**
It holds the one object that has to survive every scene load: the flow manager that plays the four
scenes in order and draws the fade. It also contains a plain black camera, purely so the half-second
before the first real scene loads is a deliberate black frame rather than Unity's "no cameras
rendering" error.

---

## 6. The traps — the non-obvious things the comments record

These are the "we tried X and it broke, so we do Y" notes. They are the most convincing thing in the
project to be able to talk about.

**Geometry and materials**

| Trap | What happens | The fix |
|---|---|---|
| Unity's Cylinder mesh is 2 units tall, 1 across | A cylinder of radius `r`, length `L` needs scale `(2r, L/2, 2r)`. Get it wrong and you get a boiler of the wrong length with no error | Always go through `Prim.Cyl` / `Prim.Capsule` |
| `AssetDatabase.CreateAsset` deletes whatever is at the path first | All five scenes are built in one run off one shared palette; recreating a material mid-run leaves the earlier scenes pointing at a destroyed object | `Prim.Build`, `StationKit.BuildEnvironment` and `PostFx.BuildProfile` all load-and-update in place |
| A reused material may still be emissive from a previous run | A surface that should be matte keeps glowing | The material builder explicitly clears the emission keyword when no emission is asked for |
| Unity 6 dropped the built-in Arial font | `Resources.GetBuiltinResource<Font>("Arial.ttf")` returns null and the sign is blank | Try `LegacyRuntime.ttf` first, fall back to Arial, and return null rather than throwing so a missing font costs you a blank sign, not a failed build |
| A `TextMesh` reads correctly from its local −Z side | Rotating a name board 180° to "turn it round" produces mirrored lettering | Leave the text at identity rotation and place the board around it |

**Lighting**

| Trap | What happens | The fix |
|---|---|---|
| An unshadowed point light ignores walls | The firebox light, placed inside the cab, lit the cab sides from within at point-blank range and blew them out to white | Hang it below the footplate, small and short-range, where it spills onto the track instead |
| The same problem, smaller | The carriage interior light was reaching through the coach sides and turning open doors into pale rectangles | Keep it weak and short-range; the emissive glass does the real work |
| A shiny flat surface at a low sun angle | At 0.42 smoothness the flat cab side acted as a mirror and blew out | Loco paint sits at 0.20 smoothness |
| Bright sources without a tonemapper | Lamps, windows and roofs clip to flat white | ACES tonemapping in `PostFx`; the sun was also pulled back from 1.25 to 1.05 once ACES was in |
| Gamma colour space | Washes the dusk out and makes emissive windows look like flat stickers | `BuildAll` forces Linear |
| Unity's default of 4 per-pixel lights | Most of the lamps fall back to cruder vertex lighting | Raised to 8 |
| One shot looks away from the sun | The waiting scene's foreground crushed to black while the two sunlit shots looked fine | The ambient sky colour was lifted for all scenes |

**Unity plumbing**

| Trap | What happens | The fix |
|---|---|---|
| `AddComponent` runs `Awake` immediately on an active object | Settings applied after the call are too late; a short test render came out full-length at full resolution | Create the object inactive, configure it, then activate it |
| Entering play mode can trigger a domain reload | Static variables reset and update subscriptions are dropped mid-capture | Disable domain reload; keep settings in `SessionState`; re-attach the hook from `[InitializeOnLoadMethod]` |
| `PostProcessProfile.AddSettings` does not parent the effect into the asset | The saved profile is three null entries and the whole grade is silently absent at runtime | Follow it with `AssetDatabase.AddObjectToAsset` |
| Skipping `PostProcessLayer.Init` | Magenta screen at runtime | Always pass it the resources |
| A post-processing volume on the wrong layer | The camera's layer mask never finds it, so no grade | Keep the volume on Default and match the mask |
| `Camera.main` only finds enabled cameras tagged MainCamera, and in edit mode the tag lookup is not always populated | Snapshot finds no camera | Fall back to any camera in the scene |
| Not restoring `RenderTexture.active` after an offscreen render | The next scene renders into a texture that no longer exists | Save and restore both the camera target and the active render texture |
| Editor wall-clock vs game time | A stalled run had its playtest captures firing 70 minutes apart while the film had barely advanced | Time everything off `Time.time` |
| Recorder's movie recorder is a separate object | It is silently dropped when the settings asset is reloaded | `AddObjectToAsset` it into the settings |
| Old frames left in the capture folder | ffmpeg's numbered-file pattern splices them into the new film | Delete the folder before every render |

**Motion and audio**

| Trap | What happens | The fix |
|---|---|---|
| Firing at most one chuff per frame | At speed, more than one exhaust beat falls inside a frame; the rhythm thins out exactly when it should tighten | A `while` loop with a safety guard of 8 |
| A single audio player for the chuffs | A fast beat cuts off the tail of the one before it | A pool of six overlapping voices |
| A looping clip that does not match at the seam | An audible click every loop | `LoopFade` crossfades the tail over the head — and takes the array **by reference**, because it trims the array afterwards and without `ref` the resize would be lost and the click would remain |
| Sharing filter state between effects | The brake squeal's grind trampled the ringing that makes it a squeal rather than noise | Give the grind its own filter state |
| Two signals sharing one mutated material | Both change aspect together | Drive lens brightness through a `MaterialPropertyBlock` |
| Particles simulated in local space | The steam plume rides along with the train instead of being left behind | Set the particle system to world-space simulation |
| Posing the figure after applying the idle sway | `Pose` writes the hip too, so it immediately overwrote the sway and the fidget was never visible | Call `Pose` first, then apply the sway |
| Turning and moving in the wrong order | The figure crabs sideways | Turn towards travel first, then step |
| The fade lives in `OnGUI` | An offscreen `Camera.Render` never sees it, so the captured film has no fades | Composite it back in from `SceneFlowManager.FadeAlpha` |

**Staging**

- The signal was originally at x = 64, where it sat *behind* the camera in the waiting scene — which
  rather defeated the point of having a signal. It is now at x = 46, ahead of the train and in shot.
- Passenger wait times were originally 1.5–12.5 s, which had everybody aboard by the halfway mark
  and left the shot playing out over an empty platform. They are now spread across 1–17.5 s.
- The last carriage must not get a gangway connector, or the back of the train is a black slab in
  mid-air.

---

## 7. Numbers worth having in your head

| Thing | Value | Where it lives |
|---|---|---|
| Unity version / pipeline | 6000.0.83f1, Built-in Render Pipeline | `ProjectSettings` |
| Total running time | ~95 s (0.6 + 25 + 18 + 28 + 24) | `SceneBuilders.BuildBootstrap` |
| Fade between scenes | 0.7 s each way | same |
| Track axis | Runs along X; train departs towards +X; Z is across the tracks | `StationKit` |
| Where the loco stops | Nose at x = 26 | `StationKit.StopMarkX` |
| Arrival entry speed / braking time | 16 m/s over 13.5 s, after a 1.5 s delay | `SceneBuilders.BuildArrival` |
| Distance the arrival covers | 72 m (= 16 × 13.5 ÷ 3), so the nose starts at x = −46 | `TrainMotion.ArrivalDistance` |
| Departure | 4 s dwell, 17 s to a 23 m/s top speed | `SceneBuilders.BuildDeparture` |
| Driving wheel / carriage wheel radius | 0.95 m / 0.55 m | `TrainFactory` |
| Exhaust beats per wheel revolution | 4 (two-cylinder locomotive) | `TrainAudio` |
| Audio sample rate | 44,100 Hz, mono clips, generated at startup | `ProceduralAudio` |
| Passengers in the boarding scene | 18 | `SceneBuilders.BuildCrowd` |
| Our train / the other train | 3 carriages / 2 carriages | `SceneBuilders` liveries |
| Track length, sleepers laid | 900 m of rail; sleepers only from x = −190 to 330 | `StationKit` |
| Fog | Exponential-squared, density 0.0072 | `StationKit.BuildEnvironment` |
| Sun | Intensity 1.05, elevation 7.5°, soft shadows | same |
| Headless render | 96 s, 1280×720, 30 fps, JPEG q90 + 16-bit WAV, muxed at CRF 19 | `RenderFilm`, `render-film.ps1` |
| Recorder path | 1920×1080 at 60 fps, MP4 with audio | `RecordSequence` |

---

## 8. If you are asked to change something live

| "Make it..." | Change this |
|---|---|
| brighter / darker | `grade.postExposure` in `PostFx.cs` (currently −0.30) |
| foggier | `RenderSettings.fogDensity` in `StationKit.BuildEnvironment` |
| stop further along the platform | `StationKit.StopMarkX` — the start position follows automatically |
| brake harder | `motion.arriveTime` in `SceneBuilders.BuildArrival` — the start position follows automatically |
| longer | the `duration` values in `SceneBuilders.BuildBootstrap` (and `FilmCapture.seconds` if rendering) |
| a longer train | `carriages` in `SceneBuilders.OurTrain()` |
| a busier platform | `count` in `SceneBuilders.BuildCrowd` |

Then **Tools ▸ Train Station ▸ Build All Scenes**, and **Snapshot Scenes** to check it.
