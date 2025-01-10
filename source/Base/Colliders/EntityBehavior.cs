﻿using CombatOverhaul.Utils;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace CombatOverhaul.Colliders;


public enum ColliderTypes
{
    /// <summary>
    /// Normal damage received<br/>
    /// No special effects
    /// </summary>
    Torso,
    /// <summary>
    /// High damage received<br/>
    /// Affects: sight, bite attacks
    /// </summary>
    Head,
    /// <summary>
    /// Low damage received<br/>
    /// Affects: punch, throw and weapon attacks
    /// </summary>
    Arm,
    /// <summary>
    /// Low damage received<br/>
    /// Affects: movement, kick attacks
    /// </summary>
    Leg,
    /// <summary>
    /// Very high damage received<br/>
    /// No special effects
    /// </summary>
    Critical,
    /// <summary>
    /// No damage received<br/>
    /// No special effects
    /// </summary>
    Resistant
}

internal sealed class ColliderTypesJson
{
    public string[] Torso { get; set; } = Array.Empty<string>();
    public string[] Head { get; set; } = Array.Empty<string>();
    public string[] Arm { get; set; } = Array.Empty<string>();
    public string[] Leg { get; set; } = Array.Empty<string>();
    public string[] Critical { get; set; } = Array.Empty<string>();
    public string[] Resistant { get; set; } = Array.Empty<string>();
}

public sealed class CollidersEntityBehavior : EntityBehavior
{
    public CollidersEntityBehavior(Entity entity) : base(entity)
    {
    }

    public CuboidAABBCollider BoundingBox { get; private set; }
    public bool HasOBBCollider { get; private set; } = false;
    public bool UnprocessedElementsLeft { get; set; } = false;
    public HashSet<string> ShapeElementsToProcess { get; private set; } = new();
    public Dictionary<string, ColliderTypes> CollidersTypes { get; private set; } = new();
    public Dictionary<string, ShapeElementCollider> Colliders { get; private set; } = new();
    public override string PropertyName() => "combatoverhaul:colliders";
    internal ClientAnimator? Animator { get; set; }
    static public bool RenderColliders { get; set; } = false;

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        try
        {
            if (!attributes.KeyExists("elements"))
            {
                LoggerUtil.Error(entity.Api, this, $"Error on parsing behavior properties for entity: {entity.Code}. 'elements' attribute was not found.");
                return;
            }

            ColliderTypesJson types = attributes["elements"].AsObject<ColliderTypesJson>();
            foreach (string collider in types.Torso)
            {
                CollidersTypes.Add(collider, ColliderTypes.Torso);
                ShapeElementsToProcess.Add(collider);
            }
            foreach (string collider in types.Head)
            {
                CollidersTypes.Add(collider, ColliderTypes.Head);
                ShapeElementsToProcess.Add(collider);
            }
            foreach (string collider in types.Arm)
            {
                CollidersTypes.Add(collider, ColliderTypes.Arm);
                ShapeElementsToProcess.Add(collider);
            }
            foreach (string collider in types.Leg)
            {
                CollidersTypes.Add(collider, ColliderTypes.Leg);
                ShapeElementsToProcess.Add(collider);
            }
            foreach (string collider in types.Critical)
            {
                CollidersTypes.Add(collider, ColliderTypes.Critical);
                ShapeElementsToProcess.Add(collider);
            }
            foreach (string collider in types.Resistant)
            {
                CollidersTypes.Add(collider, ColliderTypes.Resistant);
                ShapeElementsToProcess.Add(collider);
            }

            UnprocessedElementsLeft = true;
            HasOBBCollider = true;

        }
        catch (Exception exception)
        {
            LoggerUtil.Error(entity.Api, this, $"Error on parsing behavior properties for entity: {entity.Code}. Exception:\n{exception}");
            UnprocessedElementsLeft = false;
            HasOBBCollider = false;
        }
    }
    public override void OnGameTick(float deltaTime)
    {
        if (HasOBBCollider && Animator != null && entity.Api is ICoreClientAPI clientApi && entity.IsRendered) RecalculateColliders(Animator, clientApi);
    }

    public void SetColliderElement(ShapeElement element)
    {
        if (element?.Name == null) return;

        if (UnprocessedElementsLeft && ShapeElementsToProcess.Contains(element.Name) && !Colliders.ContainsKey(element.Name))
        {
            Colliders.Add(element.Name, new ShapeElementCollider(element));
            ShapeElementsToProcess.Remove(element.Name);
            UnprocessedElementsLeft = ShapeElementsToProcess.Count > 0;
        }
    }
    public void Render(ICoreClientAPI api, EntityAgent entityPlayer, EntityShapeRenderer renderer, int color = ColorUtil.WhiteArgb)
    {
        if (api.World.Player.Entity.EntityId == entityPlayer.EntityId) return;
        if (!HasOBBCollider) return;

        IShaderProgram? currentShader = api.Render.CurrentActiveShader;
        currentShader?.Stop();

        foreach ((string id, ShapeElementCollider collider) in Colliders)
        {
            if (!collider.HasRenderer)
            {
                collider.Renderer ??= renderer;
                collider.HasRenderer = true;
            }

            if (RenderColliders) collider.Render(api, entityPlayer, _colliderColors[CollidersTypes[id]]);
        }

        currentShader?.Use();
    }
    public bool Collide(Vector3 segmentStart, Vector3 segmentDirection, out string collider, out float parameter, out Vector3 intersection)
    {

        parameter = float.MaxValue;
        bool foundIntersection = false;
        collider = "";
        intersection = Vector3.Zero;

        if (!HasOBBCollider)
        {
            CuboidAABBCollider AABBCollider = new(entity);
            bool collided = AABBCollider.Collide(segmentStart, segmentDirection, out parameter);
            intersection = segmentStart + parameter * segmentDirection;
            return collided;
        }

        if (!BoundingBox.Collide(segmentStart, segmentDirection, out _))
        {
            return false;
        }

        foreach ((string key, ShapeElementCollider shapeElementCollider) in Colliders)
        {
            if (shapeElementCollider.Collide(segmentStart, segmentDirection, out float currentParameter, out Vector3 currentIntersection) && currentParameter < parameter)
            {
                parameter = currentParameter;
                collider = key;
                intersection = currentIntersection;
                foundIntersection = true;
            }
        }

        return foundIntersection;
    }
    public bool Collide(Vector3 thisTickOrigin, Vector3 previousTickOrigin, float radius, out string collider, out float distance, out Vector3 intersection)
    {
        distance = float.MaxValue;
        bool foundIntersection = false;
        collider = "";

        if (!HasOBBCollider)
        {
            CuboidAABBCollider AABBCollider = new(entity);
            return AABBCollider.Collide(thisTickOrigin, previousTickOrigin, radius, out intersection);
        }

        if (!BoundingBox.Collide(thisTickOrigin, previousTickOrigin, radius, out intersection))
        {
            return false;
        }

        foreach ((string key, ShapeElementCollider shapeElementCollider) in Colliders)
        {
            if (shapeElementCollider.Collide(thisTickOrigin, previousTickOrigin, radius, out float currentDistance, out Vector3 currentIntersection) && currentDistance < distance)
            {
                distance = currentDistance;
                collider = key;
                intersection = currentIntersection;
                foundIntersection = true;
            }
        }

        return foundIntersection;
    }
    public bool Collide(Vector3 thisTickOrigin, Vector3 previousTickOrigin, float radius, float penetrationDistance, out List<(string, float, Vector3)> intersections)
    {
        intersections = new();
        bool foundIntersection = false;



        if (!HasOBBCollider)
        {
            CuboidAABBCollider AABBCollider = new(entity);
            return AABBCollider.Collide(thisTickOrigin, previousTickOrigin, radius, out Vector3 intersection);
        }

        if (!BoundingBox.Collide(thisTickOrigin, previousTickOrigin, radius, out _))
        {
            return false;
        }

        Vector3 firstIntersection = previousTickOrigin;
        float lowestParameter = 1;

        foreach ((string key, ShapeElementCollider shapeElementCollider) in Colliders)
        {
            if (shapeElementCollider.Collide(thisTickOrigin, previousTickOrigin, radius, out _, out _, out Vector3 segmentClosestPoint))
            {
                Vector3 segmentPoint = segmentClosestPoint - previousTickOrigin;
                float parameter = segmentPoint.Length() / (thisTickOrigin - previousTickOrigin).Length();

                if (lowestParameter >= parameter)
                {
                    firstIntersection = segmentClosestPoint;
                    lowestParameter = parameter;
                }

                foundIntersection = true;
            }
        }

        Vector3 thisTickOriginAdjustedForPenetration = firstIntersection + Vector3.Normalize(thisTickOrigin - previousTickOrigin) * penetrationDistance;

        if (foundIntersection)
        {
            foundIntersection = false;
            foreach ((string key, ShapeElementCollider shapeElementCollider) in Colliders)
            {
                if (shapeElementCollider.Collide(thisTickOriginAdjustedForPenetration, previousTickOrigin, radius, out float currentDistance, out Vector3 currentIntersection, out Vector3 segmentClosestPoint))
                {
                    Vector3 segmentPoint = segmentClosestPoint - previousTickOrigin;
                    float parameter = (segmentPoint.Length() + currentDistance) / (thisTickOrigin - previousTickOrigin).Length();

                    intersections.Add((key, parameter, currentIntersection));
                    foundIntersection = true;
                }
            }
        }

        intersections.Sort((first, second) => (int)(first.Item2 - second.Item2));

        return foundIntersection;
    }

    private readonly Dictionary<ColliderTypes, int> _colliderColors = new()
    {
        { ColliderTypes.Torso, ColorUtil.WhiteArgb },
        { ColliderTypes.Head, ColorUtil.ColorFromRgba(255, 0, 0, 255 ) }, // Red
        { ColliderTypes.Arm, ColorUtil.ColorFromRgba(0, 255, 0, 255 ) }, // Green
        { ColliderTypes.Leg, ColorUtil.ColorFromRgba(0, 0, 255, 255 ) }, // Blue
        { ColliderTypes.Critical, ColorUtil.ColorFromRgba(255, 255, 0, 255 ) }, // Yellow
        { ColliderTypes.Resistant, ColorUtil.ColorFromRgba(255, 0, 255, 255 ) } // Magenta
    };

    private void RecalculateColliders(ClientAnimator animator, ICoreClientAPI clientApi)
    {
        foreach ((_, ShapeElementCollider collider) in Colliders)
        {
            collider.Transform(animator.TransformationMatrices, clientApi);
        }
        CalculateBoundingBox();
    }
    private void CalculateBoundingBox()
    {
        Vector3 min = new(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new(float.MinValue, float.MinValue, float.MinValue);

        foreach (ShapeElementCollider collider in Colliders.Values)
        {
            for (int vertex = 0; vertex < ShapeElementCollider.VertexCount; vertex++)
            {
                Vector4 inworldVertex = collider.InworldVertices[vertex];
                min.X = Math.Min(min.X, inworldVertex.X);
                min.Y = Math.Min(min.Y, inworldVertex.Y);
                min.Z = Math.Min(min.Z, inworldVertex.Z);
                max.X = Math.Max(max.X, inworldVertex.X);
                max.Y = Math.Max(max.Y, inworldVertex.Y);
                max.Z = Math.Max(max.Z, inworldVertex.Z);
            }
        }

        BoundingBox = new CuboidAABBCollider(min, max);
    }
}