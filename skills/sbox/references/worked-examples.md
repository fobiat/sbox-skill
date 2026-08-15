<!--
  s&box Skill : worked-examples.md

  Complete worked examples, from an FPS controller to a press-E vendor.

  Author  : Kyle (fobiat) <kyle@fobiat.dev>
  Links   : https://fobiat.dev/   https://github.com/fobiat
  Engine  : s&box 26.08.05
  Licence : MIT. Describes an API surface derived from Facepunch/sbox-public,
            which is MIT licensed. See LICENSE at the repository root.
-->

# Patterns & Examples

Complete, idiomatic s&box code, read out of the s&box engine source at version 26.08.05
and checked against it. Each example below is runnable, not a placeholder fragment. Use
these as the structural template for your own work: copy one, then trim what you don't
need rather than building up from nothing.

These examples assume you've read the topical references (`scene-and-components.md`,
`component-library.md`, `multiplayer.md`, `input-traces-and-physics.md`, `razor-interfaces.md`). They are
intentionally verbose so there's no guesswork left when you adapt them.

---

## Conventions Used Here

- Components are `sealed` unless inheritance is needed.
- `[Property]` exposes state to the inspector and to prefab serialization.
- `OnFixedUpdate` for movement and physics. `OnUpdate` for camera, input polling, visuals.
- `Scene.Get<T>()` / `Components.Get<T>()` for scene queries, not `FindObjectOfType`.
- `if ( IsProxy ) return;` at the top of any networked input/movement update.
- `Log.Info(...)` / `Log.Warning(...)`, never `Console.WriteLine` or `System.IO`.

---

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

Key points:
- `[Sync, Change( nameof( OnHealthChanged ) )]` fires the change callback on every client
  when the owner changes health, proxies included.
- `IDamageable.OnDamage` takes `in DamageInfo`, a `ref readonly` struct, so calling it
  doesn't allocate.
- Only the authoritative side (non-proxy) mutates `Current`. The change event is what
  replicates the result to everyone else.
- Scene events (`IHealthEvents.Post`) are local-only. If every client needs to see death
  effects, drive those from a `[Rpc.Broadcast]` instead.

---

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

Setup in the scene:
1. GameObject with `FirstPersonController` + `CharacterController` (auto-added).
2. A child `GameObject` named `CameraPivot` with a `CameraComponent`. Drag it into the `CameraPivot` property.
3. Make sure the `CharacterController`'s `Height` / `Radius` match your player size (e.g., 72 / 16).

`OnFixedUpdate` runs movement rather than `OnUpdate` because the character controller
does solver iterations and ground-sticking on a fixed step. Drive it from a variable-rate
update instead and you get jitter and jumps that land differently depending on frame rate.

---

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

Key points:
- Tracing runs only on the shooter (`if ( IsProxy ) return;`), and damage application
  follows locally from that same trace.
- The `[Rpc.Broadcast]` runs on **every** client including the shooter, so effects show
  up everywhere without you having to special-case the local player.
- `NetFlags.Unreliable` is correct for cosmetic effects: missing a muzzle flash once in a
  while is fine, and the reliable alternative is needless network spam for something
  nobody will notice dropped.
- Effects use `BreakFromPrefab()` so they become standalone GameObjects that self-destruct
  (assuming the prefab carries a `TemporaryEffect` component).
- `IgnoreGameObjectHierarchy( GameObject.Root )` stops self-hits even when the weapon is a
  child several levels deep in the player rig.

---

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

Key points:
- `GameObjectSystem<GameManager>` is auto-instantiated per scene, so there's no component
  to drag into the hierarchy.
- `ISceneStartup.OnHostInitialize()` runs once, on host, after the scene has loaded, which
  is exactly the window you want for lobby creation.
- `INetworkListener.OnActive` fires once the connecting client has fully loaded the scene
  and is ready to receive gameplay, not the moment they connect.
- `NetworkSpawn( connection )` assigns ownership to that client so they control their own
  player from the first tick.
- The `OnDisconnected` sweep matters because `NetworkOrphaned.Destroy` is only the default
  for **newly** spawned objects. Anything transferred ownership later won't necessarily
  follow that default, so an explicit sweep is the safe bet.

---

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

Key points:
- `[Sync]` is **owner to everyone** by default. Only the owner can assign it, unless you
  pass `SyncFlags.FromHost`.
- `[Rpc.Owner]` delivers to whichever client owns this `GameObject`, which is the shape
  you want for "tell the victim they got hit."
- `[Rpc.Broadcast]` runs on all clients plus the host, for cosmetic or shared effects.
- `[Rpc.Host]` runs only on the host, for authoritative score, economy, or any state
  change that needs validating before it's trusted.
- `Scene.Directory.FindByGuid(...)` is how you pass a `GameObject` reference through an
  RPC without serializing the full reference: cheaper on the wire, and it still resolves
  correctly for a client who joined after the object was created.
- `IGameObjectNetworkEvents.NetworkOwnerChanged` fires on every client, so it's the hook
  for switching first-person / third-person visuals whenever ownership moves.

---

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

Key points:
- `BuildHash` is the **only** way to make the Razor tree rebuild cheaply. Include every
  value your template reads, or a change to something you left out won't show up.
- `pointer-events: none` on the root lets game input pass through underneath it. Set it
  to `all` on any interactive sub-panel that needs clicks.
- `:intro` / `:outro` are s&box-specific transitions that fire when a panel is created or
  deleted. Paired with `Delete()`, that's animated kill-feed entries with no extra code.
- `LocalPlayer` gets re-queried every `BuildHash` evaluation, which is fine here because
  it's a single scene-wide `GetAllComponents` call, O(n) but small. In a large scene,
  cache the reference in `OnStart` instead.

---

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

Key points:
- `GetOrAddComponent<Rigidbody>()` is safe to call in `OnStart`: it's idempotent, so
  hot-reloading the script doesn't leave you with a duplicate.
- `_ = FuseTimer();` kicks off an async countdown without awaiting it. Because
  `Component.Task` is scoped to the GameObject, destroying the grenade early cancels the
  await for you, no manual cleanup needed.
- `Scene.Trace.Sphere( r, origin, origin ).RunAll()` returns every overlapping physics
  shape for radius damage, cheaper than iterating every component in the scene and doing
  a distance check on each.
- `WithAnyTags` is the broad-phase filter here. Filtering by tag rather than type check
  keeps the grenade from needing to know what a `Player` even is.
- `falloff = 1 - distance / radius` is a linear falloff. Swap in `MathX.LerpInverse` if
  you want a different curve.

---

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

Key points:
- Perception runs at 4 Hz (`_timeSinceDecision > 0.25f`), not on every fixed tick. An
  enemy doesn't need to reconsider its target 50 times a second, and running perception
  that often is the first thing that makes enemy count expensive.
- `Agent.MoveTo` is idempotent, so calling it every tick with the same target costs
  nothing extra. `Agent.Stop()` before a melee swing still matters though, it's what
  stops the agent overshooting into the target.
- `Scene.NavMesh.GetRandomPoint(origin, radius)` returns `Vector3?`, `null` when no
  NavMesh exists or the point can't be sampled. Falling back to the current position
  keeps a missing NavMesh from throwing instead of just standing still.
- `Agent.Velocity.Length` drives the anim graph's move-speed parameter. The same pattern
  works with `CitizenAnimationHelper.WithVelocity` if you're using the stock rig.

---

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

Key points:
- `Prefab.Clone(pos, rot)` creates an instance with a live link back to the source
  prefab, so future edits to the prefab asset propagate to it. Only call
  `BreakFromPrefab()` if you specifically need to sever that link.
- `NetworkSpawn()` must only be called on the authority. The `Networking.IsHost` gate is
  what stops every client from spawning its own copy if this component happens to run on
  clients too.
- `Vector3.Random` returns a unit-length random direction, multiply it to whatever
  radius you actually want.
- `_alive.RemoveAll( go => !go.IsValid() )` is the idiomatic way to prune destroyed
  references, since `GameObject.IsValid()` goes `false` right after `Destroy()`.
- Parenting spawned objects under the spawner's own `GameObject` gives free cleanup in
  `OnDestroy` and a single place to disable the whole set with `GameObject.Enabled = false`.

---

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

Setup:
1. GameObject with `BoxCollider` (or `SphereCollider`), `IsTrigger` set to `true`.
2. Add the `HealthPickup` component.
3. Drag the child `ModelRenderer` into the `Visual` property.
4. Mark players with the `"player"` tag (on the root player GameObject).

Key points:
- Only the host handles the pickup. Other clients just see the `_available` sync value
  and the RPCs land on their end automatically.
- `[Sync]` on a backing field exposes the state to clients so a player who joins late
  still sees pickups that are currently consumed, instead of seeing them all as available.
- `Task.DelaySeconds` on a `Component` auto-cancels when the pickup is destroyed, via the
  implicit `Component.Task` scope.
- Don't forget to set `IsTrigger = true` on the collider. Skip it and you get physics
  collisions instead of trigger events, and the pickup turns into a solid wall.

---

## Example 11: Walk Up and Press E (`IPressable`)

Example 10 fires when you *walk into* something. This one fires when you *deliberately
use* it, the stock `PlayerController`'s USE pipeline. It's the pattern for doors, buttons,
levers, vending machines, world pickups, and NPC dialogue.

**The one thing to get right:** `Press()` executes on the **pressing client**, because
the whole pipeline runs from `PlayerController.OnUpdate` inside `if ( !IsProxy )`. It's
local input handling, not a network event. Anything authoritative, spawning, giving money,
changing shared state, has to be an `[Rpc.Host]` that re-validates rather than trusting
what the client sent. This is exactly how the engine's own `Door` is built.

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

Setup:
1. GameObject with a **non-trigger** `Collider` (the USE trace calls `HitTriggers()`, so
   a trigger works too, but a solid collider is what stops you pressing through walls).
2. Add the `Vendor` component. No tags, no listener registration: implementing the
   interface is the whole wiring.
3. The player needs the stock `PlayerController` with `EnablePressing` (default on) and
   `UseLookControls` (default on).

Key points:
- **Only `Press` is required.** Everything else on `IPressable` has a default body, so
  omit whatever you don't need.
- `e.Source` is the pressing `PlayerController`; `e.Source.GameObject` is the player.
- The interface is found with `GetComponentsInParent<IPressable>( includeSelf: true )`
  from the collider that was hit, so it can live on a parent of the visual or collider
  rather than directly on it.
- Reach is `ReachLength` (130 units) from the eye, retried at radius 0, 2 and 4 so small
  props aren't fiddly to click. Distance is measured to the closest point on any child
  collider, not the object's origin.
- `CanPress` runs every frame for whatever you're looking at, and gates the tooltip as
  well as the press. Keep it cheap and local, this is not the place for a trace or a
  database lookup.
- Draw your own prompt from `playerController.Tooltips`, it's rebuilt every frame.
- Returning `true` from `Press` promises a later `Release`. Handle the edge cases: the
  player can die, disconnect, or walk out of range mid-press (the controller calls
  `StopPressing` when distance exceeds `ReachLength`).

---

## Quick-Reference Patterns

These are idioms that show up everywhere. Keep them in muscle memory.

### Local-player query

```csharp
var localPlayer = Scene.GetAllComponents<Player>().FirstOrDefault( p => !p.IsProxy );
```

Cache in `OnStart` if you query it every frame.

### "Do this on the host only"

```csharp
if ( !Networking.IsHost ) return;
```

### "Do this on the owner only"

```csharp
if ( IsProxy ) return;
```

### "Do this on everyone with authoritative data"

```csharp
[Rpc.Broadcast]
void DoThing( Vector3 pos ) { /* runs everywhere */ }
```

### Schedule work after a delay

```csharp
_ = DelayedWork();

async Task DelayedWork()
{
    await Task.DelaySeconds( 2f );
    if ( !this.IsValid() ) return;   // cancellation guard
    DoTheThing();
}
```

### Trace from crosshair

```csharp
var ray = Scene.Camera.ScreenNormalToRay( new Vector3( 0.5f, 0.5f, 0f ) );
var tr = Scene.Trace.Ray( ray, 5000f )
    .IgnoreGameObjectHierarchy( GameObject.Root )
    .UseHitboxes( true )
    .Run();
```

### Find all players and iterate

```csharp
foreach ( var player in Scene.GetAllComponents<Player>() )
{
    if ( player.IsProxy ) continue;
    // ...
}
```

### Broadcast an effect without syncing a GameObject

```csharp
[Rpc.Broadcast( NetFlags.Unreliable )]
void PlayEffect( Vector3 position )
{
    EffectPrefab.Clone( position ).BreakFromPrefab();
}
```

### Guard against destroyed references

```csharp
if ( !target.IsValid() ) return;           // works on GameObject, Component, and any IValid
```

### Tag-based collision filter (cheaper than type checks)

```csharp
if ( !other.GameObject.Tags.Has( "player" ) ) return;
```

### One-shot sound

```csharp
Sound.Play( "ui.click" );                                // 2D
Sound.Play( "impact.metal", hitPosition );               // 3D
GameObject.PlaySound( soundEventAsset, Vector3.Zero );   // attached to GO
```

---

## Anti-Patterns

If you find yourself writing one of these, stop.

| Wrong | Right | Why |
|---|---|---|
| `Update()` | `protected override void OnUpdate()` | s&box isn't Unity; the virtual method is `OnUpdate`. |
| `GetComponent<T>()` in a hot loop | Cache the reference in `OnStart` | Component lookup is cheap but not free; 60x per second on 100 objects is waste. |
| Reading `Input.*` in `OnFixedUpdate` | Read in `OnUpdate`, store, consume in `OnFixedUpdate` | Input polling is tied to frame rate, not physics tick. `Pressed`/`Released` may be missed. |
| Mutating `[Sync]` fields on a proxy | Guard with `if ( IsProxy ) return;` | Clients overwrite each other; the value snaps back on the next sync. |
| `Scene.GetAllComponents<T>()` in `OnUpdate` on every object | Cache or use scene events | O(scene) x O(components) quickly becomes the frame budget. |
| `Instantiate(prefab)` / `gameObject.SetActive(false)` | `prefab.Clone(pos)` / `go.Enabled = false` | These are Unity APIs that don't exist here. |
| `Debug.Log(...)` | `Log.Info(...)` | Unity name, doesn't exist. |
| `new Thread(...)` or raw `System.IO.File` | s&box `FileSystem.Data` and async/await | Most of `System.IO` is blocked by the sandbox whitelist. |
| `transform.position = ...` | `WorldPosition = ...` | `transform` isn't a field; Transform access is via `WorldPosition` / `LocalPosition` shortcuts. |
| Calling `[Rpc.Broadcast]` methods on proxies without owner check | Guard with `IsProxy` / `Networking.IsHost` as appropriate | Every proxy re-firing an RPC multiplies the message count. |
| Building UI in C# imperatively every frame | Razor with `BuildHash` | Razor diffing is cheap; rebuilding the DOM from scratch is not. |
| Mutating authoritative state directly inside `IPressable.Press` | Do it in an `[Rpc.Host]` and re-validate there | `Press` runs on the pressing client. The write either gets dropped silently or trusts a client. |
| `go.NetworkSpawn()` with no arguments | `go.NetworkSpawn( Connection.Host )` or `( owner )` | The bare form owns to `Connection.Local`, whoever ran the line. |
| Assuming a scene-placed object replicates `[Sync]` | `NetworkSpawn` it, or accept snapshot-only state | `NetworkMode.Snapshot` is the default and doesn't live-sync, but RPCs still work, so it looks fine until it doesn't. |
| Reassigning a `NetList<T>` property to clear it | `list.Clear()` | The replacement never gets wired to the network table and loses its proxy guard. |
| `[Change]` on a `NetList` / `NetDictionary` property | Subscribe to the collection's `OnChanged` field | `[Change]` wraps the property setter, so element mutations never fire it. |
| `[GameResource( "Title", "ext", "desc" )]` | `[AssetType( Name = ..., Extension = ... )]` | Obsolete engine-wide; a build failure under `TreatWarningsAsErrors`. |
| Using `Model.Load(path)` without a null check | Check for null, fall back to `Model.Error` | A path that doesn't resolve returns null, not the placeholder. |
| Using `Prop` for a decorative or non-destructible object | `ModelRenderer` + `ModelCollider` (+ `Rigidbody`) | `Prop` brings synced `Health`, `IDamageable` and gibs you didn't ask for. |

---

*See the topical references (`scene-and-components.md`, `component-library.md`, `multiplayer.md`,
`input-traces-and-physics.md`, `razor-interfaces.md`) for exhaustive API details. This file is for
patterns and shape; those are for signatures and specifics.*
