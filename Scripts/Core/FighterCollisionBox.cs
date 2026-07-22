using System;
using Godot;

namespace ModularFighter.Core;

public abstract partial class FighterCollisionBox : Area2D
{
	[Export] public FighterBoxKind Kind { get; protected set; } = FighterBoxKind.Hurtbox;
	[Export] public int LifetimeFrames { get; set; } = -1;
	[Export] public bool DestroyWhenLifetimeEnds { get; set; } = true;
	[Export] public Vector2 ShapeLocalOffset { get; private set; }

	public FighterController OwnerFighter { get; private set; }
	public FighterBoxFrame SourceFrame { get; private set; }
	public CollisionShape2D ShapeNode { get; private set; }
	public Shape2D Shape => ShapeNode?.Shape;

	public event Action<FighterCollisionBox, Area2D> BoxAreaEntered;

	public override void _Ready()
	{
		AreaEntered += OnAreaEnteredInternal;
	}

	public override void _ExitTree()
	{
		AreaEntered -= OnAreaEnteredInternal;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (LifetimeFrames < 0) return;
		LifetimeFrames--;
		if (LifetimeFrames > 0 || !DestroyWhenLifetimeEnds) return;
		QueueFree();
	}

	public void Initialize(FighterController owner, FighterBoxFrame sourceFrame, int lifetimeFrames = -1,
		uint? collisionLayer = null, uint? collisionMask = null)
	{
		if (sourceFrame == null) return;
		OwnerFighter = owner;
		SourceFrame = sourceFrame;
		Kind = sourceFrame.Kind;
		InitializeRectangle(owner, sourceFrame.LocalRect, sourceFrame.MirrorWithFacing, lifetimeFrames,
			collisionLayer, collisionMask);
	}

	public void InitializeRectangle(FighterController owner, Rect2 localRect, bool mirrorWithFacing = true, int lifetimeFrames = -1,
		uint? collisionLayer = null, uint? collisionMask = null)
	{
		OwnerFighter = owner;
		LifetimeFrames = lifetimeFrames;
		ApplyCollisionLayers(collisionLayer, collisionMask);
		Monitoring = true;
		Monitorable = true;

		Rect2 facingRect = GetFacingRect(owner, localRect, mirrorWithFacing);
		var rectangle = new RectangleShape2D { Size = facingRect.Size };
		SetShapeDeferred(rectangle, facingRect.GetCenter());
	}

	public void InitializeShape(FighterController owner, Shape2D shape, Vector2 localOffset, int lifetimeFrames = -1,
		uint? collisionLayer = null, uint? collisionMask = null)
	{
		OwnerFighter = owner;
		LifetimeFrames = lifetimeFrames;
		ApplyCollisionLayers(collisionLayer, collisionMask);
		Monitoring = true;
		Monitorable = true;
		SetShapeDeferred(shape, localOffset);
	}

	/// <summary>Initializes this runtime box from a CollisionShape2D authored in a scene.</summary>
	public void InitializeShape(FighterController owner, CollisionShape2D shapeNode, int lifetimeFrames = -1,
		uint? collisionLayer = null, uint? collisionMask = null)
	{
		if (shapeNode?.Shape == null)
			throw new ArgumentException("A CollisionShape2D with a Shape resource is required.", nameof(shapeNode));
		OwnerFighter = owner;
		LifetimeFrames = lifetimeFrames;
		ApplyCollisionLayers(collisionLayer, collisionMask);
		Monitoring = true;
		Monitorable = true;
		CallDeferred(MethodName.ApplyShapeNodeDeferred, (Shape2D)shapeNode.Shape.Duplicate(), shapeNode.Transform);
	}

	public void AttachTo(Node parent)
	{
		if (GetParent() == parent) return;
		parent.CallDeferred(Node.MethodName.AddChild, this);
	}

	public void SetShapeDeferred(Shape2D shape, Vector2 localOffset)
	{
		CallDeferred(MethodName.ApplyShapeDeferred, shape, localOffset);
	}

	private void ApplyShapeDeferred(Shape2D shape, Vector2 localOffset)
	{
		ShapeNode ??= new CollisionShape2D { Name = "CollisionShape2D" };
		if (ShapeNode.GetParent() == null) AddChild(ShapeNode);
		ShapeNode.Shape = shape;
		ShapeNode.Position = localOffset;
		ShapeLocalOffset = localOffset;
	}

	private void ApplyShapeNodeDeferred(Shape2D shape, Transform2D localTransform)
	{
		ShapeNode ??= new CollisionShape2D { Name = "CollisionShape2D" };
		if (ShapeNode.GetParent() == null) AddChild(ShapeNode);
		ShapeNode.Shape = shape;
		ShapeNode.Transform = localTransform;
		ShapeLocalOffset = localTransform.Origin;
	}

	private void ApplyCollisionLayers(uint? collisionLayer, uint? collisionMask)
	{
		if (collisionLayer.HasValue) CollisionLayer = collisionLayer.Value;
		if (collisionMask.HasValue) CollisionMask = collisionMask.Value;
	}

	private void OnAreaEnteredInternal(Area2D area)
	{
		BoxAreaEntered?.Invoke(this, area);
	}

	private static Rect2 GetFacingRect(FighterController owner, Rect2 localRect, bool mirrorWithFacing)
	{
		if (!mirrorWithFacing || owner == null || owner.Facing >= 0) return localRect;
		return new Rect2(new Vector2(-localRect.Position.X - localRect.Size.X, localRect.Position.Y), localRect.Size);
	}
}
