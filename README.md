# Ashford Hill — a four-scene steam train film

A short Unity film at a railway station, in four scenes:

1. **01_Arrival** — the train comes in out of the fog and stops at the platform.
2. **02_Waiting** — it holds while another train clears the section and the signal comes off.
3. **03_Boarding** — passengers queue along the platform and board.
4. **04_Departure** — it whistles, pulls away and recedes into the fog.

They play back to back as one continuous piece, driven by `00_Bootstrap`.

## Everything here is generated

There is not a single downloaded asset, texture, model, audio file or Asset Store package in
this project. Every object is built from Unity primitives by editor scripts, every material is
created in code, and **every sound is synthesised sample by sample at runtime** — the steam
whistle is four detuned sine partials with vibrato, the chuffs are filtered noise bursts, the
brake squeal is a pair of very resonant filters swept over noise.

That means the `.unity` scene files are **build output, not source**. Do not hand-edit them.
Change the builders and regenerate.

## Requirements

- Unity **6000.0.83f1** (Unity 6 LTS), Built-in Render Pipeline.
- No admin rights needed — the editor lives under `C:\Users\biyon\Unity\Editors`.

## Running it

1. Open the project in Unity.
2. **Tools ▸ Train Station ▸ Build All Scenes** (`Ctrl+Shift+T`).
   This regenerates all five scenes, creates the materials, and registers the scenes in
   Build Settings.
3. Open `Assets/Scenes/00_Bootstrap.unity` and press **Play**.

Total running time is about 95 seconds.

## Layout

The track runs along **X**. The train departs towards **+X**. **Z** is across the tracks.

| Thing | Value |
|---|---|
| Platform road (our train) | `z = 0` |
| Through road (the other train) | `z = -7.6` |
| Rail top | `y = 0.52` |
| Platform surface | `y = 1.5` |
| Platform extent | `x = -58 … 40` |
| Where the loco stops | `x = 26` (nose) |

## Code map

### Runtime — `Assets/Scripts/`

| File | What it does |
|---|---|
| `TrainMotion.cs` | Moves a train. Three phases: **Arrive** (brakes to a stand on the mark), **Hold**, **Depart**. Speed is the single source of truth — wheels, steam and audio all derive from it. |
| `TrainWheel.cs` | One wheel that knows its own radius. Drivers are 0.95 m and carriage wheels 0.55 m, so a shared rate would leave half the train skating. |
| `TrainAudio.cs` | Schedules chuffs **by distance, not by a timer** — four exhaust beats per driving-wheel revolution — so the rhythm accelerates with the train and can never drift out of step with it. |
| `ProceduralAudio.cs` | All sound synthesis. Whistle, chuff, brake squeal, safety valve, ambience, chime, door slam. |
| `PassengerWalker.cs` | Procedural walk cycle driven by distance covered. Legs, arms, hip bob and lean all come off one stride phase. |
| `BoardingDirector.cs` | Runs the boarding: chime, doors open, passengers board, doors slam down the train one at a time, guard's whistle. Exists because a delegate can't be saved into a scene. |
| `CarriageDoor.cs` | One slam door, hinged at its leading edge, swinging out over the platform. |
| `FilmCapture.cs` | Headless capture: JPEG sequence plus a WAV, both streamed rather than buffered. |
| `CameraDolly.cs` | Damped look-at plus drift and handheld noise. The lag is the point: the train pulls ahead of frame instead of staying pinned to centre. |
| `SceneFlowManager.cs` | Loads the four scenes in order with a fade. Lives in `00_Bootstrap` and survives every load. |
| `SignalLight.cs` | Two-aspect colour-light signal. Red until the other train is clear. |

### Build — `Assets/Scripts/Editor/`

| File | What it does |
|---|---|
| `Prim.cs` | Primitive and material helpers. Owns the cylinder gotcha (see below). |
| `StationKit.cs` | Ground, track, platform, canopy, lamps, station building, signals, scenery. Also the dusk lighting and fog. |
| `TrainFactory.cs` | The steam rig — loco, tender, carriages. Built nose-first at local `X = 0`, running back down `-X`. |
| `PassengerFactory.cs` | One articulated figure. Limb pivots sit **at the joint** with the box hanging below. |
| `SceneBuilders.cs` | The five scenes, and the staging for each shot. |
| `PostFx.cs` | ACES tonemapping, bloom and vignette. Not decoration — see below. |
| `BuildAll.cs` | The menu items, plus project settings (Linear colour space, light counts). |
| `RecordSequence.cs` | Configures Unity Recorder for the MP4. |
| `SceneSnapshot.cs` | Renders a still from each scene's own camera into `Snapshots/`, for checking framing without opening the editor. |
| `Playtest.cs` | Runs the whole film headlessly in play mode, grabbing frames into `Playtest/` and failing on any runtime error. |
| `RenderFilm.cs` | Drives `FilmCapture` from the command line. Watch the two ordering traps documented in it. |

## Things worth knowing before you change anything

- **Unity's Cylinder mesh is 2 units tall and 1 across.** A cylinder of radius `r` and length
  `L` needs a scale of `(2r, L/2, 2r)`. `Prim.Cyl` hides this; bypass it and you get a boiler of
  the wrong length with no error to tell you.
- **Colour space must be Linear.** Gamma washes the dusk lighting out and makes the emissive
  windows look like flat stickers. `BuildAll` sets this.
- **Arrival distance is derived, not guessed.** `TrainMotion.ArrivalDistance` is the integral of
  the braking curve, and the builder places the train back down the track by exactly that much so
  it stops *on* the mark rather than near it. Change the braking curve and the stop stays correct.
- **Sleepers are only laid where the fog lets you see them.** The full 900 m of track would be a
  few thousand invisible objects.
- **Nothing has a collider.** There is no physics in this film; `Prim.Spawn` strips them.
- **The tonemapper is load-bearing.** The scene is lit by a very low sun and full of small bright
  sources — lamps, lit windows, a firebox. Without ACES those clip to flat white, which is exactly
  what the carriage roofs were doing before `PostFx` existed. If you raise the sun or the emissive
  multipliers, check `Snapshots/` afterwards.
- **Shared assets are updated in place, never recreated.** All five scenes are built in one run off
  one palette, and `AssetDatabase.CreateAsset` deletes whatever is already at the path — so
  recreating a material or the skybox mid-run would leave the scenes built earlier pointing at
  destroyed objects. `Prim.Build`, `StationKit.BuildEnvironment` and `PostFx.BuildProfile` all
  load-and-update instead.

## Checking your work

**Tools ▸ Train Station ▸ Snapshot Scenes** renders a still from each scene's own camera into
`Snapshots/`. These include the full grade — render callbacks fire in edit mode, so ACES, bloom
and the vignette are all present — which makes them a fair guide to how the film will look.

The one thing they cannot show is **motion**: no script has run, so the train sits at its start
position. In `01_Arrival` that is correctly just a dot in the fog.

If the grade wants nudging, the knobs are `grade.postExposure` and `vignette.intensity` in
`PostFx.cs`.

**Tools ▸ Train Station ▸ Playtest** is the stronger check: it enters play mode, runs the full
95 seconds, writes frames into `Playtest/` and reports failure if anything logs a runtime error.
That is the only thing here that proves the film actually *runs* — that the scenes hand over, the
train brakes onto its mark and the passengers reach a door. Run it from the command line with:

```
Unity.exe -batchmode -projectPath <project>           -executeMethod TrainStation.Build.Playtest.BatchPlaytest -logFile <log>
```

It needs domain reload disabled to work at all, which it sets for itself.

## Making the MP4

```powershell
.ender-film.ps1              # builds, renders, muxes -> Recordings/AshfordHill.mp4
.ender-film.ps1 -Probe       # 8 seconds at 640x360, to check the pipeline first
.ender-film.ps1 -SkipBuild   # reuse the scenes already on disk
```

No editor window, no buttons. `FilmCapture` runs the film in batch mode and writes a JPEG
sequence plus a WAV; ffmpeg muxes them. Two details make it work:

- **`Time.captureFramerate`** pins `Time.deltaTime` to exactly 1/fps and lets the game run as
  fast as it can render, so the output plays at real speed however slow the machine is.
- **`AudioRenderer`** taps the audio mixer, which is the only way to get the synthesised whistle
  and chuffs out of a headless run. Batch mode does produce real audio — verified.

The screen fade lives in `OnGUI`, which an offscreen `Camera.Render` never sees, so it is
composited back in from `SceneFlowManager.FadeAlpha`.

### Or use Unity Recorder

**Tools ▸ Train Station ▸ Record Film (MP4)** configures the Recorder window instead, if you
would rather drive it by hand. Scene loads do not interrupt a Recorder capture, so the four
separate scene files still produce one continuous take.
