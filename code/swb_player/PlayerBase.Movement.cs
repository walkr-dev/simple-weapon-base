using Sandbox.Citizen;
using SWB.Shared;

namespace SWB.Player;

public partial class PlayerBase
{
	// Movement Properties
	[Property] public float GroundControl { get; set; } = 4.0f;
	[Property] public float AirControl { get; set; } = 0.1f;
	[Property] public float MaxForce { get; set; } = 50f;
	[Property] public float RunSpeed { get; set; } = 290f;
	[Property] public float WalkSpeed { get; set; } = 160f;
	[Property] public float CrouchSpeed { get; set; } = 90f;
	[Property] public float JumpForce { get; set; } = 350f;

	// Member Variables
	[Sync] public Vector3 WishVelocity { get; set; } = Vector3.Zero;
	[Sync] public Angles EyeAngles { get; set; }
	[Sync] public Vector3 EyeOffset { get; set; } = Vector3.Zero;
	[Sync] public bool IsCrouching { get; set; } = false;
	[Sync] public bool IsRunning { get; set; } = false;

	public bool IsOnGround => CharacterController.IsOnGround;
	public Vector3 Velocity => CharacterController.Velocity;
	public Vector3 EyePos => Head.Transform.Position + EyeOffset;

	public CharacterController CharacterController { get; set; }
	public CitizenAnimationHelper AnimationHelper { get; set; }

	void OnMovementAwake()
	{
		CharacterController = Components.Get<CharacterController>();
		AnimationHelper = Components.Get<CitizenAnimationHelper>();
	}

	void OnMovementUpdate()
	{
		if ( !IsProxy )
		{
			IsRunning = Input.Down( InputButtonHelper.Run );

			if ( Input.Pressed( InputButtonHelper.Jump ) )
				Jump();

			UpdateCrouch();
		}

		RotateBody();
		UpdateAnimations();
	}

	void OnMovementFixedUpdate()
	{
		if ( IsProxy ) return;
		BuildWishVelocity();
		Move();
	}

	void BuildWishVelocity()
	{
		WishVelocity = 0;

		var rot = EyeAngles.ToRotation();
		if ( Input.Down( InputButtonHelper.Forward ) ) WishVelocity += rot.Forward;
		if ( Input.Down( InputButtonHelper.Backward ) ) WishVelocity += rot.Backward;
		if ( Input.Down( InputButtonHelper.Left ) ) WishVelocity += rot.Left;
		if ( Input.Down( InputButtonHelper.Right ) ) WishVelocity += rot.Right;

		WishVelocity = WishVelocity.WithZ( 0 );
		if ( !WishVelocity.IsNearZeroLength ) WishVelocity = WishVelocity.Normal;

		if ( IsCrouching ) WishVelocity *= CrouchSpeed;
		else if ( IsRunning ) WishVelocity *= RunSpeed;
		else WishVelocity *= WalkSpeed;
	}

	void Move()
	{
		var gravity = Scene.PhysicsWorld.Gravity;

		if ( IsOnGround )
		{
			// Friction / Acceleration
			CharacterController.Velocity = CharacterController.Velocity.WithZ( 0 );
			CharacterController.Accelerate( WishVelocity );
			CharacterController.ApplyFriction( GroundControl );
		}
		else
		{
			// Air control / Gravity
			CharacterController.Velocity += gravity * Time.Delta * 0.5f;
			CharacterController.Accelerate( WishVelocity.ClampLength( MaxForce ) );
			CharacterController.ApplyFriction( AirControl );
		}

		CharacterController.Move();

		// Second half of gravity after movement (to stay accurate)
		if ( IsOnGround )
		{
			CharacterController.Velocity = CharacterController.Velocity.WithZ( 0 );
		}
		else
		{
			CharacterController.Velocity += gravity * Time.Delta * 0.5f;
		}
	}

	void RotateBody()
	{
		var targetRot = new Angles( 0, EyeAngles.ToRotation().Yaw(), 0 ).ToRotation();
		float rotateDiff = Body.Transform.Rotation.Distance( targetRot );

		if ( rotateDiff > 20f || CharacterController.Velocity.Length > 10f )
		{
			Body.Transform.Rotation = Rotation.Lerp( Body.Transform.Rotation, targetRot, Time.Delta * 2f );
		}
	}

	void Jump()
	{
		if ( !IsOnGround ) return;

		IsCrouching = false;
		CharacterController.Punch( Vector3.Up * JumpForce );
		AnimationHelper?.TriggerJump();
	}

	void UpdateAnimations()
	{
		if ( AnimationHelper is null ) return;

		AnimationHelper.WithWishVelocity( WishVelocity );
		AnimationHelper.WithVelocity( CharacterController.Velocity );
		AnimationHelper.AimAngle = EyeAngles.ToRotation();
		AnimationHelper.IsGrounded = IsOnGround;
		AnimationHelper.WithLook( EyeAngles.ToRotation().Forward, 1f, 0.75f, 0.5f );
		AnimationHelper.MoveStyle = CitizenAnimationHelper.MoveStyles.Run;
		AnimationHelper.DuckLevel = IsCrouching ? 1 : 0;
	}

	void UpdateCrouch()
	{
		if ( Input.Down( InputButtonHelper.Duck ) && !IsCrouching && IsOnGround )
		{
			IsCrouching = true;
			CharacterController.Height /= 2f;
		}

		if ( IsCrouching && !Input.Down( InputButtonHelper.Duck ) )
		{
			// Check we have space to uncrouch
			var targetHeight = CharacterController.Height * 2f;
			var upTrace = CharacterController.TraceDirection( Vector3.Up * targetHeight );

			if ( !upTrace.Hit )
			{
				IsCrouching = false;
				CharacterController.Height = targetHeight;
			}
		}
	}
}
