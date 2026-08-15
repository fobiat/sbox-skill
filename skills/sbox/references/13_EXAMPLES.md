<!--
  s&box Skill : 13_EXAMPLES.md

  Complete worked examples, from an FPS controller to a press-E vendor.

  Author  : fobiat (Kyle Tarff) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# Worked Examples

Full components, each one pulled from s&box engine source at version 26.08.05 and
verified against it, not trimmed placeholder fragments. Treat these as the starting
skeleton for your own systems: grab the closest match, delete what doesn't apply, and
build outward from something that already compiles.

They lean on the topical references (`01_SCENE.md`, `02_COMPONENTS.md`,
`04_NETWORKING.md`, `05_INPUT_PHYSICS.md`, `03_UI.md`) for the surrounding
concepts, so read those first. Everything here is spelled out in full rather than
abbreviated, on purpose, so adapting one doesn't require guessing at the missing half.

***

## Ground Rules

- Every component below is `sealed` unless something further down needs to inherit from it.
- `[Property]` is what turns a plain field into something the inspector can edit and a prefab can serialize.
- Movement and physics run in `OnFixedUpdate`; camera work, input polling, and visuals run in `OnUpdate`.
- Scene queries go through `Scene.Get<T>()` / `Components.Get<T>()`, never `FindObjectOfType`, which doesn't exist here.
- Any update loop driving networked input or movement opens with `if ( IsProxy ) return;`.
- Logging goes through `Log.Info(...)` / `Log.Warning(...)`. `Console.WriteLine` and raw `System.IO` are off the table.

***

## Example 1: Health Component with Damage Events

A reusable `Health` component implementing `IDamageable`. Broadcasts a scene-wide
`IHealthEvents` event when entities die so other systems (score, spawn, HUD) can react
without holding a reference back to `Health` itself.

```csharp
using Sandbox;

public interface IHealthEvents : ISceneEvent<IHealthEvents>
{
    void OnDamaged( Health health, in DamageInfo damage ) { }
    void OnKilled( Health health, in DamageInfo damage ) { }
}

public sealed class Health : Component, Component.IDamageable
{
    [Property, Range( 1f, 1000f )] public float MaxHealth { get; set; } = 100f;
    [Property] public bool Invincible { get; set; }

    [Sync, Change( nameof( OnHealthChanged ) )]
    public float Current { get; set; }

    public bool IsAlive => Current > 0f;

    TimeSince _timeSinceLastDamage;

    protected override void OnStart()
    {
        if ( !IsProxy )
            Current = MaxHealth;
    }

    public void OnDamage( in DamageInfo damage )
    {
        if ( IsProxy || !IsAlive || Invincible ) return;

        Current = MathX.Clamp( Current - damage.Damage, 0f, MaxHealth );
        _timeSinceLastDamage = 0;

        IHealthEvents.Post( x => x.OnDamaged( this, damage ) );

        if ( Current <= 0f )
            IHealthEvents.Post( x => x.OnKilled( this, damage ) );
    }

    void OnHealthChanged( float oldValue, float newValue )
    {
        if ( newValue < oldValue )
            Sound.Play( "player.hurt", WorldPosition );
    }
}
```

Worth flagging:
- `[Sync, Change( nameof( OnHealthChanged ) )]` triggers its change callback everywhere,
  proxies included, whenever the owner's health value moves.
- `in DamageInfo` on `IDamageable.OnDamage` is a `ref readonly` struct parameter, so the
  call costs nothing extra in allocations.
- `Current` is only ever written on the authoritative side. Every other client learns the
  new value through the change callback, not by mutating it themselves.
- `IHealthEvents.Post` stays local to the machine that raised it. Death effects everyone
  needs to see belong on a `[Rpc.Broadcast]`, not a scene event.

***

## Example 2: First-Person Character Controller

A standalone FPS controller built on the lower-level `CharacterController`, without the
`PlayerController` helper. Reach for this when you want explicit control over
acceleration, friction, air control, and camera placement instead of inheriting the
stock behaviour.

```csharp
using Sandbox;

public sealed class FirstPersonController : Component
{
    [Property] public CharacterController Controller { get; set; }
    [Property] public GameObject CameraPivot { get; set; }

    [Property, Range( 50f, 500f )] public float WalkSpeed { get; set; } = 180f;
    [Property, Range( 50f, 700f )] public float RunSpeed { get; set; } = 320f;
    [Property, Range( 100f, 600f )] public float JumpPower { get; set; } = 320f;
    [Property, Range( 1f, 20f )] public float GroundFriction { get; set; } = 6f;
    [Property, Range( 0f, 1f )] public float AirControl { get; set; } = 0.15f;

    [Sync] public Angles EyeAngles { get; set; }

    protected override void OnStart()
    {
        if ( !Controller.IsValid() )
            Controller = GetOrAddComponent<CharacterController>();
    }

    protected override void OnUpdate()
    {
        if ( IsProxy ) return;

        var look = EyeAngles;
        look += Input.AnalogLook;
        look.pitch = look.pitch.Clamp( -89f, 89f );
        look.roll = 0f;
        EyeAngles = look;

        if ( CameraPivot.IsValid() )
        {
            CameraPivot.WorldRotation = EyeAngles.ToRotation();
            CameraPivot.WorldPosition = WorldPosition + Vector3.Up * 64f;
        }
    }

    protected override void OnFixedUpdate()
    {
        if ( IsProxy ) return;

        var yaw = Rotation.FromYaw( EyeAngles.yaw );
        var wishDir = Input.AnalogMove * yaw;
        if ( !wishDir.IsNearZeroLength )
            wishDir = wishDir.Normal;

        var wishSpeed = Input.Down( "run" ) ? RunSpeed : WalkSpeed;

        if ( Controller.IsOnGround )
        {
            Controller.ApplyFriction( GroundFriction );
            Controller.Accelerate( wishDir * wishSpeed );

            if ( Input.Pressed( "jump" ) )
                Controller.Punch( Vector3.Up * JumpPower );
        }
        else
        {
            Controller.Velocity += Scene.PhysicsWorld.Gravity * Time.Delta;
            Controller.Accelerate( wishDir * wishSpeed * AirControl );
        }

        Controller.Move();

        WorldRotation = Rotation.FromYaw( EyeAngles.yaw );
    }
}
```

Scene setup:
1. Add `FirstPersonController` to a GameObject; it pulls in `CharacterController` automatically.
2. Give it a child GameObject called `CameraPivot` carrying a `CameraComponent`, then drag that child into the `CameraPivot` slot.
3. Set the `CharacterController`'s `Height` and `Radius` to match your player capsule; 72 and 16 are reasonable defaults.

`OnFixedUpdate` runs movement rather than `OnUpdate` because the character controller
does solver iterations and ground-sticking on a fixed step. Drive it from a variable-rate
update instead and you get jitter and jumps that land differently depending on frame rate.

***

## Example 3: Hitscan Weapon with Networked Effects

A rifle that traces, applies damage, and plays impact effects on every client. Uses
`[Rpc.Broadcast]` to replicate the effects without syncing a particle GameObject over the
wire.

```csharp
using Sandbox;

public sealed class HitscanWeapon : Component
{
    [Property] public float Damage { get; set; } = 25f;
    [Property] public float Range { get; set; } = 5000f;
    [Property] public float RPM { get; set; } = 600f;
    [Property] public GameObject MuzzleFlashPrefab { get; set; }
    [Property] public GameObject ImpactPrefab { get; set; }
    [Property] public string ShootSound { get; set; } = "weapon.rifle.shoot";

    TimeSince _timeSinceShot;
    float FireDelay => 60f / RPM;

    protected override void OnUpdate()
    {
        if ( IsProxy ) return;
        if ( !Input.Down( "attack1" ) ) return;
        if ( _timeSinceShot < FireDelay ) return;

        _timeSinceShot = 0;
        Fire();
    }

    void Fire()
    {
        var cam = Scene.Camera;
        if ( !cam.IsValid() ) return;

        var ray = cam.ScreenNormalToRay( new Vector3( 0.5f, 0.5f, 0f ) );

        var tr = Scene.Trace.Ray( ray, Range )
            .UseHitboxes( true )
            .IgnoreGameObjectHierarchy( GameObject.Root )
            .WithoutTags( new[] { "trigger" } )
            .Run();

        if ( tr.Hit && tr.GameObject.IsValid() )
        {
            var damageable = tr.GameObject.Components.GetInAncestorsOrSelf<Component.IDamageable>();
            if ( damageable is not null )
            {
                damageable.OnDamage( new DamageInfo( Damage, GameObject.Root, GameObject )
                {
                    Position = tr.HitPosition,
                    Origin = ray.Position
                } );
            }
        }

        PlayShotEffects( tr.StartPosition, tr.EndPosition, tr.Normal, tr.Hit );
    }

    [Rpc.Broadcast( NetFlags.Unreliable )]
    void PlayShotEffects( Vector3 start, Vector3 end, Vector3 normal, bool hit )
    {
        Sound.Play( ShootSound, start );

        if ( MuzzleFlashPrefab.IsValid() )
        {
            var muzzle = MuzzleFlashPrefab.Clone( start );
            muzzle.BreakFromPrefab();
        }

        if ( hit && ImpactPrefab.IsValid() )
        {
            var impact = ImpactPrefab.Clone( end, Rotation.LookAt( normal ) );
            impact.BreakFromPrefab();
        }
    }
}
```

Worth flagging:
- The trace, and the damage that follows from it, only ever happens on the shooter's
  machine (`if ( IsProxy ) return;`).
- `[Rpc.Broadcast]` fires on **every** client, shooter included, which is why the effects
  don't need a special case for the local player.
- Cosmetic effects want `NetFlags.Unreliable`: an occasional dropped muzzle flash costs
  nothing, whereas paying for reliable delivery on something nobody will notice missing
  is wasted bandwidth.
- Effects get `BreakFromPrefab()` called on them so they detach into standalone
  GameObjects that clean themselves up, assuming the prefab carries a `TemporaryEffect`
  component.
- `IgnoreGameObjectHierarchy( GameObject.Root )` keeps the weapon from hitting itself even
  when it's nested several levels deep under the player.

***

## Example 4: Networked Game Manager & Player Spawning

A `GameManager` implemented as a `GameObjectSystem` that sets up the lobby on host, then
spawns player prefabs as each connection becomes active. As a system rather than a
component, it doesn't need a GameObject to live on: it gets automatic per-scene lifetime
for free.

```csharp
using Sandbox;
using Sandbox.Network;
using System.Linq;

public sealed class GameManager : GameObjectSystem<GameManager>,
                                  ISceneStartup,
                                  Component.INetworkListener
{
    public GameManager( Scene scene ) : base( scene ) { }

    const string PlayerPrefabPath = "prefabs/player.prefab";

    void ISceneStartup.OnHostInitialize()
    {
        if ( !Networking.IsActive )
        {
            Networking.CreateLobby( new LobbyConfig
            {
                MaxPlayers = 8,
                Privacy = LobbyPrivacy.Public,
                Name = $"{Game.Ident} Game"
            } );
        }
    }

    public void OnActive( Connection connection )
    {
        var prefab = GameObject.GetPrefab( PlayerPrefabPath );
        if ( !prefab.IsValid() )
        {
            Log.Warning( $"Player prefab not found at {PlayerPrefabPath}" );
            return;
        }

        var spawn = PickSpawnPoint();
        var player = prefab.Clone( spawn.Position, spawn.Rotation );
        player.Name = $"Player - {connection.DisplayName}";
        player.NetworkSpawn( connection );
    }

    public void OnDisconnected( Connection connection )
    {
        // Clean up any objects left owned by this connection
        foreach ( var go in Scene.GetAllObjects( true ) )
        {
            if ( go.Network.Owner == connection )
                go.Destroy();
        }
    }

    Transform PickSpawnPoint()
    {
        var points = Scene.GetAllComponents<SpawnPoint>().ToList();
        if ( points.Count == 0 )
            return global::Transform.Zero;

        var chosen = points[Game.Random.Next( points.Count )];
        return chosen.WorldTransform;
    }
}
```

Worth flagging:
- `GameObjectSystem<GameManager>` spins itself up per scene automatically, so there's no
  GameObject anywhere carrying it.
- `ISceneStartup.OnHostInitialize()` fires exactly once, on the host, right after the
  scene finishes loading, the right moment for lobby setup.
- `INetworkListener.OnActive` doesn't fire the instant a client connects; it waits until
  they've finished loading the scene and are actually ready for gameplay.
- Passing `connection` into `NetworkSpawn` hands that client ownership immediately, so
  they're driving their own player from tick one.
- The sweep in `OnDisconnected` earns its keep because `NetworkOrphaned.Destroy` only
  covers objects that were **newly** spawned. Anything that changed hands afterward isn't
  guaranteed to follow that default, so cleaning up explicitly is the safer bet.

***

## Example 5: Networked Player with Sync, RPCs, and Proxy Safety

A minimal player component that pairs with `FirstPersonController` from Example 2. It
covers the full networking surface you'll actually use day to day: `INetworkSpawn`,
`[Sync]`, proxy checks, owner-only RPCs, and `IGameObjectNetworkEvents`.

```csharp
using Sandbox;

public sealed class Player : Component,
                             Component.INetworkSpawn,
                             IGameObjectNetworkEvents
{
    [Property] public FirstPersonController Movement { get; set; }
    [Property] public Health Health { get; set; }
    [Property] public SkinnedModelRenderer Body { get; set; }

    [Sync] public string DisplayName { get; set; }
    [Sync] public int Kills { get; set; }
    [Sync] public int Deaths { get; set; }

    public void OnNetworkSpawn( Connection owner )
    {
        DisplayName = owner.DisplayName;

        // Hide the body for the owner (first-person view)
        if ( owner == Connection.Local && Body.IsValid() )
            Body.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;
    }

    void IGameObjectNetworkEvents.NetworkOwnerChanged( Connection newOwner, Connection previousOwner )
    {
        Log.Info( $"{DisplayName} ownership: {previousOwner?.DisplayName} → {newOwner?.DisplayName}" );
    }

    [Rpc.Owner]
    public void TakeDamage( float amount, Guid attackerId )
    {
        // Runs on whoever owns this player
        Health?.OnDamage( new DamageInfo( amount, Scene.Directory.FindByGuid( attackerId ), null ) );
    }

    [Rpc.Broadcast]
    public void PlayEmote( string name )
    {
        if ( Body.IsValid() )
            Body.Set( $"emote_{name}", true );
    }

    [Rpc.Host]
    public void AddKill()
    {
        // Authoritative score increment: only the host mutates
        Kills++;
    }
}
```

Worth flagging:
- The default direction for `[Sync]` is **owner to everyone**, and only the owner can
  write to it unless `SyncFlags.FromHost` says otherwise.
- `[Rpc.Owner]` lands on whichever client owns the `GameObject`, exactly the shape needed
  for telling a victim they just took a hit.
- `[Rpc.Broadcast]` reaches every client and the host, suited to cosmetic or shared
  effects.
- `[Rpc.Host]` runs on the host alone, which is where score, economy, or anything else
  that needs validation before being trusted belongs.
- Rather than serializing a full `GameObject` reference across an RPC, pass its Guid and
  resolve it on the other end with `Scene.Directory.FindByGuid(...)`. It's cheaper on the
  wire and still resolves for clients who joined after the object existed.
- `IGameObjectNetworkEvents.NetworkOwnerChanged` fires for every client, which makes it
  the hook for flipping between first-person and third-person visuals as ownership moves.

***

## Example 6: Razor HUD Panel with Data Binding

A HUD showing health, ammo, and a rolling kill feed. It only rebuilds when `BuildHash`
changes, so it's cheap to leave on screen for the whole match.

**PlayerHud.razor**, dropped on a `GameObject` with a `ScreenPanel` sibling:

```razor
@using Sandbox
@using Sandbox.UI
@inherits PanelComponent

<root class="hud">
    <div class="vitals">
        <div class="bar">
            <div class="fill" style="width: @(HealthPercent)%"></div>
            <label>@((int)HealthValue) / @((int)MaxHealthValue)</label>
        </div>
        <label class="ammo">@AmmoValue</label>
    </div>

    <div class="killfeed">
        @foreach ( var entry in KillFeed )
        {
            <label class="kill">@entry.Killer killed @entry.Victim</label>
        }
    </div>
</root>

@code
{
    Player LocalPlayer => Game.ActiveScene?
        .GetAllComponents<Player>()
        .FirstOrDefault( p => !p.IsProxy );

    float HealthValue => LocalPlayer?.Health?.Current ?? 0f;
    float MaxHealthValue => LocalPlayer?.Health?.MaxHealth ?? 1f;
    float HealthPercent => MathX.Clamp( HealthValue / MaxHealthValue * 100f, 0f, 100f );
    int AmmoValue => GetComponent<HitscanWeapon>() is { } w ? 30 : 0;

    List<KillEntry> KillFeed { get; } = new();

    public record KillEntry( string Killer, string Victim, TimeSince Time );

    protected override void OnUpdate()
    {
        KillFeed.RemoveAll( x => x.Time > 5f );
    }

    protected override int BuildHash() =>
        HashCode.Combine( HealthValue, AmmoValue, KillFeed.Count );
}
```

**PlayerHud.razor.scss**, auto-loaded by filename convention:

```scss
PlayerHud {
    position: absolute;
    left: 0; top: 0; right: 0; bottom: 0;
    pointer-events: none;
    flex-direction: column;
    justify-content: space-between;
    padding: 24px;

    .vitals {
        flex-direction: row;
        align-items: flex-end;
        justify-content: space-between;

        .bar {
            width: 320px;
            height: 28px;
            background-color: rgba( 0, 0, 0, 0.55 );
            border-radius: 4px;
            overflow: hidden;
            position: relative;

            .fill {
                height: 100%;
                background-color: #e74c3c;
                transition: width 0.25s ease-out;
            }

            label {
                position: absolute;
                left: 0; right: 0; top: 0; bottom: 0;
                justify-content: center;
                align-items: center;
                color: white;
                font-size: 16px;
                text-shadow: 1px 1px 2px black;
            }
        }

        .ammo {
            color: white;
            font-size: 48px;
            font-weight: bold;
            text-shadow: 2px 2px 4px black;
        }
    }

    .killfeed {
        flex-direction: column;
        align-items: flex-end;

        .kill {
            color: #ddd;
            font-size: 14px;
            padding: 4px 8px;
            background-color: rgba( 0, 0, 0, 0.4 );
            margin-bottom: 2px;
            transition: all 0.3s ease;
            &:intro { opacity: 0; transform: translateX( 40px ); }
            &:outro { opacity: 0; transform: translateX( -40px ); }
        }
    }
}
```

Worth flagging:
- `BuildHash` is the mechanism that keeps the Razor tree rebuild cheap, and it only works
  if every value the template reads is included in it. Leave one out and a change to it
  silently fails to show up.
- `pointer-events: none` on the root panel lets input fall through to the game underneath.
  Flip individual sub-panels to `all` wherever they need to catch clicks.
- `:intro` and `:outro` are transitions specific to s&box, firing on panel creation and
  deletion respectively. Combine them with `Delete()` and the kill-feed animates itself,
  no extra work required.
- `LocalPlayer` re-runs its query on every `BuildHash` evaluation. That's fine here since
  it's a single `GetAllComponents` call across the scene, O(n) but small; a large scene
  would want that reference cached in `OnStart` instead.

***

## Example 7: Physics Grenade with Trigger Proximity & Explosion

A thrown grenade that bounces off world geometry, detonates on contact with a player, or
times out on its own. Covers `Rigidbody.ApplyImpulse`, `ICollisionListener`,
`SceneTrace.Sphere` for radius damage, and async sequencing with `Task.DelaySeconds`.

```csharp
using Sandbox;
using System.Threading.Tasks;

public sealed class Grenade : Component, Component.ICollisionListener
{
    [Property] public float FuseSeconds { get; set; } = 3f;
    [Property] public float ExplosionRadius { get; set; } = 250f;
    [Property] public float MaxDamage { get; set; } = 150f;
    [Property] public float ThrowForce { get; set; } = 800f;
    [Property] public GameObject ExplosionPrefab { get; set; }

    Rigidbody _rigidbody;
    bool _exploded;

    protected override void OnStart()
    {
        _rigidbody = GetOrAddComponent<Rigidbody>();
        _rigidbody.Velocity = WorldRotation.Forward * ThrowForce;
        _rigidbody.AngularVelocity = Vector3.Random * 5f;

        _ = FuseTimer();
    }

    async Task FuseTimer()
    {
        await Task.DelaySeconds( FuseSeconds );
        Explode();
    }

    public void OnCollisionStart( Collision collision )
    {
        // Bounce off world, but detonate on contact with a player
        if ( collision.Other.GameObject.Tags.Has( "player" ) )
            Explode();
    }

    public void OnCollisionUpdate( Collision collision ) { }
    public void OnCollisionStop( CollisionStop collision ) { }

    void Explode()
    {
        if ( _exploded ) return;
        _exploded = true;

        var origin = WorldPosition;

        if ( ExplosionPrefab.IsValid() )
        {
            var fx = ExplosionPrefab.Clone( origin );
            fx.BreakFromPrefab();
        }

        // Radius damage: sphere overlap via a sphere sweep of zero length
        foreach ( var hit in Scene.Trace
                     .Sphere( ExplosionRadius, origin, origin )
                     .WithAnyTags( "player", "prop" )
                     .RunAll() )
        {
            var target = hit.GameObject.Components.GetInAncestorsOrSelf<Component.IDamageable>();
            if ( target is null ) continue;

            var dist = Vector3.DistanceBetween( origin, hit.EndPosition );
            var falloff = 1f - MathX.Clamp( dist / ExplosionRadius, 0f, 1f );

            target.OnDamage( new DamageInfo( MaxDamage * falloff, GameObject, null )
            {
                Position = hit.EndPosition,
                Origin = origin,
                IsExplosion = true
            } );

            // Knockback: find a Rigidbody on the victim and shove it
            var rb = hit.GameObject.Components.GetInAncestorsOrSelf<Rigidbody>();
            if ( rb.IsValid() )
            {
                var dir = (hit.EndPosition - origin).Normal;
                rb.ApplyImpulse( dir * MaxDamage * falloff * 10f );
            }
        }

        GameObject.Destroy();
    }
}
```

Worth flagging:
- Calling `GetOrAddComponent<Rigidbody>()` from `OnStart` is safe because it's idempotent,
  so a hot-reload of the script won't leave a second `Rigidbody` behind.
- `_ = FuseTimer();` fires an async countdown and walks away from it. `Component.Task` is
  scoped to the GameObject it runs on, so an early-destroyed grenade cancels its own
  await, nothing extra to clean up.
- `Scene.Trace.Sphere( r, origin, origin ).RunAll()` hands back every physics shape inside
  the radius in one call, cheaper than looping every component in the scene and running a
  distance check on each.
- The broad-phase filter here is `WithAnyTags`, tag-based rather than type-checked, which
  is what lets the grenade stay ignorant of what a `Player` actually is.
- `falloff = 1 - distance / radius` gives a linear curve. Reach for `MathX.LerpInverse`
  instead if a different falloff shape is wanted.

***

## Example 8: NavMeshAgent AI with State Machine

A simple enemy that patrols between waypoints, chases the player once close enough, and
attacks when in range. Uses `NavMeshAgent` for movement and `CitizenAnimationHelper`-style
parameter driving for the anim graph.

```csharp
using Sandbox;
using System.Linq;

public sealed class EnemyAi : Component
{
    public enum State { Idle, Patrol, Chase, Attack, Dead }

    [Property] public NavMeshAgent Agent { get; set; }
    [Property] public SkinnedModelRenderer Body { get; set; }
    [Property] public Health Health { get; set; }

    [Property] public float SightRange { get; set; } = 800f;
    [Property] public float AttackRange { get; set; } = 120f;
    [Property] public float AttackDamage { get; set; } = 15f;
    [Property] public float AttackCooldown { get; set; } = 1.25f;
    [Property] public float PatrolRadius { get; set; } = 600f;

    State _state;
    Vector3 _patrolTarget;
    GameObject _target;
    TimeSince _timeSinceAttack;
    TimeSince _timeSinceDecision;

    protected override void OnStart()
    {
        Agent ??= Components.Get<NavMeshAgent>();
        PickNewPatrolTarget();
    }

    protected override void OnFixedUpdate()
    {
        if ( IsProxy ) return;
        if ( Health is { IsAlive: false } )
        {
            EnterState( State.Dead );
            return;
        }

        if ( _timeSinceDecision > 0.25f )
        {
            _timeSinceDecision = 0;
            UpdatePerception();
        }

        switch ( _state )
        {
            case State.Idle:
            case State.Patrol: TickPatrol(); break;
            case State.Chase: TickChase(); break;
            case State.Attack: TickAttack(); break;
        }

        DriveAnimation();
    }

    void UpdatePerception()
    {
        var nearest = Scene.GetAllComponents<Player>()
            .Where( p => p.Health is { IsAlive: true } )
            .OrderBy( p => Vector3.DistanceBetween( p.WorldPosition, WorldPosition ) )
            .FirstOrDefault();

        if ( nearest is null )
        {
            _target = null;
            if ( _state is State.Chase or State.Attack )
                EnterState( State.Patrol );
            return;
        }

        var dist = Vector3.DistanceBetween( nearest.WorldPosition, WorldPosition );
        if ( dist > SightRange )
        {
            _target = null;
            if ( _state is State.Chase or State.Attack )
                EnterState( State.Patrol );
            return;
        }

        _target = nearest.GameObject;
        EnterState( dist <= AttackRange ? State.Attack : State.Chase );
    }

    void TickPatrol()
    {
        if ( !Agent.IsNavigating || Vector3.DistanceBetween( WorldPosition, _patrolTarget ) < 40f )
            PickNewPatrolTarget();
    }

    void TickChase()
    {
        if ( !_target.IsValid() ) return;
        Agent.MoveTo( _target.WorldPosition );
    }

    void TickAttack()
    {
        if ( !_target.IsValid() ) return;

        Agent.Stop();

        var lookDir = (_target.WorldPosition - WorldPosition).WithZ( 0 ).Normal;
        if ( !lookDir.IsNearZeroLength )
            WorldRotation = Rotation.LerpTo( WorldRotation, Rotation.LookAt( lookDir ), Time.Delta * 8f );

        if ( _timeSinceAttack >= AttackCooldown )
        {
            _timeSinceAttack = 0;
            DoAttack();
        }
    }

    void DoAttack()
    {
        if ( !_target.IsValid() ) return;

        var dmg = _target.Components.GetInAncestorsOrSelf<Component.IDamageable>();
        dmg?.OnDamage( new DamageInfo( AttackDamage, GameObject, null )
        {
            Position = _target.WorldPosition,
            Origin = WorldPosition
        } );
    }

    void PickNewPatrolTarget()
    {
        _patrolTarget = Scene.NavMesh.GetRandomPoint( WorldPosition, PatrolRadius ) ?? WorldPosition;
        Agent.MoveTo( _patrolTarget );
        EnterState( State.Patrol );
    }

    void EnterState( State newState )
    {
        if ( _state == newState ) return;
        _state = newState;

        if ( newState == State.Dead )
        {
            Agent.Stop();
            Agent.Enabled = false;
        }
    }

    void DriveAnimation()
    {
        if ( !Body.IsValid() ) return;
        Body.Set( "move_speed", Agent.Velocity.Length );
        Body.Set( "b_attack", _state == State.Attack && _timeSinceAttack < 0.1f );
    }
}
```

Worth flagging:
- Perception ticks at 4 Hz (`_timeSinceDecision > 0.25f`) instead of on every fixed
  update. Nothing needs an enemy re-evaluating its target fifty times a second, and
  that's exactly the cost that balloons first as enemy count grows.
- `Agent.MoveTo` costs nothing extra when called repeatedly with an unchanged target,
  since it's idempotent. `Agent.Stop()` before a melee swing still earns its place, it's
  what keeps the agent from overshooting the target.
- `Scene.NavMesh.GetRandomPoint(origin, radius)` returns a nullable `Vector3?`, coming
  back `null` when there's no NavMesh or nothing samplable at that point. Falling back to
  the current position turns a missing NavMesh into standing still rather than a throw.
- The anim graph's move-speed parameter is driven off `Agent.Velocity.Length`; the same
  approach carries over to `CitizenAnimationHelper.WithVelocity` for the stock rig.

***

## Example 9: Prefab Spawner with Pool-Friendly Lifecycle

A spawner that uses `GameObject.GetPrefab` and `Clone` to create enemies on a timer.
Parks new instances under a pool root to keep the scene outliner tidy, and respects host
authority so clients don't spawn duplicate authoritative objects.

```csharp
using Sandbox;

public sealed class PrefabSpawner : Component
{
    [Property] public GameObject Prefab { get; set; }
    [Property, Range( 0.5f, 30f )] public float Interval { get; set; } = 5f;
    [Property] public int MaxAlive { get; set; } = 12;
    [Property] public bool NetworkSpawned { get; set; } = true;

    TimeUntil _nextSpawn;
    readonly List<GameObject> _alive = new();

    protected override void OnStart()
    {
        _nextSpawn = Interval;
    }

    protected override void OnUpdate()
    {
        // Only the host spawns authoritative objects
        if ( NetworkSpawned && !Networking.IsHost ) return;

        _alive.RemoveAll( go => !go.IsValid() );

        if ( _alive.Count >= MaxAlive ) return;
        if ( !_nextSpawn ) return;

        _nextSpawn = Interval;
        Spawn();
    }

    void Spawn()
    {
        if ( !Prefab.IsValid() ) return;

        var pos = WorldPosition + Vector3.Random.WithZ( 0 ) * 100f;
        var rot = Rotation.FromYaw( Game.Random.NextSingle() * 360f );

        var go = Prefab.Clone( pos, rot );
        go.Name = $"{Prefab.Name} (spawn)";
        go.SetParent( GameObject );

        if ( NetworkSpawned )
            go.NetworkSpawn();

        _alive.Add( go );
    }

    protected override void OnDestroy()
    {
        foreach ( var go in _alive )
        {
            if ( go.IsValid() )
                go.Destroy();
        }
    }
}
```

Worth flagging:
- `Prefab.Clone(pos, rot)` keeps a live link to the source prefab, so edits made to the
  prefab asset later propagate to every clone. That link only breaks if `BreakFromPrefab()`
  is called deliberately.
- `NetworkSpawn()` belongs to the authority alone. The `Networking.IsHost` check is what
  keeps every connected client from spawning its own duplicate if this component happens
  to also run there.
- `Vector3.Random` hands back a unit-length direction, so scale it by whatever radius is
  actually wanted.
- Pruning dead references with `_alive.RemoveAll( go => !go.IsValid() )` is the standard
  idiom, since `GameObject.IsValid()` flips to `false` the instant `Destroy()` runs.
- Parenting spawned objects to the spawner's own `GameObject` buys automatic cleanup
  through `OnDestroy` and a single switch, `GameObject.Enabled = false`, to disable the
  whole group at once.

***

## Example 10: Trigger Zone (Pickup / Checkpoint)

A `Collider` configured as a trigger that grants a pickup when a player overlaps it. Uses
`ITriggerListener` and a broadcast RPC to tell every client to play the pickup effect.

```csharp
using Sandbox;

public sealed class HealthPickup : Component, Component.ITriggerListener
{
    [Property] public float HealAmount { get; set; } = 25f;
    [Property] public float RespawnTime { get; set; } = 15f;
    [Property] public GameObject PickupEffect { get; set; }
    [Property] public ModelRenderer Visual { get; set; }

    [Sync] bool _available { get; set; } = true;

    public void OnTriggerEnter( Collider other )
    {
        if ( !Networking.IsHost ) return;
        if ( !_available ) return;
        if ( !other.GameObject.Tags.Has( "player" ) ) return;

        var health = other.GameObject.Components.GetInAncestorsOrSelf<Health>();
        if ( health is null || !health.IsAlive ) return;
        if ( health.Current >= health.MaxHealth ) return;

        health.Current = MathX.Clamp( health.Current + HealAmount, 0f, health.MaxHealth );

        _available = false;
        PlayPickupEffect();
        _ = RespawnAsync();
    }

    public void OnTriggerExit( Collider other ) { }

    [Rpc.Broadcast]
    void PlayPickupEffect()
    {
        if ( Visual.IsValid() )
            Visual.Enabled = false;

        if ( PickupEffect.IsValid() )
        {
            var fx = PickupEffect.Clone( WorldPosition );
            fx.BreakFromPrefab();
        }

        Sound.Play( "pickup.health", WorldPosition );
    }

    [Rpc.Broadcast]
    void RespawnVisual()
    {
        if ( Visual.IsValid() )
            Visual.Enabled = true;
    }

    async Task RespawnAsync()
    {
        await Task.DelaySeconds( RespawnTime );
        if ( !this.IsValid() ) return;

        _available = true;
        RespawnVisual();
    }
}
```

To wire this up:
1. Give the GameObject a `BoxCollider` (or `SphereCollider`) and switch `IsTrigger` to `true`.
2. Attach the `HealthPickup` component to that GameObject.
3. Point the `Visual` property at the child `ModelRenderer`.
4. Tag the root player GameObject `"player"`, that's what the trigger check looks for.

Worth flagging:
- The host is the only machine that processes the pickup. Everyone else just observes the
  synced `_available` value and receives the RPCs as they land.
- Putting `[Sync]` on the backing field means a player joining mid-match still sees
  already-consumed pickups as consumed, instead of everything looking freshly available.
- `Task.DelaySeconds` running on a `Component` cancels itself automatically once the
  pickup is destroyed, courtesy of the implicit `Component.Task` scope.
- Leaving `IsTrigger = true` off the collider turns this into a solid wall: physics
  collisions fire instead of trigger events, and the pickup blocks the player outright.

***

## Example 11: Walk Up and Press E (`IPressable`)

The previous example triggers on overlap, walking straight into something. This one
needs a deliberate action instead, routed through the stock `PlayerController`'s USE
pipeline. Reach for it with doors, buttons, levers, vending machines, world pickups, and
anywhere NPC dialogue needs to hook in.

**The one thing to get right:** `Press()` executes on the **pressing client**, because
the whole pipeline runs from `PlayerController.OnUpdate` inside `if ( !IsProxy )`. That
makes it local input handling rather than a network event. Anything authoritative, giving
money, spawning something, changing shared state, needs an `[Rpc.Host]` that re-checks
everything instead of trusting the client's word. It's exactly how the engine builds its
own `Door`.

```csharp
using Sandbox;
using System.Linq;

public sealed class Vendor : Component, Component.IPressable
{
    [Property] public int Price { get; set; } = 50;
    [Property] public GameObject ProductPrefab { get; set; }

    [Sync( SyncFlags.FromHost )] public int Stock { get; set; } = 5;

    // ---- runs on the pressing client ----

    public bool CanPress( Component.IPressable.Event e )
    {
        // Gate the prompt as well as the press. Cheap, local checks only:
        // this is called every frame for whatever you're looking at.
        return Stock > 0 && e.Source.GameObject.GetComponent<Wallet>() is { } w && w.Money >= Price;
    }

    public Component.IPressable.Tooltip? GetTooltip( Component.IPressable.Event e )
    {
        if ( Stock <= 0 )
            return new( "Vendor", "block", "Sold out", Enabled: false );

        return new( "Vendor", "shopping_cart", $"Buy for ${Price}" );
    }

    public bool Press( Component.IPressable.Event e )
    {
        // Client-side: predict/feedback only. Never mutate authoritative state here.
        Sound.Play( "ui.button.press", WorldPosition );

        BuyOnHost();     // <- the real work crosses the wire
        return true;     // true => Release() will be called when USE is let go
    }

    public void Release( Component.IPressable.Event e ) { }

    public void Hover( Component.IPressable.Event e ) => Highlight( true );
    public void Blur( Component.IPressable.Event e )  => Highlight( false );

    // ---- runs on the host ----

    [Rpc.Host]
    void BuyOnHost()
    {
        var buyer = Rpc.Caller;                       // never trust the client's word for this
        var player = Scene.GetAllComponents<Wallet>()
                          .FirstOrDefault( x => x.Network.Owner == buyer );

        if ( !player.IsValid() ) return;

        // Re-validate everything CanPress checked. The client could be lying,
        // and state may have changed between their frame and ours.
        if ( Stock <= 0 ) return;
        if ( player.Money < Price ) return;
        if ( player.WorldPosition.Distance( WorldPosition ) > 160f ) return;   // reach + slack

        player.Money -= Price;
        Stock--;

        ProductPrefab.Clone( WorldPosition + Vector3.Up * 32f )
                     .NetworkSpawn( buyer );          // explicit owner, never bare NetworkSpawn()
    }

    void Highlight( bool on ) { /* tint the renderer */ }
}
```

Wiring it up:
1. Give the GameObject a **non-trigger** `Collider` (the USE trace calls `HitTriggers()`,
   so a trigger would technically work, but a solid collider is what stops players
   pressing through walls).
2. Attach the `Vendor` component. There's no tag to add and nothing to register
   elsewhere, implementing the interface is the entire hookup.
3. The player GameObject needs the stock `PlayerController`, with `EnablePressing` and
   `UseLookControls` left at their default of on.

Worth flagging:
- **`Press` is the only method that's mandatory.** Every other member of `IPressable`
  ships with a default body, so skip whatever this Vendor doesn't need.
- `e.Source` names the `PlayerController` doing the pressing; reach the player's
  GameObject itself through `e.Source.GameObject`.
- Resolution happens via `GetComponentsInParent<IPressable>( includeSelf: true )`,
  starting from whichever collider got hit, so the component doesn't have to sit directly
  on that collider, a parent works too.
- The press trace reaches `ReachLength` (130 units) from the eye and retries at radius 0,
  2 and 4, sparing small props from needing pixel-perfect aim. Distance is measured to
  the nearest point on any child collider, not the object's origin.
- `CanPress` runs every single frame against whatever's under the crosshair, and it
  controls the tooltip as much as the press itself. Keep it cheap and local, a trace or a
  database lookup has no business running here.
- The tooltip prompt itself comes from `playerController.Tooltips`, rebuilt each frame;
  draw your own UI from that.
- A `Press` that returns `true` is a promise that `Release` will follow. Cover the ways
  that promise can go unfulfilled: the player dying, disconnecting, or walking out of
  range mid-press, which is when the controller calls `StopPressing` once distance passes
  `ReachLength`.

***

## Idioms Worth Memorizing

These snippets turn up constantly. Get them into muscle memory.

### Finding the local player

```csharp
var localPlayer = Scene.GetAllComponents<Player>().FirstOrDefault( p => !p.IsProxy );
```

If you're calling this every frame, cache the result in `OnStart` instead.

### Host-only logic

```csharp
if ( !Networking.IsHost ) return;
```

### Owner-only logic

```csharp
if ( IsProxy ) return;
```

### Everyone, driven by authoritative data

```csharp
[Rpc.Broadcast]
void DoThing( Vector3 pos ) { /* runs everywhere */ }
```

### Delayed work

```csharp
_ = DelayedWork();

async Task DelayedWork()
{
    await Task.DelaySeconds( 2f );
    if ( !this.IsValid() ) return;   // cancellation guard
    DoTheThing();
}
```

### Crosshair trace

```csharp
var ray = Scene.Camera.ScreenNormalToRay( new Vector3( 0.5f, 0.5f, 0f ) );
var tr = Scene.Trace.Ray( ray, 5000f )
    .IgnoreGameObjectHierarchy( GameObject.Root )
    .UseHitboxes( true )
    .Run();
```

### Iterating every player

```csharp
foreach ( var player in Scene.GetAllComponents<Player>() )
{
    if ( player.IsProxy ) continue;
    // ...
}
```

### Effect broadcast without a synced GameObject

```csharp
[Rpc.Broadcast( NetFlags.Unreliable )]
void PlayEffect( Vector3 position )
{
    EffectPrefab.Clone( position ).BreakFromPrefab();
}
```

### Destroyed-reference guard

```csharp
if ( !target.IsValid() ) return;           // works on GameObject, Component, and any IValid
```

### Filtering collisions by tag instead of type

```csharp
if ( !other.GameObject.Tags.Has( "player" ) ) return;
```

### Playing a sound once

```csharp
Sound.Play( "ui.click" );                                // 2D
Sound.Play( "impact.metal", hitPosition );               // 3D
GameObject.PlaySound( soundEventAsset, Vector3.Zero );   // attached to GO
```

***

## Common Mistakes

Catch yourself writing any of these, and stop.

| Instead of | Use | Reason |
|:--|:--|:--|
| `Update()` | `protected override void OnUpdate()` | There's no bare `Update()` in s&box; the lifecycle method is `OnUpdate`. |
| `GetComponent<T>()` in a hot loop | Cache the reference inside `OnStart` | Lookup isn't free, and 60 calls a second across 100 objects adds up to real waste. |
| Polling `Input.*` from inside `OnFixedUpdate` | Read it in `OnUpdate`, cache the result, and consume that in `OnFixedUpdate` | Input polling tracks frame rate rather than the physics tick, so `Pressed`/`Released` states can slip through unnoticed. |
| Writing to a `[Sync]` field from a proxy | Guard the write with `if ( IsProxy ) return;` | Clients would stomp on each other's writes, and the value snaps right back on the next sync anyway. |
| Calling `Scene.GetAllComponents<T>()` from `OnUpdate` on every object | Cache the result once, or switch to scene events | Cost scales as scene size times component count, and that eats the frame budget fast. |
| `Instantiate(prefab)` / `gameObject.SetActive(false)` | `prefab.Clone(pos)` / `go.Enabled = false` | Both are Unity calls; neither exists in s&box. |
| `Debug.Log(...)` | `Log.Info(...)` | That's the Unity name; s&box doesn't have it. |
| `new Thread(...)` or raw `System.IO.File` | s&box `FileSystem.Data` and async/await | The sandbox whitelist blocks most of `System.IO` outright. |
| `transform.position = ...` | `WorldPosition = ...` | `transform` isn't a field here; reach the object's placement through the `WorldPosition` / `LocalPosition` shortcuts instead. |
| Letting a proxy call `[Rpc.Broadcast]` methods without checking ownership | Add whichever of `IsProxy` / `Networking.IsHost` fits the call | Every proxy that re-fires the RPC multiplies the message traffic. |
| Constructing UI imperatively in C# on every frame | Razor with `BuildHash` | Razor's diffing is cheap; rebuilding the whole DOM from scratch every frame isn't. |
| Writing authoritative state straight from `IPressable.Press` | Move the write into an `[Rpc.Host]` that re-validates everything | `Press` executes on the pressing client, so that write either silently vanishes or blindly trusts whatever the client sent. |
| `go.NetworkSpawn()` with no arguments | `go.NetworkSpawn( Connection.Host )` or `( owner )` | The bare call assigns ownership to `Connection.Local`, whichever machine happened to run that line. |
| Expecting a scene-placed object to replicate `[Sync]` state | Call `NetworkSpawn` on it, or knowingly accept snapshot-only behavior | `NetworkMode.Snapshot` is the default and doesn't live-sync at all, but RPCs keep working regardless, so the gap stays invisible until it isn't. |
| Clearing a `NetList<T>` by reassigning the property | `list.Clear()` | A freshly assigned list never gets wired into the network table, and it loses its proxy guard in the process. |
| `[Change]` on a `NetList` / `NetDictionary` property | Subscribe to the collection's `OnChanged` field instead | `[Change]` only wraps the property setter, so mutating individual elements never trips it. |
| `[GameResource( "Title", "ext", "desc" )]` | `[AssetType( Name = ..., Extension = ... )]` | Obsolete across the whole engine, and a build failure the moment `TreatWarningsAsErrors` is on. |
| Calling `Model.Load(path)` and skipping the null check | Check for null and fall back to `Model.Error` | A path that fails to resolve comes back null, not the placeholder model. |
| Reaching for `Prop` on something purely decorative or non-destructible | `ModelRenderer` + `ModelCollider` (+ `Rigidbody`) | `Prop` drags along synced `Health`, `IDamageable`, and gib behavior nobody asked for. |

***

*See the topical references (`01_SCENE.md`, `02_COMPONENTS.md`, `04_NETWORKING.md`,
`05_INPUT_PHYSICS.md`, `03_UI.md`) for exhaustive API details. This file is for
patterns and shape; those are for signatures and specifics.*
