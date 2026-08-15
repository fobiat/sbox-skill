<!--
  s&box Skill : sound-and-language.md

  The audio mixer graph and sound handles, plus phrases and language files.

  Author  : Kyle (fobiat) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# Audio & Localization

Covers the `Sandbox.Audio` mixer graph (bus routing, occlusion/spatialization, processors,
`SoundHandle`) and the `Sandbox.Localization` system (`Phrase`, `PhraseCollection`,
on-disk layout, `#`-token resolution in UI). Read out of engine source at version 26.08.05:
`engine/Sandbox.Engine/Systems/Audio/` (`Mixer.cs`, `Mixer.Children.cs`, `Mixer.List.cs`,
`Mixer.Processors.cs`, `Mixer.Serialize.cs`, `SoundHandle.cs`, `Sound.Play.cs`, `Sound.cs`,
`MixerHandle.cs`, `AudioMeter.cs`, `Processors/*.cs`), `engine/Sandbox.Engine/Resources/Sound/SoundEvent.cs`,
`engine/Sandbox.Engine/Systems/Localization/Language.cs`, `engine/Sandbox.System/Localization/`
(`Languages.cs`, `Phrase.cs`, `PhraseCollection.cs`), `engine/Sandbox.Engine/Systems/UI/Controls/Label.cs`,
`engine/Sandbox.Engine/Scene/Components/Render/TextRenderer.cs`, and
`game/addons/tools/Code/Utility/LocalizationTools.cs`.

---

## Part 1: Audio

### 1. The mixer graph

`Sandbox.Audio.Mixer` is a tree. Every mixer except the root has exactly one `Parent`
(`internal`, not settable from outside the class); `IsMaster` is just `Parent is null`.

```csharp
public static Mixer Master { get; }    // root
public static Mixer Default { get; set; } // sounds with no TargetMixer play here
public static Mixer Voice { get; }     // reserved for VoIP
```

Lookups walk the tree from `Master`, matching `Name` case-insensitively or `Id` exactly:

```csharp
Mixer.FindMixerByName( "Music" );   // static, searches from Mixer.Master
Mixer.FindMixerByGuid( someGuid );
```

There is no indexed lookup, it's a recursive tree walk every call (the source comment on
`FindMixerByName` even says "We might want to do a fast lookup at some point").

Building the tree:

```csharp
var bus = parentMixer.AddChild();   // creates a new Mixer, adds to Children, returns it
bus.Name = "Explosions";
bus.Destroy();                       // removes self from Parent.Children (no-op on Master)
Mixer[] kids = parentMixer.GetChildren();
bool nested = child.IsDescendantOf( ancestor );
int n = parentMixer.ChildCount;
```

| Member | Type | Notes |
|---|---|---|
| `Name` | `string` | Display name and the string mixer-routing keys off |
| `Id` | `Guid` | Assigned in the constructor, stable identity for serialization/lookup |
| `Volume` | `float` `[0,1]` | Thread-safe (`Interlocked`/`Volatile`), scales this mixer's output |
| `Mute` | `bool` | Muted if this or any ancestor is muted (`IsMuted()` walks up) |
| `Solo` | `bool` | See solo semantics below |
| `MaxVoices` | `int`, default `64` | Cap on simultaneous voices mixed on this mixer per frame; oldest-first cutoff beyond the cap |
| `IsMaster` | `bool` | `Parent is null` |
| `Meter` | `AudioMeter` | Rolling buffer of `Frame { MaxLevelLeft, MaxLevelRight, VoiceCount }`, updated every mix. `Meter.Current` for a VU-meter style readout |

**Solo semantics**: if *any* mixer anywhere in the tree has `Solo = true`, every mixer whose
`IsSolo()` is false (not itself solo, and no ancestor is solo) stops mixing voices entirely
(`ShouldMixVoices()` in `Mixer.cs`). Soloing a child does not silence its own children unless
you explicitly check further down, they just aren't automatically excluded by the parent's
solo state, only mixers outside the solo'd branch are cut.

A sound targets a mixer by **name match**, not object reference: `ShouldPlay` compares
`vs.TargetMixer.Name == Name`. A `null` `TargetMixer` on the voice means "whatever
`Mixer.Default` currently is".

### 2. Routing a sound, and how buses get set up in practice

`Mixer.ResetToDefault()` (called on startup and whenever the mixer settings fail to load)
builds the shipped default topology:

```
Master
├── Music   (Spatializing=0, DistanceAttenuation=0, Occlusion=0, Reverb=0, AirAbsorption=0)
├── Game    (all simulation floats at default 1) ← Mixer.Default
├── UI      (same flat 2D settings as Music)
└── Voice                                        ← Mixer.Voice
```

`Music` and `UI` are flattened to pure 2D (no distance falloff, occlusion, reverb or air
absorption) because they're meant to always be audible regardless of world position.

Route a sound to a bus in one of three ways:

```csharp
// 1. At the SoundEvent asset level (persists with the asset)
soundEvent.DefaultMixer = "Music";              // MixerHandle has an implicit string conversion

// 2. Per-play, via Sound.Play overloads that take a mixer
Sound.Play( "explosion", Audio.Mixer.FindMixerByName( "SFX" ) );

// 3. On the handle after the fact
var handle = Sound.Play( soundEvent );
handle.TargetMixer = myBusMixer;                  // SoundHandle.TargetMixer is a Mixer, not a MixerHandle
```

`MixerHandle` (used on `SoundEvent.DefaultMixer` and anywhere a mixer reference needs to
survive serialization) stores both `Name` and `Id` and resolves by GUID first, then name:

```csharp
public readonly Mixer Get( Mixer fallback = null );   // GUID lookup, then name lookup, then fallback
public readonly Mixer GetOrDefault();                  // fallback = Mixer.Default ?? Mixer.Master
```

It has implicit conversions from `string`, `Guid` and `Mixer`, so `soundEvent.DefaultMixer = "Music";`
compiles directly.

A project's actual bus layout (renamed/added buses, per-bus volume, tags, processors) is
edited in the audio mixer editor tool and persisted as `MixerSettings` (a `ConfigData` with
`Version => 2` holding a raw `JsonObject Mixers` tree), loaded via
`Mixer.LoadFromSettings(settings, typeLibrary)`. That call always resets to the default
topology first, then deserializes the saved tree over it, so `Music`/`Game`/`UI`/`Voice`
exist even in a project that has never touched mixer settings.

`SoundEvent.UI = true` is a shortcut for "always 2D, always audible": `Sound.Play` forces
`ListenLocal = true` and zeroes `DistanceAttenuation`/`AirAbsorption`/`OcclusionEnabled`/`ReverbEnabled`
on the handle, and if the event has no `DefaultMixer` set it auto-routes to a mixer literally
named `"UI"` (`Mixer.FindMixerByName( "UI" )`). If your project renamed or removed the `UI`
bus, that fallback silently returns null and the sound falls through to `Mixer.Default`.

### 3. Occlusion and spatialization

All of these live under `[Group( "Simulation" )]` on `Mixer` and are `[Range(0,1)]` floats
unless noted, thread-safe via `Interlocked`/`Volatile`:

| Property | Meaning |
|---|---|
| `Occlusion` | How much sounds on this mixer can be occluded by geometry. `0` disables occlusion simulation entirely for the bus |
| `DistanceAttenuation` | How much sounds get quieter with distance (`0` = no falloff, flat volume regardless of distance) |
| `Spatializing` | `0` = comes out of all speakers (non-positional), `1` = fully spatialized/binaural |
| `AirAbsorption` | How much high-frequency energy the air absorbs over distance |
| `Reverb` | How much reverb send this bus contributes (`0` = dry) |
| `MaxVoices` | Not simulation-grouped, but caps voices as above |

**`Spacializing` vs `Spatializing`, both exist.** `Spatializing` is the real, current
property. `Spacializing` is a second property on `Mixer`, explicitly marked
`[Obsolete( "Use Spatializing instead." )]`, that forwards straight through:

```csharp
[Obsolete( "Use Spatializing instead." ), Hide]
public float Spacializing
{
    get => Spatializing;
    set => Spatializing = value;
}
```

Both compile. `Mixer.Deserialize` even reads either JSON key (`js["Spatializing"] ?? js["Spacializing"]`)
for backward compatibility with old saved mixer settings. Always write `Spatializing` in new
code, `Spacializing` is the misspelling frozen in for legacy data.

Occlusion/simulation tracing is tag-gated, and this is also mid-migration:

| Current | Legacy (`[Obsolete]`) |
|---|---|
| `BlockingTags` (`TagSet`) | `OverrideOcclusion` (`bool`) + `OcclusionTags` (`TagSet`) |
| `IgnoredTags` (`TagSet`) | (no direct equivalent, `IgnoredTags` is new) |
| `GetBlockingTags()` / `GetIgnoredTags()` | `GetOcclusionTags()` |

`BlockingTags` empty means "hit everything"; non-empty restricts audio traces to bodies
carrying one of those tags. `IgnoredTags` is applied on top and always skips those tags
regardless of `BlockingTags` (defaults seeded on Master: `passaudio`, `passbullets`, `sky`,
`playerclip`, `trigger`, `player`). Both walk up to the parent mixer if the local mixer has
neither set (`GetBlockingTags`/`GetIgnoredTags` in `Mixer.cs`).

### 4. `SoundHandle` and controlling a playing sound

`Sound.Play` returns a `SoundHandle`, live even before you check `IsValid()`:

```csharp
public static SoundHandle Play( string eventName, float fadeInTime = 0.0f );
public static SoundHandle Play( SoundEvent soundEvent, float fadeInTime = 0.0f );
public static SoundHandle Play( SoundEvent soundEvent, Vector3 position, float fadeInTime = 0.0f );
public static SoundHandle Play( string eventName, Vector3 position, float fadeInTime = 0.0f );
public static SoundHandle Play( string eventName, Audio.Mixer mixer );
public static SoundHandle Play( SoundEvent soundEvent, Audio.Mixer mixer );
public static SoundHandle PlayFile( SoundFile soundFile, float volume = 1.0f, float pitch = 1.0f, float delay = 0.0f, float fadeInTime = 0.0f );
public static void StopAll( float fade );
```

`Application.IsHeadless` makes every `Play` overload a no-op returning `SoundHandle.Empty`
(safe to call, does nothing).

Key `SoundHandle` members once you have one:

| Member | Type | Notes |
|---|---|---|
| `Position` / `Rotation` / `Transform` | | World placement of the sound source |
| `Volume` | `float`, default `1` | |
| `Pitch` | `float`, default `1` | |
| `SpacialBlend` | `float [0,1]`, default `1` | Per-sound 3D blend, combines with the mixer's `Spatializing` |
| `Distance` | `float`, default `15000` | Max audible distance |
| `Falloff` / `Fadeout` / `Fadein` | `Curve` | Distance falloff and fade curves |
| `TargetMixer` | `Mixer` | Which bus this sound mixes into |
| `DistanceAttenuation` / `AirAbsorption` / `OcclusionEnabled` / `ReverbEnabled` | `bool`, default `true` | Per-sound overrides |
| `Reverb` | `float`, default `1` | Per-sound reverb send contribution |
| `ListenLocal` | `bool` | Places the listener at origin facing forward, used for UI/local sounds |
| `Loopback` | `bool` | Marks this as the local player's own voice; only plays if `voice_loopback` is on |
| `Time` | `float`, get/set | Current playback position in seconds; setting seeks (published to the mix thread, not instant) |
| `Amplitude` | `float` | Current loudness, useful for lip sync / visualization |
| `IsPlaying` / `IsStopped` / `IsValid` | `bool` | `IsStopped == !IsValid` |
| `Paused` | `bool` | |
| `Stop( float fadeTime = 0f )` | | `fadeTime > 0` fades out over time instead of stopping immediately |

Obsolete members still present for old code: `Decibels` (unused), `Reflections` (alias for
`ReverbEnabled`), `Occlusion` (alias for `OcclusionEnabled`), `OcclusionRadius` (unused),
`Transmission` (unused, derived from occlusion now), `ElapsedTime` (alias for `Time`), and
`Update()` (empty no-op, "no longer needs to exist").

`GameObject.PlaySound(...)` / `GameObject.StopAllSounds(fadeOutTime)` are the positional
convenience wrappers, covered in `component-library.md`. They ultimately call into the same
`Sound.Play` / `SoundHandle.Stop` machinery described here.

### 5. Audio processors

`Sandbox.Audio.AudioProcessor` is the base for DSP effects attached to a mixer. Public
surface is small:

```csharp
public abstract partial class AudioProcessor
{
    public bool Enabled { get; set; } = true;
    [Range(0,1)] public float Mix { get; set; } = 1;
    protected Transform Listener { get; }   // current listener position, set per-frame
    protected virtual void ProcessSingleChannel( AudioChannel channel, Span<float> input );
}
```

Attach/manage on a `Mixer`:

```csharp
mixer.AddProcessor( new PitchProcessor { Pitch = 1.5f } );
mixer.RemoveProcessor( someProcessor );
mixer.ClearProcessors();
AudioProcessor[] all = mixer.GetProcessors();
var pitch = mixer.GetProcessor<PitchProcessor>();
int count = mixer.ProcessorCount;
```

`Mix` controls dry/wet crossfade against the pre-processor signal; `Mix <= 0` skips the
processor entirely for that frame. Processors are applied per-listener, after all voices for
that mixer are summed (`Mixer.ApplyProcessors`).

Built-in processors, all `[Expose]` sealed classes under `Systems/Audio/Processors/`:

| Type | Properties | Notes |
|---|---|---|
| `DelayProcessor` | `Delay [0,1]`, `Volume [0,1]` | Wraps a native delay DSP |
| `PitchProcessor` | `Pitch [0.5, 2.0]` | Wraps a native pitch-shift DSP |
| `HighPassProcessor` | `Cutoff [0,1]` | Simple one-pole filter. Source doc comment: **"Just a test - don't count on this sticking around"** |
| `LowPassProcessor` | `Cutoff [0,1]` | Same one-pole filter shape, same "don't count on this sticking around" comment |

Custom processors subclass `AudioProcessor` (or the generic `AudioProcessor<TState>` used by
the built-ins for per-listener state) and must be `[Expose]`d to survive
`Serialize()`/`Deserialize()`, which round-trips through `TypeLibrary.Create<AudioProcessor>(typeName)`
by class name. An unrecognized processor type in saved mixer settings is dropped with a
warning at load, not an error.

### 6. `StopAll(fade)` and mixer lifetime

Two different `StopAll`s exist, don't confuse them:

```csharp
Sound.StopAll( float fade );        // static, stops every active SoundHandle in the process
mixer.StopAll( float fade );        // instance, stops only handles whose TargetMixer == this mixer
```

`Mixer.StopAll` is a reference-equality filter (`handle.TargetMixer != mixer`) against the
exact `Mixer` instance, it does **not** cascade to child buses. Stopping a parent bus does
not stop sounds explicitly targeting one of its children.

Mixer tree lifetime: `Mixer.ResetToDefault()` throws away the whole tree (`Master?.Clear()`,
which destroys every processor first so DSP slots don't leak) and rebuilds the four default
buses. `Mixer.LoadFromSettings` does the same reset, then deserializes saved settings on top,
then ensures a `Voice` mixer exists even if the saved settings predate it. Don't hold onto a
`Mixer` reference across a settings reload, look it up again by name or GUID.

---

## Part 2: Localization

### 7. `Languages` and `LanguageInformation`

`Sandbox.Localization.Languages` is a static catalog of ~29 supported languages (English,
French, German, Simplified/Traditional Chinese, Arabic, Pirate, etc.):

```csharp
public static IEnumerable<LanguageInformation> List { get; }
public static LanguageInformation Find( string key );  // matches Abbreviation, then Title, case-insensitive
```

```csharp
public class LanguageInformation
{
    public string Title { get; }         // "French"
    public string Abbreviation { get; }  // "fr" (ISO 639-1, optional region suffix like "es-419")
    public string Parent { get; }        // e.g. Pirate's Parent is "en", Portuguese-Brazil's is "pt"
    public bool RightToLeft { get; }     // true only for Arabic
}
```

`Parent` is metadata only, nothing in the engine currently walks it to build a fallback
chain. The only fallback that actually happens is English, see below.

`Game.Language` (a `LanguageContainer`) exposes the live, currently-active state:

```csharp
Game.Language.SelectedCode;   // Application.LanguageCode, e.g. "fr"
Game.Language.Current;        // resolved LanguageInformation
Game.Language.GetPhrase( "ui.play" );
```

`Sandbox.Language` (static) is the shortcut for the same thing (`Language.GetPhrase(...)`,
`Language.SelectedCode`, `Language.Current`), usable from anywhere without holding a
`Game.Language` reference.

### 8. `Phrase` and `PhraseCollection`

`Phrase` wraps one localized string and pre-splits it on `{token}` placeholders at
construction:

```csharp
public class Phrase
{
    public Phrase( string value );
    public string Render();                                   // returns Value verbatim, no substitution
    public string Render( Dictionary<string, object> data );   // substitutes {Key} -> data["Key"]
}
```

If `value` has no `{`/`}` pair, `Parts` stays `null` and `Render(data)` short-circuits to
`Value`. If a placeholder's key isn't found in `data` (or `data` is null), the literal
`{Key}` text is left in the output rather than throwing or silently dropping it, useful as a
visible signal that a translation is missing a substitution the base string expects.

`PhraseCollection` is a flat, case-insensitive key store:

```csharp
public class PhraseCollection
{
    public void Set( string key, string value );
    public string GetPhrase( string phrase, Dictionary<string, object> data = null );
}
```

`GetPhrase` on an unknown key returns the key itself unchanged, never throws, never returns
null. This is also how missing translations degrade: you see the raw token in-game instead of
a crash.

### 9. On-disk layout

Verified from `Project.GetLocalizationPath()` (`RootFileSystem?.GetFullPath( "Localization" )`),
`PackageManager.ActivePackage` mounting `localPackage.LocalizationPath` into
`ActivePackage.Localization`, and `game/addons/tools/Code/Utility/LocalizationTools.cs`.

A project's localization lives at `<project root>/Localization/`, with one subfolder per
language abbreviation, lowercase, holding any number of `*.json` files each shaped as a flat
`Dictionary<string,string>`:

```
MyProject/
└── Localization/
    ├── en/
    │   ├── ui.json
    │   └── dialogue.json
    ├── fr/
    │   ├── ui.json
    │   └── dialogue.json
    └── de/
        └── ui.json
```

```json
// Localization/en/ui.json
{
    "ui.play": "Play",
    "ui.settings": "Settings",
    "hello.player": "Hello {PlayerName}!"
}
```

Filenames should match across language folders (the `localization.build` editor `ConCmd`
walks every `en/*.json` file and produces a same-named file per other language). The lookup
that populates a language (`FileSystem.FindFile( shortName, "*.json" )` in
`LanguageContainer.AddFromPath`) is **not recursive**, files nested in a subfolder under
`Localization/<lang>/` are silently skipped.

`LanguageContainer.Tick()` always loads `en/` first as the fallback baseline, then loads the
selected language on top, key by key. So a language folder that's missing `ui.settings`
falls back to whatever English defines for that key, and only falls back to the raw key
string if English is also missing it. This also means an incomplete translation is never a
hard error, just a silent partial fallback to English.

Localization files are filesystem-watched (`FileWatch`); editing a `.json` under
`Localization/` while the game is running triggers `Refresh()` and a reload on the next tick,
no restart needed.

### 10. `#`-prefixed tokens in Razor/Panel UI

Any UI text field that starts with `#` (and is longer than one character) is treated as a
localization token instead of literal text. This is implemented independently in a couple of
places, all doing the same strip-and-lookup:

**`Label.Text`** (`Sandbox.UI` control, what Razor `<label>` uses under the hood):

```csharp
[Parameter]
public virtual string Text
{
    set
    {
        if ( Tokenize && value.Length > 1 && value[0] == '#' )
            value = Language.GetPhrase( value[1..] );
        ...
    }
}
public bool Tokenize { get; set; } = true;   // set false to treat text starting with # literally
```

```razor
<label text="#ui.play" />
<label>@("#ui.play")</label>
```

**CSS `content`**: a stylesheet rule like `content: "#ui.tooltip.close";` gets the same
`#`-strip-and-lookup treatment in `Label.PreLayout`, so localized pseudo-content works too.

**3D world text**: `TextRenderer`'s `TextScope.Text` setter applies the identical check
(`Game.Language.GetPhrase(token)`), so `TextRenderer.Text = "#sign.welcome";` on a
`TextRenderer` component localizes the same way as UI labels.

The `lang.showkeys` ConVar (`Language.DisplayKeys`, backed by `[ConVar]`) forces every lookup
to return the raw token instead of the resolved phrase, useful for auditing which strings are
actually going through localization versus hardcoded literal text. Toggling it fires
`UISystem.OnLanguageChanged()` so visible UI updates immediately.

---

## Gotchas

- **`Spatializing` vs `Spacializing`**: both compile. `Spatializing` is current; `Spacializing`
  is `[Obsolete]` and forwards to it. Only ever write `Spatializing`.
- Occlusion is represented three different shapes across three types: `Mixer.Occlusion` is a
  `float [0,1]` simulation strength, `SoundHandle.OcclusionEnabled` and `SoundEvent.OcclusionEnabled`
  are plain `bool`s. Don't assume they share a type just because they share a name.
- `Mixer.BlockingTags`/`IgnoredTags` replace the older `OverrideOcclusion`/`OcclusionTags`
  pair, but `Deserialize` still reads the legacy keys (and legacy `OcclusionEnabled`/`ReverbEnabled`
  bools, forcing the corresponding float to `0` if either was explicitly `false`) for old saved
  mixer settings. Both code paths are live.
- `Mixer.StopAll(fade)` only stops handles whose `TargetMixer` is reference-equal to that
  exact mixer. It does not cascade into child buses; use `Sound.StopAll(fade)` for everything,
  or walk `GetChildren()` yourself.
- `HighPassProcessor` and `LowPassProcessor` carry a source doc comment reading "Just a test -
  don't count on this sticking around." Treat them as unstable, not a committed API surface.
- Outside the editor, `Mixer.FinishMixing` silently multiplies a mixer's effective volume by
  `Preferences.MusicVolume` or `Preferences.VoipVolume` if its `Name` case-insensitively
  equals `"music"` or `"voice"`. Renaming your music bus away from "Music" opts it out of the
  user's music volume slider.
- `PhraseCollection.GetPhrase` on a missing key returns the key itself, not `null` and not an
  exception. A raw dotted token appearing on screen almost always means a missing localization
  entry, not a bug in the lookup.
- `Phrase.Render(data)` leaves `{Key}` literally in the output when `data` doesn't contain
  that key, rather than dropping it or throwing.
- Localization file discovery is non-recursive per language folder
  (`Localization/<lang>/*.json` only, one level deep). Nested subfolders are invisible to the
  loader.
- `LanguageInformation.Parent` (e.g. Pirate's parent is English) is metadata only. The engine
  does not currently chain translations through it, only `en/` is ever used as the automatic
  fallback for every language.
- `Label.Tokenize` defaults to `true`; if you need to display literal text that happens to
  start with `#` (a hashtag, a hex color as a string, etc.) in a label, set `Tokenize = false`
  or it will be treated as a lookup key.
- `SoundEvent.UI = true` auto-routes to a mixer literally named `"UI"` if `DefaultMixer` is
  unset. If that bus doesn't exist in your project's mixer settings, the sound falls through
  to `Mixer.Default` instead, silently, no warning.
