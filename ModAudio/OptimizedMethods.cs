﻿using Newtonsoft.Json.Linq;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Marioalexsan.ModAudio;

// TODO: Investigate Burst with hand-written intrinsics
internal static class OptimizedMethods
{
    // Note: Burst's automated vectorization couldn't optimize this method any better than JIT
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyFloatArray(float[] data, float factor)
    {
        unsafe
        {
            fixed (float* start = data)
            {
                float* current = start;
                float* end = start + data.Length;

                while (current != end)
                {
                    *current = *current * factor;
                    current++;
                }
            }
        }
    }

    public delegate void CachedForeachAction<T>(in T value);

    public static void CachedForeach<T>(ICollection<T> enumerable, CachedForeachAction<T> action)
    {
        var cache = ArrayPool<T>.Shared.Rent(enumerable.Count);
        var cacheSize = 0;

        foreach (var value in enumerable)
        {
            cache[cacheSize++] = value;
        }

        for (int i = 0; i < cacheSize; i++)
        {
            action(cache[i]);
        }

        ArrayPool<T>.Shared.Return(cache);
    }

    public delegate void CachedForeachAction<T, V>(in T value, in V context);

    public static void CachedForeach<T, V>(ICollection<T> enumerable, in V context, CachedForeachAction<T, V> action)
    {
        var cache = ArrayPool<T>.Shared.Rent(enumerable.Count);
        var cacheSize = 0;

        foreach (var value in enumerable)
        {
            cache[cacheSize++] = value;
        }

        for (int i = 0; i < cacheSize; i++)
        {
            action(cache[i], in context);
        }

        ArrayPool<T>.Shared.Return(cache);
    }
}
