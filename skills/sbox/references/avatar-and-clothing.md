# Avatar and Clothing

Covers the Citizen avatar, the `Clothing` GameResource, `ClothingContainer`, dressing a
`SkinnedModelRenderer` (including the built-in `Dresser` component), how outfit choices
travel across the network, and body groups/material groups as they apply to clothing.
Read out of engine source at version 26.08.05:
`engine/Sandbox.Engine/Game/Avatar/Clothing.cs`,
`engine/Sandbox.Engine/Game/Avatar/ClothingContainer.cs`,
`engine/Sandbox.Engine/Game/Avatar/ClothingContainer.Dressing.cs`,
`engine/Sandbox.Engine/Game/Avatar/Avatar.cs`,
`engine/Sandbox.Engine/Game/Avatar/AvatarRandomizer.cs`,
`engine/Sandbox.Engine/Scene/Components/Game/Dresser.cs`,
`engine/Sandbox.Services/Meta/ClothingMetaData.cs`,
`engine/Sandbox.System/ConVar/ConVarAttributes.cs`,
`engine/Sandbox.Engine/Systems/Networking/PlayerInfo/ConnectionInfoManager.cs`,
`engine/Sandbox.Engine/Scene/Components/Game/PlayerController/PlayerController.Utility.cs`,
`game/addons/menu/Code/AvatarEditor/AvatarEditManager.cs`.

---

## 1. The Citizen avatar

s&box's standard player body is the Citizen model. Verified asset paths, under
`game/addons/citizen/Assets/models/`:

| Model | Path |
|---|---|
| Citizen (default) | `models/citizen/citizen.vmdl` |
| Citizen (SFM variant) | `models/citizen/citizen_sfm.vmdl` |
| Human male | `models/citizen_human/citizen_human_male.vmdl` |
| Human female | `models/citizen_human/citizen_human_female.vmdl` |
| Mannequin | `models/citizen_mannequin/mannequin.vmdl` |

Clothing items live under `models/citizen_clothes/...` as `.clothing` resources, e.g.
`models/citizen_clothes/underwear/y_front_pants/y_front_pants_white.clothing` (the default
underwear item, referenced directly by path in `ClothingContainer.Dressing.cs`).

The dressing system (`ClothingContainer.Apply`) distinguishes "citizen" from "human" purely
by model name:

```csharp
static bool DetermineHuman( SkinnedModelRenderer b, bool defaultValue = false )
{
    if ( b?.Model is null ) return defaultValue;
    var model = b.Model.BaseModel ?? b.Model;
    return !model.Name.Contains( "citizen.vmdl", StringComparison.OrdinalIgnoreCase );
}
```

Any renderer whose model isn't `citizen.vmdl` (or based on it) is treated as "human", which
changes which clothing fields get used (`Model` vs `HumanAltModel`/`HumanAltFemaleModel`,
`SkinMaterial` vs `HumanSkinMaterial`, etc.) and turns on automatic underwear (section 4).

There's no separate "Avatar" component. An avatar is just a `SkinnedModelRenderer` on the
Citizen (or human) model, dressed by applying a `ClothingContainer` to it.

---

## 2. Clothing (GameResource)

```csharp
[AssetType( Name = "Clothing Definition", Extension = "clothing", Category = "citizen",
    Flags = AssetTypeFlags.IncludeThumbnails, IconColor = "#fdea60" )]
public sealed partial class Clothing : GameResource
```

One `.clothing` asset is one item (a hat, a shirt, a pair of boots). Key properties:

| Property | Type | Purpose |
|---|---|---|
| `Title`, `Subtitle` | `string` | Display name/subtitle for menu UI |
| `Category` | `ClothingCategory` | Slot category (see enum below) |
| `Tags` | `string` (space-separated) | Free-form tags, e.g. `"female"`; drives `ConditionalModels` and human alt-model selection |
| `ConditionalModels` | `Dictionary<string,string>` | tag → model path override, checked against every other worn item's tags |
| `Model` | `string` (`.vmdl` path) | Model bonemerged onto the citizen when worn |
| `HumanAltModel` / `HumanAltFemaleModel` | `string` (`.vmdl` path) | Model used instead of `Model` when the target body is "human" |
| `SkinMaterial` / `EyesMaterial` | `string` (`.vmat` path) | Replaces the body's `"skin"`/`"eyes"` material override (citizen skins) |
| `MaterialGroup` | `string` | Material group applied to this item's own model |
| `HeelHeight` | `float` (0-1) | Feeds the `scale_heel` animgraph parameter; highest value across all worn items wins |
| `SlotsUnder` / `SlotsOver` | `Slots` (`[BitFlags]`) | Which body slots this item occupies, on the inner/outer layer |
| `HideBody` | `BodyGroups` (`[BitFlags]`) | Which body regions to hide (Head/Chest/Legs/Hands/Feet) while worn |
| `AllowTintSelect` | `bool` | Lets the wearer recolor this item |
| `TintSelection` | `Gradient` | Color ramp sampled by the tint value |
| `TintDefault` | `float` (0-1) | Default position on the ramp |
| `SteamItemDefinitionId` | `int?` | Ties this asset to a Steam Inventory item; gates `HasPermissions()` |
| `HasHumanSkin` (feature) | `bool` | This item *replaces the whole human body*, not just an attachment |
| `HumanSkinModel`/`HumanSkinMaterial`/`HumanEyesMaterial`/`HumanSkinBodyGroups`/`HumanSkinMaterialGroup`/`HumanSkinTags` (feature) | | Body swap data used only when `HasHumanSkin` is set |

`ClothingCategory` is a large flat enum (`Hat`, `Hair`, `Footwear`, `Bottoms`, `Tops`,
`Underwear`, `Bra`, `FacialHairBeard`, `GlassesSun`, ... over 80 values) covering both broad
slots and fine-grained subtypes. `Slots` is a `[Flags]` enum of ~29 body regions (`HeadTop`,
`Chest`, `LeftWrist`, `Waist`, ...) used only for worn-together compatibility, not rendering.
`BodyGroups` (the clothing-level one, distinct from the render-level bodygroup system in
section 6) is a 5-bit flag set: `Head`, `Chest`, `Legs`, `Hands`, `Feet`.

Two methods worth knowing:

```csharp
// Resolves the model to bonemerge, taking conditional overrides and human/female tags into account
public string GetModel( IEnumerable<Clothing> clothingList, TagSet tagset );

// Two items conflict if they share an outer-slot bit, share an under-slot bit,
// or are both HasHumanSkin. Same item is never wearable with itself.
public bool CanBeWornWith( Clothing target );
```

`Sandbox.Services.ClothingMetaData` (in `Sandbox.Services`, shared between the website and
the engine) is a parallel DTO with the same enums, used to read metadata out of a compiled
`.clothing_c` package without loading the full `GameResource`. Its `Category`/`Slots`/
`BodyGroups` enums must stay numerically identical to `Clothing`'s for compiled assets to
parse; treat them as the same thing under two names.

---

## 3. ClothingContainer

`ClothingContainer` is a plain (non-Component) class that holds an outfit: a list of
`ClothingEntry` plus body customization fields. It refuses to hold two items that can't be
worn together.

```csharp
public float Height { get; set; } = 0.5f;      // 0-1, feeds scale_height
public float Age { get; set; } = 0.5f;         // 0-1, feeds skin_age shader attribute
public float Tint { get; set; } = 0.5f;        // 0-1, feeds skin_tint shader attribute
public bool PrefersHuman { get; set; }         // wants a human body over citizen, where offered
public List<ClothingEntry> Clothing;
```

`ClothingEntry`:

| Property | Notes |
|---|---|
| `Clothing` | Direct reference to the `Clothing` resource |
| `ItemDefinitionId` | Steam Inventory item def id, used when the resource isn't resolved yet (see `ApplyAsync`) |
| `Tint` | `float?`, per-item tint override (`AllowTintSelect` items only) |
| `Bone`, `Transform` | Present in the data model for a manually-placed item (a bone name plus offset) but **not read anywhere in `Apply`/`ApplyAsync`** as of this version, see Gotchas |

Building and querying:

```csharp
var container = new ClothingContainer();

container.Add( someHatResource );          // ClothingEntry Add( Clothing )
container.Toggle( someHatResource );       // add if absent, remove if present
container.Has( someHatResource );          // bool
var entry = container.FindEntry( someHatResource );
if ( entry is not null ) entry.Tint = 0.75f;

container.Height = 0.6f;
container.Age = 0.3f;
container.Normalize();                     // clamps Height/Age/Tint to 0-1, truncates DisplayName to 32 chars
```

`Add` (both the `Clothing` overload and the `ClothingEntry` overload) removes any existing
entries that `CanBeWornWith` rejects before adding the new one, so a container never holds an
incompatible pair, but it does mean adding item B can silently evict a previously-added item
A.

Loading a stored outfit:

```csharp
var container = ClothingContainer.CreateFromJson( json );
var local = ClothingContainer.CreateFromLocalUser();               // reads the local user's saved avatar, strips unowned items
var remote = ClothingContainer.CreateFromConnection( connection );  // reads a connection's replicated avatar (see section 5)
```

---

## 4. Dressing a SkinnedModelRenderer

### Manual: `Apply` / `ApplyAsync`

```csharp
var body = go.GetComponent<SkinnedModelRenderer>();
body.Model = Model.Load( "models/citizen/citizen.vmdl" );

var outfit = ClothingContainer.CreateFromLocalUser();
outfit.Apply( body );
```

`Apply( SkinnedModelRenderer body )` is synchronous and does the whole job in one pass:

1. `Reset( body )` — destroys every existing child GameObject tagged `"clothing"`, resets
   `scale_height` to 1, `MaterialGroup` to `"default"`, clears `MaterialOverride`, restores
   `BodyGroups` to `body.Model.Parts.DefaultMask`.
2. Sets `scale_height` from `Height` (remapped 0-1 → 0.8-1.2) and `scale_heel` from the
   highest `HeelHeight` among worn items, both as animgraph parameters via `body.Set(...)`.
3. Sets `skin_age`/`skin_tint` render attributes via `body.Attributes.Set(...)`.
4. Filters the entry list down to items whose model actually loads and are valid for the
   target body (citizen vs human), via `IsValidClothing`.
5. If the body is human: picks up a `HasHumanSkin` item if one is worn (swaps `body.Model`,
   `BodyGroups`, `MaterialGroup` entirely), and calls `EnsureHumanUnderwear` (see Gotchas).
6. Resolves and applies `skin`/`eyes` material overrides via
   `body.SetMaterialOverride( material, "skin" | "eyes" )`.
7. For every remaining item: loads its model, creates a child `GameObject` named
   `"Clothing - {resource name}"`, tags it `"clothing"`, adds a `SkinnedModelRenderer` with
   `BoneMergeTarget = body`, applies the same skin/eyes overrides, its `MaterialGroup`, and
   its tint (if `AllowTintSelect`).
8. Applies the combined `HideBody` mask as body groups on `body` itself (section 6).

```csharp
public void Apply( SkinnedModelRenderer body );
public Task ApplyAsync( SkinnedModelRenderer body, CancellationToken token );
```

`ApplyAsync` calls `Apply` immediately for anything already resolvable, then downloads any
entry that only has an `ItemDefinitionId` (a Steam Inventory item whose `Clothing` resource
isn't loaded on this client yet) via `Cloud.Load<Clothing>` (game context) or package
install (menu context), and re-applies once new items resolve. Prefer `ApplyAsync` whenever
the outfit can contain Steam Inventory items you haven't necessarily got loaded locally,
which is the common case for dressing other players.

### The built-in `Dresser` component

`Sandbox.Dresser` (`Component, Component.ExecuteInEditor`) wraps `ClothingContainer` for the
common case of "dress this body from somewhere" and is the fastest correct way to hook a
player up to whatever outfit they picked on sbox.game:

```csharp
[Property] public Dresser.ClothingSource Source { get; set; }
// Manual         - dress from Clothing / WorkshopItems / ManualHeight / ManualAge / ManualTint set on the component
// LocalUser      - ClothingContainer.CreateFromLocalUser()
// OwnerConnection- ClothingContainer.CreateFromConnection( Network.Owner, RemoveUnownedItems )

[Property] public SkinnedModelRenderer BodyTarget { get; set; }
[Property] public bool RemoveUnownedItems { get; set; } = true;  // OwnerConnection only
[Property] public bool ApplyHeightScale { get; set; } = true;
```

Typical player prefab setup: put `Dresser` next to the body's `SkinnedModelRenderer`, set
`Source = ClothingSource.OwnerConnection` and `BodyTarget` to that renderer. On `OnAwake`
(only if `!IsProxy`) it resolves the outfit and calls `ClothingContainer.ApplyAsync`, then
`BodyTarget.MergeDescendants()` to fold the new clothing renderers' bone hierarchies in.
Call `Apply()` yourself to re-dress on demand (e.g. after `Networking.IsHost` code hands out
a uniform), or `Clear()` to strip all clothing back to a bare `ClothingContainer`.

`ClothingSource.Manual` clothing (its `List<ClothingEntry> Clothing` property) is not
`[Sync]`; only the height/age/tint scalars are. See section 5 for what that means for
networking a manually-assigned outfit.

---

## 5. Serializing and networking an avatar

### Serialization

```csharp
string json = container.Serialize();
var restored = ClothingContainer.CreateFromJson( json );
```

`Serialize()` writes `{ Items, Height, DisplayName, Age, Tint, PrefersHuman }`, where each
item is `{ p: resourcePath, iid: steamItemDefId, t: tint }` (compact keys; a Steam Inventory
item stores `iid` instead of a path). `Deserialize` also accepts the legacy flat-array
format and a legacy integer resource id (`id`), for old saved avatars.

### The built-in avatar convar

The local player's chosen outfit is a single saved, user-info-replicated convar:

```csharp
[ConVar( "avatar", ConVarFlags.Saved | ConVarFlags.UserInfo | ConVarFlags.Protected )]
public static string AvatarJson { get; set; } = DefaultAvatar;
```

`ConVarFlags.UserInfo`: "Adds to userinfo - making it accessible via the connection class on
other clients." Concretely, `ConnectionInfoManager` replicates userinfo convars through a
`StringTable` keyed `"{connectionId}#{convarName}"`, so **every client can read every other
connected player's `avatar` convar**, not just the owner. That's what makes this work
without any game-specific networking code:

```csharp
// Works for the local connection AND for any remote connection, on any client:
var outfit = ClothingContainer.CreateFromConnection( connection );
outfit.ApplyAsync( targetRenderer, token );
```

`CreateFromConnection` reads `connection.GetUserData( "avatar" )`, parses it, and (unless
`removeUnowned: false`) strips items the connection isn't verified to own — via
`connection.HasInventoryItem`, which only resolves for remote connections on the host
(`Networking.IsHost`); a client asking about another client's ownership gets nothing
stripped, by design, since it has no visibility into that data.

This is the mechanism `Dresser.ClothingSource.OwnerConnection` and the "wear what you picked
on sbox.game" behavior are built on: the player's client sets the `avatar` convar (the
in-game avatar editor calls `ClothingContainer.Store`, which also sets the Steam-backed
convar), it replicates as userinfo, and any other client can build a `ClothingContainer`
from it at any time.

### Game-controlled outfits (not the player's own pick)

For anything the game decides rather than the player, uniforms, disguises, team skins,
there's no engine-provided sync path: build it yourself. The shape is straightforward
because `ClothingContainer` is already JSON round-trippable:

```csharp
public sealed class TeamUniform : Component
{
    [Sync] public string OutfitJson { get; set; }

    [Property] public SkinnedModelRenderer Body { get; set; }

    protected override void OnAwake()
    {
        if ( Networking.IsHost )
            OutfitJson = ClothingContainer.CreateFromJson( teamOutfit ).Serialize();
    }

    // fires on every client when OutfitJson changes, including the initial replication
    void OnOutfitJsonChanged( string oldValue, string newValue )
    {
        var container = ClothingContainer.CreateFromJson( newValue );
        _ = container.ApplyAsync( Body, default );
    }
}
```

Whichever path builds the clothing GameObjects, remember they're plain, non-networked
GameObjects created under the body. If you re-dress an already-`NetworkSpawn`ed object
(changing clothes mid-game, not just at spawn), call `go.Network.Refresh()` on the body's
GameObject afterwards, structural changes after spawn are not automatically networked (see
`networking.md`).

---

## 6. Body groups and material groups

Two independent mechanisms on `SkinnedModelRenderer`/`ModelRenderer`, both used by clothing:

- **Body groups** hide/show alternate meshes baked into the model itself. `BodyGroups` is a
  `ulong` bitmask (`Model.BodyGroupMaskAttribute` drives the editor widget); each named part
  (`"Head"`, `"Chest"`, `"Legs"`, `"Hands"`, `"Feet"` on the citizen) occupies a slice of that
  mask. `body.Model.Parts.DefaultMask` is the model's authored default (`Reset` restores it).
  `SetBodyGroup( string name, int value )` and `SetBodyGroup( string name, string choice )`
  set one part by name; `GetBodyGroup( string name )` reads it back.
- **Material groups** swap the whole set of materials a model uses in one go.
  `MaterialGroup` is a plain `string` naming a group defined on the `.vmdl`; clothing sets it
  per-item from `Clothing.MaterialGroup`, and human skins set it from
  `Clothing.HumanSkinMaterialGroup`.

`ClothingContainer.GetBodyGroups` computes which citizen body parts to hide based on the
combined `HideBody` mask of all worn items:

```csharp
public IEnumerable<(string name, int value)> GetBodyGroups( IEnumerable<Clothing> items, Model model = null );
```

For each of the five clothing `BodyGroups` flags it's set on any worn item, it looks up that
named part on the model and returns the *last* choice index (`Choices.Count - 1`), which by
citizen modeling convention is the empty/hidden mesh; `Apply` then calls
`body.SetBodyGroup( name, value )` only where `value != 0`, so items that don't hide
anything never touch a bodygroup that was already correct.

Per-clothing-item material overrides go through `SetMaterialOverride( Material, string
target )` (by attribute name, not by index): worn items use it to override the `"skin"` and
`"eyes"` material slots on both the main body and every clothing child renderer, from
`Clothing.SkinMaterial`/`EyesMaterial` (or the Human equivalents).

---

## 7. Gotchas

- **`ClothingEntry.Bone` and `.Transform` are dead weight in the dressing path.** They exist
  in the serialized shape (comment: "if this item is manually placed, this is the bone we're
  attached to") but `Apply`/`ApplyAsync` never read them, every worn item goes through the
  same bonemerge-to-body path regardless. Don't build a manual-attachment-point feature
  assuming the engine honors these fields; it doesn't, as of this version.
- **Citizen vs human is decided by a model name string match**, not a flag you set. If your
  custom body model happens to be named anything other than `citizen.vmdl` (case
  insensitive), the entire dressing pipeline treats it as human: `HumanAltModel` fields get
  used instead of `Model`, `HasHumanSkin` items become eligible, and automatic underwear
  kicks in (next point).
- **Human bodies get free underwear you didn't ask for.** `EnsureHumanUnderwear` adds a
  default white underwear item (and a bra, if `female` tag and chest isn't hidden) to any
  human-body outfit that doesn't already have one in the `Underwear`/`Underpants`/`Bra`
  categories, unless the relevant `HideBody` flag is set. This only runs for human bodies;
  citizens never get anything added automatically.
- **`Apply` silently skips clothing it can't resolve or validate**: a null/failed model load,
  an unresolved `ItemDefinitionId` (no `Clothing` object yet), or an item invalid for the
  current body type (citizen-only item on a human body with no `HumanAltModel`) is dropped
  from the render, not errored. Use `ApplyAsync` if any entries might still need downloading.
- **`Dresser` only calls `Apply()` for the owning client.** `OnAwake` is `if ( IsProxy )
  return;` before building the outfit; on a proxy, `OnEnabled` only reapplies
  height/age/tint attributes onto clothing children that must already exist in the object's
  hierarchy. That hierarchy has to arrive via the object's initial network spawn snapshot,
  changes made after spawn (re-dressing mid-game) need `go.Network.Refresh()` from the owner
  to reach anyone already connected.
- **Manual `Dresser` outfits aren't networked.** `ClothingSource.Manual`'s `Clothing` list has
  no `[Sync]`; only `ManualHeight`/`ManualAge`/`ManualTint` do. If you drive `Dresser` in
  Manual mode from game logic, you own getting the item list to other clients yourself
  (section 5's `TeamUniform` pattern).
- **`CreateRagdoll` (on `PlayerController`) copies worn clothing renderers onto the ragdoll**,
  cloning every child `SkinnedModelRenderer` under the body and re-pointing
  `BoneMergeTarget` at the new ragdoll body. If you build ragdolls a different way, remember
  clothing doesn't follow automatically, you have to walk the same children.
- **`RemoveUnownedItems( Connection )` is a no-op for anything but the host or the local
  connection.** A client calling it for someone else's `Connection` returns unfiltered,
  because clients have no visibility into another player's Steam Inventory. Ownership
  enforcement for remote players has to happen host-side.
- **`ConVarFlags.UserInfo` is how remote avatars are visible at all**, not a networking
  concept specific to clothing. Any userinfo convar you read via `Connection.GetUserData`
  works the same way; the `avatar` convar just happens to be the one the engine ships.
