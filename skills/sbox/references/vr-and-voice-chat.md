<!--
  s&box Skill : vr-and-voice-chat.md

  VR rig, controllers and haptics, plus voice chat capture, transmission and playback.

  Author  : Kyle (fobiat) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# VR and Voice

VR input/rendering and voice chat, both thinner public surfaces than the rest of the engine. Read out of the engine source at version **26.08.05** (`sbox-public`): `engine/Sandbox.Engine/Platform/VR/**`, `engine/Sandbox.Engine/Systems/Input/VR/**`, `engine/Sandbox.Engine/Scene/Components/VR/**`, `engine/Sandbox.Engine/Systems/Input/Haptics/**`, `engine/Sandbox.Engine/Scene/Components/Audio/VoiceComponent.cs`, `engine/Sandbox.Engine/Utility/VoiceManager.cs`, `engine/Sandbox.Engine/Systems/Audio/Mixer.List.cs`, `engine/Sandbox.Engine/Systems/Audio/SoundStream.cs`, `engine/Sandbox.Engine/Game/Preferences.cs`. Namespace for everything VR is `Sandbox.VR`; most of it is `internal`, only a narrow slice is public.

---

## Part 1: VR

### 1. Detecting VR

The real check is `Game.IsRunningInVR`, a thin wrapper over `VRSystem.IsActive` (`engine/Sandbox.Engine/Game/Game/Game.cs:81`):

```csharp
public static bool IsRunningInVR => VRSystem.IsActive;
```

Every shipped VR component in the engine gates on the same three things, in this order (`VrAnchor.cs`, `VrHand.cs`, `VrTrackedObject.cs`, `VrModelRenderer.cs`):

```csharp
protected override void OnUpdate()
{
    if ( !Enabled || Scene.IsEditor || !Game.IsRunningInVR )
        return;

    if ( IsProxy )
        return;

    // VR-only logic here
}
```

Do the same in your own components: check `Game.IsRunningInVR` before touching anything under `Input.VR`, and check `IsProxy` before driving VR-only state, since VR input only makes sense for the locally-controlled player.

`Input.VR` is `VRInput.Current`, which is `null` until VR activates (`engine/Sandbox.Engine/Systems/Input/Input.cs:14`). Don't read `Input.VR.Head` or anything off it without the `Game.IsRunningInVR` guard first, it'll null-ref.

VR only ever activates when there's a headset and the game opted in: `VRSystem.Init()` bails on `-novr`, on headless, and on standalone builds unless `Standalone.Manifest.IsVRProject` is set, then only proceeds if `Instance.HasHeadset()` returns true. There is no code path that turns VR on for a game that isn't a declared VR project.

A non-VR game is unaffected by default: nothing in the object model requires VR, and every built-in VR component (`VRAnchor`, `VRHand`, `VRTrackedObject`, `VRModelRenderer`) is a no-op when `Game.IsRunningInVR` is false.

### 2. The VR rig: HMD pose, play space, anchoring

`VRInput` (`Sandbox.VR.VRInput`, reached via `Input.VR`) is the root of VR state:

| Member | Type | Notes |
|---|---|---|
| `Anchor` | `Transform` | Center of the VR play area in world space. Set this to move the whole play space. |
| `Head` | `Transform` | HMD pose, `Anchor.ToWorld( localHeadPose )`. Averaged from both eye poses, not a raw device pose. |
| `LeftHand` / `RightHand` | `VRController` | See section 3. |
| `TrackedObjects` | `IReadOnlyList<TrackedObject>` | All tracked devices (currently just both hands; the engine comment notes trackers aren't wired up because Quest doesn't support them). |
| `Scale` | `float` | Player scale in the world. Setting it to `2` makes the player twice as big; internally flips `VRSystem.WorldScale = 1 / value`. |

Anchoring pattern, the `VRAnchor` component (`Scene/Components/VR/VrAnchor.cs`):

```csharp
public class VRAnchor : Component
{
    protected override void OnUpdate()
    {
        if ( !Enabled || Scene.IsEditor || !Game.IsRunningInVR ) return;
        if ( IsProxy ) return;

        Input.VR.Anchor = GameObject.WorldTransform;
    }
}
```

Drop this on the GameObject that represents the player's play-space origin (e.g. the body root), and every VR pose (`Head`, `LeftHand.Transform`, hand joints) is reported relative to it automatically, since `VRController.Transform`, `TrackedObject.Transform` and `VRInput.Head` all route through `Input.VR.Anchor.ToWorld(...)`.

There is no public per-eye camera API. Stereo rendering (`VRSystem.Rendering.cs`) is entirely `internal`: eye render poses, IPD, clip planes and the compositor submit path are engine-owned. You place a normal `CameraComponent` in the scene; the engine renders both eyes from it when `Game.IsRunningInVR` is true. Don't invent a `Camera.LeftEye` / `RightEye` API, it doesn't exist publicly.

`VRInput.IPDInches`-equivalent is not exposed on `VRInput`; IPD lives on the internal `VRSystem.IPDInches` and is only surfaced through the `vr_info` console command.

### 3. Controllers: pose, buttons, analog inputs

`VRController : TrackedObject` (`sealed partial record`, `Systems/Input/VR/VRController.cs`), reached as `Input.VR.LeftHand` / `Input.VR.RightHand`.

Pose (inherited from `TrackedObject`, `Systems/Input/VR/TrackedObject.cs`):

| Member | Type | Notes |
|---|---|---|
| `Transform` | `Transform` | Grip pose, centered on the palm. World space, already through the anchor. |
| `AimTransform` | `Transform` | Aim pose, points forward. Use this for raycasting/pointing, not `Transform`. |
| `Velocity` | `Vector3` | Local velocity, computed frame-to-frame. |
| `AngularVelocity` | `Angles` | Degrees/second. |
| `Active` | `bool` | False if the device isn't currently tracked; `Transform` won't update while false. |
| `Role` | `TrackedDeviceRole` | `LeftHand`, `RightHand`, `Head`, plus a long list of body-tracker roles (`Waist`, `Chest`, `LeftKnee`, etc.) that exist in the enum but aren't populated by anything shipped. |
| `Type` | `TrackedDeviceType` | Device category. |

Inputs, all on `VRController` (`Systems/Input/VR/VRController.Inputs.cs`):

| Member | Type | What it is |
|---|---|---|
| `Trigger` | `AnalogInput` | Index trigger, `0..1`. |
| `Grip` | `AnalogInput` | Grip/squeeze, `0..1`. |
| `Joystick` | `AnalogInput2D` | Primary thumbstick, `Vector2`. |
| `JoystickPress` | `DigitalInput` | Thumbstick click. |
| `ButtonA` | `DigitalInput` | Primary face button (A on most, X on Oculus Touch). |
| `ButtonB` | `DigitalInput` | Secondary face button (B / Y). |

`AnalogInput` and `DigitalInput` both implicitly convert to `float`/`bool` respectively, and both expose `.Value`/`.IsPressed`, `.Delta`, and `.Active` (false when the underlying action isn't bound on this device, in which case the value is always 0/false):

```csharp
if ( Game.IsRunningInVR && Input.VR.RightHand.Trigger.Value > 0.75f )
{
    // pulled the trigger
}

if ( Input.VR.RightHand.ButtonA.IsPressed ) { }
```

**That's the entire button surface.** There is no `GripButton`, `ButtonX`/`ButtonY` alias, `MenuButton`, `SystemButton`, or per-controller-model button name in the public API. Don't invent them: everything routes through the six members above regardless of what the physical headset calls its buttons.

There is no bridge from VR controller buttons into the normal `Input` action-name system (`Input.Down( "attack1" )` and friends). VR buttons are read directly off `Input.VR.LeftHand` / `.RightHand`, not through `InputAction` bindings. The one place they cross is `WorldInput` (`Scene/Components/UI/WorldInput.cs`), which manually maps `hand.Trigger.Value > 0.75f` to a "mouse left pressed" state for driving `WorldPanel` UI when `Game.IsRunningInVR` is true.

Hand tracking: `VRController.IsHandTracked` is true when the runtime is reporting full finger tracking instead of a physical controller. Per-finger curl (`VRSystem.GetFingerCurl`, `FingerValue` enum) and joint data (`VRController.GetJoints( MotionRange )`, returning `VRHandJointData[]`) exist for driving hand models; see `VRHand` component below for the built-in consumer. `MotionRange.Hand` estimates a bare hand pose, `MotionRange.Controller` estimates how the hand wraps a held controller.

Built-in rig components (`[Category("VR")]` in the inspector), all no-ops outside VR:

- `VRAnchor`: play-space anchoring, see section 2.
- `VRTrackedObject`: drives a GameObject's transform from `Head`/`LeftHand`/`RightHand`, grip or aim pose, position/rotation/both, world or anchor-relative.
- `VRHand`: poses a `SkinnedModelRenderer`'s finger bones from `GetJoints( MotionRange )`, matching bone names like `finger_index_1_L`.
- `VRModelRenderer`: assigns the device-specific controller model to a `ModelRenderer` (`hand.GetModel()`).

`VRController.GetModel()` returns `Model.Cube` in this engine build, there is no controller-specific mesh wired up. Don't claim it returns a HTC Vive/Quest controller model; that's not implemented here.

### 4. Haptics

Two ways to trigger vibration, both on `VRController`:

```csharp
// Raw pulse
Input.VR.RightHand.TriggerHaptics( HapticEffect.SoftImpact );

// Stop everything
Input.VR.RightHand.StopAllHaptics();
```

`TriggerHaptics( HapticEffect effect, float lengthScale = 1, float frequencyScale = 1, float amplitudeScale = 1 )` plays a `HapticEffect` (`record class`, `Systems/Input/Haptics/HapticEffect.cs`), which bundles a `ControllerPattern`, `LeftTriggerPattern`, `RightTriggerPattern` (each a `HapticPattern`, defined by length + frequency/amplitude curves). Built-in effects (`HapticEffect.Effects.cs`): `SoftImpact`, `HardImpact`, `Rumble`, `RumbleLeftTrigger`, `RumbleRightTrigger`, `Heartbeat`.

Lower-level: `controller.Rumble( duration, frequency, amplitude )` is `[Obsolete]` in favor of `TriggerHaptics`, but still functional; it clamps `duration` to `0..10`, `frequency` to `0..320`Hz, `amplitude` to `0..1` and throws `ArgumentOutOfRangeException` outside those ranges.

### 5. VR overlays / UI in VR

`Sandbox.VR.VROverlay` exists in source but is marked `[Obsolete( "Unsupported by OpenXR. Please use WorldPanel." )]` (`Platform/VR/Overlay/VROverlay.cs`). Every member (`Visible`, `Transform`, `Width`, `Curvature`, `Color`, `Texture`) is a stub that does nothing, the class only survives for source compatibility with old SteamVR-era code. **Do not use it and do not teach it as a working API.**

The real answer for VR UI is `WorldPanel` plus `WorldInput` (`Scene/Components/UI/WorldInput.cs`), the same in-world UI system used for non-VR world-space panels. `WorldInput` auto-detects VR:

```csharp
if ( Game.IsRunningInVR )
{
    var hand = (VRHandSource == VRHand.HandSources.Left) ? Input.VR.LeftHand : Input.VR.RightHand;
    WorldPanelInput.MouseLeftPressed = hand.Trigger.Value > 0.75f;
    WorldPanelInput.MouseRightPressed = false;
    WorldPanelInput.MouseWheel = hand.Joystick.Value;
}
```

Put a `WorldInput` component on the controller GameObject (or anywhere with the right forward vector), set `VRHandSource`, and any `WorldPanel` in range becomes interactive with the trigger as click and the joystick as scroll. There is no separate "VR UI" component; it's the same world-panel system with VR as one of its input sources.

### 6. What is NOT available

State plainly, don't guess at any of these:

- No per-eye camera API (`Camera.LeftEye`, eye textures, render target access). Stereo rendering is internal.
- No working `VROverlay`. It's an obsolete stub; use `WorldPanel`.
- No public IPD accessor on `VRInput` (it exists internally on `VRSystem`, surfaced only via the `vr_info` concmd).
- No button names beyond `Trigger`, `Grip`, `Joystick`, `JoystickPress`, `ButtonA`, `ButtonB`. No menu/system button, no grip-button-as-digital-input, no per-manufacturer button aliases.
- No controller-specific 3D models. `VRController.GetModel()` returns `Model.Cube`.
- No bridge from VR buttons into the `InputAction`/`Input.Down("action")` system. VR input is read directly off `Input.VR`.
- No public tracker support beyond the two hands, despite `TrackedDeviceRole` listing waist/chest/knee/etc roles. `VRInput.TrackedObjects` only ever contains left and right hand in this build.
- `IsLeftHandDominant` and `ControllersAreDrawing` on `VRInput` are both `[Obsolete]` and hardcoded to return fixed values (`false`). Don't rely on them.

---

## Part 2: Voice

### 7. How voice chat works

Voice is a `Component` (class name `Voice`, `[Title("Voice Transmitter")]`, `Scene/Components/Audio/VoiceComponent.cs`), built on Steam's voice API. Add it to a player prefab:

```csharp
[Property] public Voice.ActivateMode Mode { get; set; } = Voice.ActivateMode.PushToTalk;
```

`Voice.ActivateMode` has three values: `AlwaysOn`, `PushToTalk` (hold `PushToTalkInput`, default action name `"voice"`), `Manual` (you drive it by setting `Voice.IsListening` yourself).

What the engine does for you:

- Captures mic audio through Steam (`VoiceManager`, wraps `ISteamUser.StartVoiceRecording`/`GetVoice`/`DecompressVoice`), at a fixed `VoiceManager.SampleRate` of 44100.
- Compresses and ships the buffer to other clients as an unreliable RPC (`Msg_Voice`, `[Rpc.Broadcast( NetFlags.OwnerOnly | NetFlags.UnreliableNoDelay )]`).
- Decompresses and plays it back through a `SoundStream` per `Voice` component instance, including on the sending client's own proxy-side instance (loopback path, see below).
- Drives lip-sync morphs on a `SkinnedModelRenderer` if you wire `Renderer` and leave `LipSync` on.

What a game has to do:

- Add the `Voice` component to the player and pick an `ActivateMode`.
- If not `AlwaysOn`/`PushToTalk`, flip `Voice.IsListening` yourself for `Manual` mode.
- Respect `Preferences.VoiceMode` (see section 10), the component already checks it for you in `IsListening`, but the player-facing settings UI is yours to build.

Local-only readback:

| Member | Type | Notes |
|---|---|---|
| `IsRecording` | `bool` | True only on the local, currently-recording instance. Meaningless read on a proxy. |
| `IsListening` | `bool` | Whether this instance should currently be capturing (checks `Preferences.VoiceMode` and `Mode` internally). `false` for any `IsProxy` instance. |
| `Amplitude` | `float` | Loudness of the sound currently playing back through this component, works for proxies too since it reads off the `SoundHandle`. |
| `LastPlayed` | `RealTimeSince` | Time since this component last received/played voice data. Also proxy-safe. |
| `LaughterScore` / `Visemes` | `float` / `IReadOnlyList<float>` | Lip-sync analysis of the currently-playing audio. |

`Volume` (`[Property] float`, default `1`), `WorldspacePlayback` (`[Property] bool`, default `true`, positions the sound in 3D at `WorldPosition` with occlusion; when `false` it plays as `ListenLocal`, unspatialized) and `Loopback` (`[Property] bool`, default `false`, plays your own voice back to yourself) are the transmit-side knobs.

### 8. Speaking indicator: reading per-connection voice state

There's no dedicated "who is talking" API on `Connection`. Read it directly off each player's `Voice` component instead, since `Amplitude` and `LastPlayed` both update correctly for proxies (remote players), not just the local one:

```csharp
public sealed class SpeakingIndicator : Component
{
    [Property] public Voice Voice { get; set; }

    protected override void OnUpdate()
    {
        bool isSpeaking = Voice.IsValid() && Voice.LastPlayed < 0.2f && Voice.Amplitude > 0.02f;
        // drive a UI icon / highlight from isSpeaking
    }
}
```

`0.2f` matches the freshness window the engine itself uses internally for viseme data (`UpdateMorphs`, `VoiceComponent.cs`), a reasonable "currently talking" cutoff. Tune the amplitude threshold to taste; the engine doesn't define one publicly.

### 9. Routing through the mixer, positional vs global voice

The engine creates a `Voice` mixer as a child of `Master` alongside `Music`, `Game`, `UI` (`Systems/Audio/Mixer.List.cs`, `ResetToDefault()`). It's reachable as `Mixer.Voice`.

`Voice.VoiceMixer` (`[Property, ParentMixer("Voice")] MixerHandle`) lets you route an individual `Voice` component's output to a mixer other than the default, but it's enforced to be a descendant of `Mixer.Voice`: if you set something that isn't, `OnEnabledInternal` resets it back to `Mixer.Voice`. Read the resolved mixer through `Voice.TargetMixer` (`Mixer`, falls back to `Mixer.Voice` if `VoiceMixer` is unset).

Positional vs global is the `WorldspacePlayback` flag from section 7:

```csharp
if ( WorldspacePlayback )
{
    sound.Position = WorldPosition;
    sound.OcclusionEnabled = true;
}
else
{
    sound.ListenLocal = true;   // unspatialized, plays "on top of" the listener
    sound.Position = Vector3.Forward * 10.0f;
}
```

`Distance` (`[Property] float`, default `15_000`) and `Falloff` (`[Property] Curve`) control attenuation over distance for the worldspace case, both forwarded straight to the underlying `SoundHandle`.

### 10. Muting, volume, permissions

- **Global volume**: `Preferences.VoipVolume` (`ConVar "voip_volume"`, `0..1`, saved), the player's own volume slider. Not applied per-speaker, it's a user preference.
- **Global on/off/mode**: `Preferences.VoiceMode` (`ConVar "voip_mode"`, saved), an enum: `PushToTalk` (default), `OpenMicrophone`, `Disabled`. `Voice.IsListening` already checks this: `Disabled` always returns `false`, `OpenMicrophone` follows the component's `ActivateMode`, and otherwise the component behaves as push-to-talk regardless of its own `Mode` setting.
- **Per-listener muting / permission**: override two `protected virtual` methods on a `Voice` subclass:

```csharp
public sealed class TeamVoice : Voice
{
    protected override IEnumerable<Connection> ExcludeFilter()
        => Connection.All.Where( c => !IsSameTeam( c ) );   // never even sent to them

    protected override bool ShouldHearVoice( Connection connection )
        => IsSameTeam( connection );                        // received but discarded
}
```

`ExcludeFilter()` is checked on the sender and wraps the broadcast in `Rpc.FilterExclude(...)`, so excluded connections never receive the packet at all. `ShouldHearVoice( Connection )` is checked on the receiver inside the `Msg_Voice` RPC handler, so it's a cheaper per-listener override when you don't need to save bandwidth. Both default to "everyone can hear."

There is no built-in server-side global mute list or admin-mute API; build that on top of `ShouldHearVoice`/`ExcludeFilter` yourself.

### 11. Gotchas

- `Input.VR` is `null` until `Game.IsRunningInVR` is true. Guard every access, including in components that only sometimes run in VR.
- `VRController.Transform` is the grip pose. If you're aiming or raycasting, you want `AimTransform`, not `Transform`; using the wrong one gives a pose centered on the palm instead of pointed where the controller is aimed.
- `VROverlay` compiles and has a full-looking API, but every member is a no-op stub. Nothing you set on it has any effect. Use `WorldPanel` instead.
- `VRController.GetModel()` always returns `Model.Cube` in this build. Don't ship on the assumption you'll get a real controller mesh; supply your own via `VRModelRenderer.ModelRenderer` if you need a visible controller.
- Voice capture and playback are Steam-dependent (`ISteamUser`). `VoiceManager.IsValid` is `steamUser.IsValid`; if that's false (no Steam context, e.g. certain headless/test configurations), `Voice.OnUpdate` silently skips recording, no exception, no capture.
- `Voice.IsRecording` and `Voice.IsListening` only reflect real state on the locally-owned, non-proxy instance. Reading them on a remote player's `Voice` component to build a speaking indicator gives you nothing useful, use `Amplitude`/`LastPlayed` instead (section 8).
- Only one `Voice` component can be the active recorder at a time process-wide (`static Voice singleRecorder`). Adding a second enabled `Voice` component to the local player and enabling both won't record from both, whichever last started recording wins and the other's `OnVoice` never fires.
- `VoiceComponent`'s `WorldspacePlayback = false` path still sets a `Position` (`Vector3.Forward * 10`) even though it's `ListenLocal`; that's for stereo left/right placement, not real 3D positioning. Don't read it as a worldspace position when `WorldspacePlayback` is off.
- Setting `Voice.VoiceMixer` to a mixer that isn't a descendant of `Mixer.Voice` is silently corrected back to `Mixer.Voice` on the next enable, it won't throw or warn.
