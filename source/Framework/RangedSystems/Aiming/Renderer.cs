﻿using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace CombatOverhaul.RangedSystems.Aiming;

public enum WeaponAimingState
{
    None,
    Blocked,
    PartCharge,
    FullCharge
}

public sealed class ReticleRenderer : IRenderer
{
    public WeaponAimingState AimingState { get; set; } = WeaponAimingState.None;
    public bool ReticleScaling { get; set; } = false;
    public bool ThrowCircle { get; set; } = false;
    public bool DebugRender { get; set; } = false;
    public Vector2 AimingPoint { get; set; }

    public double RenderOrder => 0.98;
    public int RenderRange => 9999;


    public ReticleRenderer(ICoreClientAPI api)
    {
        _clientApi = api;

        LoadedTexture blockedReticle = new(api);
        LoadedTexture partChargeReticle = new(api);
        LoadedTexture fullChargeReticle = new(api);

        _aimTextureThrowCircle = new LoadedTexture(api);

        api.Render.GetOrLoadTexture(new AssetLocation("combatoverhaul", "gui/aiming/default-blocked.png"), ref blockedReticle);
        api.Render.GetOrLoadTexture(new AssetLocation("combatoverhaul", "gui/aiming/default-part.png"), ref partChargeReticle);
        api.Render.GetOrLoadTexture(new AssetLocation("combatoverhaul", "gui/aiming/default-full.png"), ref fullChargeReticle);
        api.Render.GetOrLoadTexture(new AssetLocation("combatoverhaul", "gui/aiming/throw-circle.png"), ref _aimTextureThrowCircle);

        _defaultTextures[WeaponAimingState.Blocked] = blockedReticle;
        _defaultTextures[WeaponAimingState.PartCharge] = partChargeReticle;
        _defaultTextures[WeaponAimingState.FullCharge] = fullChargeReticle;

        _currentTextures[WeaponAimingState.Blocked] = blockedReticle;
        _currentTextures[WeaponAimingState.PartCharge] = partChargeReticle;
        _currentTextures[WeaponAimingState.FullCharge] = fullChargeReticle;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (AimingState == WeaponAimingState.None) return;

        LoadedTexture texture = _currentTextures[AimingState];

        float reticleScale = ReticleScaling ? RuntimeEnv.GUIScale : 1f;

        _clientApi.Render.Render2DTexture(texture.TextureId,
            (_clientApi.Render.FrameWidth / 2) - (texture.Width * reticleScale / 2) + AimingPoint.X,
            (_clientApi.Render.FrameHeight / 2) - (texture.Height * reticleScale / 2) + AimingPoint.Y,
            texture.Width * reticleScale, texture.Height * reticleScale, 10000f);

        if (ThrowCircle)
        {
            _clientApi.Render.Render2DTexture(_aimTextureThrowCircle.TextureId,
                    (_clientApi.Render.FrameWidth / 2) - (_aimTextureThrowCircle.Width / 2),
                    (_clientApi.Render.FrameHeight / 2) - (_aimTextureThrowCircle.Height / 2),
                    _aimTextureThrowCircle.Width, _aimTextureThrowCircle.Height, 10001f);
        }

        if (DebugRender)
        {
            LoadedTexture debugReticle = _currentTextures[WeaponAimingState.FullCharge];

            _clientApi.Render.Render2DTexture(debugReticle.TextureId,
                    (_clientApi.Render.FrameWidth / 2) - (debugReticle.Width / 2) + AimingPoint.X,
                    (_clientApi.Render.FrameHeight / 2) - (debugReticle.Height / 2) + AimingPoint.Y,
                    debugReticle.Width, debugReticle.Height, 10000f);
        }
    }

    public void SetReticleTextures(LoadedTexture partCharge, LoadedTexture fullCharge, LoadedTexture blocked)
    {
        _currentTextures[WeaponAimingState.Blocked] = blocked.TextureId > 0 ? blocked : _defaultTextures[WeaponAimingState.Blocked];
        _currentTextures[WeaponAimingState.PartCharge] = partCharge.TextureId > 0 ? partCharge : _defaultTextures[WeaponAimingState.PartCharge];
        _currentTextures[WeaponAimingState.FullCharge] = fullCharge.TextureId > 0 ? fullCharge : _defaultTextures[WeaponAimingState.FullCharge];
    }

    public void Dispose()
    {
        foreach (LoadedTexture texture in _defaultTextures.Values)
        {
            texture.Dispose();
        }
    }

    private readonly Dictionary<WeaponAimingState, LoadedTexture> _defaultTextures = new();
    private readonly Dictionary<WeaponAimingState, LoadedTexture> _currentTextures = new();
    private readonly LoadedTexture _aimTextureThrowCircle;
    private readonly ICoreClientAPI _clientApi;
}