﻿using Lab;
using Sandbox;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

[Library( "npc_test", Title = "Npc Test")]
public partial class NpcTest : AnimatedEntity
{
	[ConVar.Replicated]
	public static bool nav_drawpath { get; set; }

	[ConCmd.Server( "npc_clear" )]
	public static void NpcClear( )
	{
		foreach ( var npc in Entity.All.OfType<NpcTest>().ToArray() )
			npc.Delete();
	}

	float Speed;

	NavPath Path = new NavPath();
	public NavSteer Steer;

	public override void Spawn()
	{
		base.Spawn();

		SetModel( "models/citizen/citizen.vmdl" );
		EyePosition = Position + Vector3.Up * 64;
		CollisionGroup = CollisionGroup.Player;
		SetupPhysicsFromCapsule( PhysicsMotionType.Keyframed, Capsule.FromHeightAndRadius( 72, 8 ) );

		EnableHitboxes = true;

		this.SetMaterialGroup( Rand.Int( 0, 3 ) );

		new ModelEntity( "models/citizen_clothes/trousers/trousers.smart.vmdl", this );
		new ModelEntity( "models/citizen_clothes/jacket/labcoat.vmdl", this );
		new ModelEntity( "models/citizen_clothes/shirt/shirt_longsleeve.scientist.vmdl" , this );

		if ( Rand.Int(3) == 1 )
		{
			new ModelEntity( "models/citizen_clothes/hair/hair_femalebun.black.vmdl" , this );
		}
		else if ( Rand.Int( 10 ) == 1 )
		{
			new ModelEntity( "models/citizen_clothes/hat/hat_hardhat.vmdl" , this );
		}

		SetBodyGroup( 1, 0 );

		Speed = Rand.Float( 100, 300 );
	}

	public Sandbox.Debug.Draw Draw => Sandbox.Debug.Draw.Once;

	Vector3 InputVelocity;

	Vector3 LookDir;

	[Event.Tick.Server]
	public void Tick()
	{
		using var _a = Sandbox.Debug.Profile.Scope( "NpcTest::Tick" );

		InputVelocity = 0;

		if ( Steer != null )
		{
			using var _b = Sandbox.Debug.Profile.Scope( "Steer" );

			Steer.Tick( Position );

			if ( !Steer.Output.Finished )
			{
				InputVelocity = Steer.Output.Direction.Normal;
				Velocity = Velocity.AddClamped( InputVelocity * Time.Delta * 500, Speed );
			}

			if ( nav_drawpath )
			{
				Steer.DebugDrawPath();
			}
		}

		using ( Sandbox.Debug.Profile.Scope( "Move" ) )
		{
			Move( Time.Delta );
		}

		var walkVelocity = Velocity.WithZ( 0 );
		if ( walkVelocity.Length > 0.5f )
		{
			var turnSpeed = walkVelocity.Length.LerpInverse( 0, 100, true );
			var targetRotation = Rotation.LookAt( walkVelocity.Normal, Vector3.Up );
			Rotation = Rotation.Lerp( Rotation, targetRotation, turnSpeed * Time.Delta * 20.0f );
		}

		var animHelper = new CitizenAnimationHelper( this );

		LookDir = Vector3.Lerp( LookDir, InputVelocity.WithZ( 0 ) * 1000, Time.Delta * 100.0f );
		animHelper.WithLookAt( EyePosition + LookDir );
		animHelper.WithVelocity( Velocity );
		animHelper.WithWishVelocity( InputVelocity );		
	}

	protected virtual void Move( float timeDelta )
	{
		var bbox = BBox.FromHeightAndRadius( 64, 4 );
		//DebugOverlay.Box( Position, bbox.Mins, bbox.Maxs, Color.Green );

		MoveHelper move = new( Position, Velocity );
		move.MaxStandableAngle = 50;
		move.Trace = move.Trace.Ignore( this ).Size( bbox );

		if ( !Velocity.IsNearlyZero( 0.001f ) )
		{
		//	Sandbox.Debug.Draw.Once
		//						.WithColor( Color.Red )
		//						.IgnoreDepth()
		//						.Arrow( Position, Position + Velocity * 2, Vector3.Up, 2.0f );

			using ( Sandbox.Debug.Profile.Scope( "TryUnstuck" ) )
				move.TryUnstuck();

			using ( Sandbox.Debug.Profile.Scope( "TryMoveWithStep" ) )
				move.TryMoveWithStep( timeDelta, 30 );
		}

		using ( Sandbox.Debug.Profile.Scope( "Ground Checks" ) )
		{
			var tr = move.TraceDirection( Vector3.Down * 10.0f );

			if ( move.IsFloor( tr ) )
			{
				GroundEntity = tr.Entity;

				if ( !tr.StartedSolid )
				{
					move.Position = tr.EndPosition;
				}

				if ( InputVelocity.Length > 0 )
				{
					var movement = move.Velocity.Dot( InputVelocity.Normal );
					move.Velocity = move.Velocity - movement * InputVelocity.Normal;
					move.ApplyFriction( tr.Surface.Friction * 10.0f, timeDelta );
					move.Velocity += movement * InputVelocity.Normal;

				}
				else
				{
					move.ApplyFriction( tr.Surface.Friction * 10.0f, timeDelta );
				}
			}
			else
			{
				GroundEntity = null;
				move.Velocity += Vector3.Down * 900 * timeDelta;
				Sandbox.Debug.Draw.Once.WithColor( Color.Red ).Circle( Position, Vector3.Up, 10.0f );
			}
		}

		Position = move.Position;
		Velocity = move.Velocity;
	}
}
