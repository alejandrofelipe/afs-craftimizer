using Artificer.Plugin;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Artificer.Utils;

public interface ITextureIcon
{
    ISharedImmediateTexture Source { get; }

    Vector2? Dimensions { get; }

    float? AspectRatio => Dimensions is { } d ? d.X / d.Y : null;

    ImTextureID Handle { get; }
}

public interface ILoadedTextureIcon : ITextureIcon, IDisposable { }

public sealed class IconManager : IDisposable
{
    private sealed class LoadedIcon : ILoadedTextureIcon
    {
        public ISharedImmediateTexture Source { get; }

        public Vector2? Dimensions => GetWrap()?.Size;

        public ImTextureID Handle => GetWrapOrEmpty().Handle;

        private Task<IDalamudTextureWrap> TextureWrapTask { get; }
        private CancellationTokenSource DisposeToken { get; }

        public LoadedIcon(ISharedImmediateTexture source)
        {
            Source = source;
            DisposeToken = new();
            TextureWrapTask = source.RentAsync(DisposeToken.Token);
        }

        public IDalamudTextureWrap? GetWrap()
        {
            if (TextureWrapTask.IsCompletedSuccessfully)
                return TextureWrapTask.Result;
            return null;
        }

        public IDalamudTextureWrap GetWrapOrEmpty() => GetWrap() ?? Service.DalamudAssetManager.Empty4X4;

        public void Dispose()
        {
            DisposeToken.Cancel();
            TextureWrapTask.ToContentDisposedTask(true).Wait();
        }
    }

    /// <summary>
    /// Cached icon wrapper that keeps texture loaded in memory.
    /// Automatically unloaded by IMemoryCache after expiration.
    /// </summary>
    private sealed class CachedIcon(ISharedImmediateTexture source) : ITextureIcon
    {
        private LoadedIcon Base { get; } = new(source);

        public ISharedImmediateTexture Source => Base.Source;

        public Vector2? Dimensions => Base.Dimensions;

        public ImTextureID Handle => Base.Handle;

        public void Release()
        {
            Base.Dispose();
        }
    }

    private readonly IMemoryCache _cache;
    private readonly Configuration _config;

    public IconManager(Configuration config)
    {
        _config = config;
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = _config.IconCacheSizeLimit > 0 ? _config.IconCacheSizeLimit : null,
            CompactionPercentage = 0.25, // Remove 25% when limit reached
            ExpirationScanFrequency = TimeSpan.FromMinutes(1)
        });
    }

    private MemoryCacheEntryOptions CreateIconOptions()
    {
        if (!_config.EnableIconCacheEviction)
        {
            // No eviction → icons remain indefinitely
            return new MemoryCacheEntryOptions()
                .SetSize(1);
        }

        return new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetSlidingExpiration(TimeSpan.FromMinutes(_config.IconCacheSlidingExpirationMinutes))
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(_config.IconCacheAbsoluteExpirationMinutes))
            .RegisterPostEvictionCallback(OnIconEvicted);
    }

    private MemoryCacheEntryOptions CreateAssemblyTextureOptions()
    {
        if (!_config.EnableIconCacheEviction)
        {
            return new MemoryCacheEntryOptions()
                .SetSize(1);
        }

        // Assembly textures rarely change, longer TTL (6x sliding, 4x absolute)
        return new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetSlidingExpiration(TimeSpan.FromMinutes(_config.IconCacheSlidingExpirationMinutes * 6))
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(_config.IconCacheAbsoluteExpirationMinutes * 4))
            .RegisterPostEvictionCallback(OnIconEvicted);
    }

    private void OnIconEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        if (value is CachedIcon icon)
        {
            Log.Debug($"[IconCache] Evicted {key}: {reason}");
            icon.Release();
        }
    }

    private static ISharedImmediateTexture GetIconInternal(uint id, bool isHq = false) =>
        Service.TextureProvider.GetFromGameIcon(new GameIconLookup(id, itemHq: isHq));

    private static ISharedImmediateTexture GetAssemblyTextureInternal(string filename) =>
        Service.TextureProvider.GetFromManifestResource(Assembly.GetExecutingAssembly(), $"Artificer.{filename}");

    public static ILoadedTextureIcon GetIcon(uint id, bool isHq = false) =>
        new LoadedIcon(GetIconInternal(id, isHq));

    public static ILoadedTextureIcon GetAssemblyTexture(string filename) =>
        new LoadedIcon(GetAssemblyTextureInternal(filename));

    public ITextureIcon GetIconCached(uint id, bool isHq = false)
    {
        var key = $"icon:{id}:{isHq}";
        return _cache.GetOrCreate(key, entry =>
        {
            entry.SetOptions(CreateIconOptions());
            Log.Debug($"[IconCache] Miss: {key}, loading...");
            return new CachedIcon(GetIconInternal(id, isHq));
        })!;
    }

    public ITextureIcon GetAssemblyTextureCached(string filename)
    {
        var key = $"asm:{filename}";
        return _cache.GetOrCreate(key, entry =>
        {
            entry.SetOptions(CreateAssemblyTextureOptions());
            Log.Debug($"[IconCache] Miss: {key}, loading...");
            return new CachedIcon(GetAssemblyTextureInternal(filename));
        })!;
    }

    public void Dispose()
    {
        (_cache as IDisposable)?.Dispose();
    }
}
