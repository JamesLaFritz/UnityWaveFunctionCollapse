// Copyright (C) 2016 Maxim Gumin, The MIT License (MIT)

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityWaveFunctionCollapse
{
    public class OverlappingModel : Model
    {
        private readonly List<byte[]> _patterns;
        private readonly List<int> _colors;

        public OverlappingModel(string name, int width, int height, bool periodic, Heuristic heuristic, int patternSize,
            bool periodicInput, int symmetry, bool ground) : base(width, height, periodic, heuristic, patternSize)
        {
            var (bitmap, sx, sy) = Helper.BitmapHelper.LoadBitmap($"samples/{name}.png");        
            var sample = new byte[bitmap.Length];
            _colors = new List<int>();
            for (var i = 0; i < sample.Length; i++)
            {
                var color = bitmap[i];
                var k = 0;
                for (; k < _colors.Count; k++) if (_colors[k] == color) break;
                if (k == _colors.Count) _colors.Add(color);
                sample[i] = (byte)k;
            }

            static byte[] Pattern(Func<int, int, byte> f, int n)
            {
                var result = new byte[n * n];
                for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                    result[x + y * n] = f(x, y);
                return result;
            }
            
            static byte[] Rotate(IReadOnlyList<byte> p, int n) => Pattern((x, y) => p[n - 1 - y + x * n], n);
            static byte[] Reflect(IReadOnlyList<byte> p, int n) => Pattern((x, y) => p[n - 1 - x + y * n], n);

            static long Hash(IReadOnlyList<byte> p, int c)
            {
                long result = 0, power = 1;
                for (var i = 0; i < p.Count; i++)
                {
                    result += p[p.Count - 1 - i] * power;
                    power *= c;
                }
                return result;
            }

            _patterns = new List<byte[]>();
            Dictionary<long, int> patternIndices = new();
            List<double> weightList = new();

            var count = _colors.Count;
            var xMax = periodicInput ? sx : sx - patternSize + 1;
            var yMax = periodicInput ? sy : sy - patternSize + 1;
            for (var y = 0; y < yMax; y++)
            {
                for (var x = 0; x < xMax; x++)
                {
                    var ps = new byte[8][];

                    // ReSharper disable AccessToModifiedClosure
                    ps[0] = Pattern((dx, dy) =>
                    {
                        var sampleIndex = (x + dx) % sx + (y + dy) % sy * sx;
                        if (sampleIndex >= 0 && sampleIndex < sample.Length) return sample[sampleIndex];
                        Debug.LogWarning($"WARN: computed sample index {sampleIndex} is not in range {sample.Length}");
                        sampleIndex = sampleIndex < 0 ? 0 : sample.Length - 1;
                        return sample[sampleIndex];
                    }, patternSize);
                    // ReSharper restore AccessToModifiedClosure
                    ps[1] = Reflect(ps[0], patternSize);
                    ps[2] = Rotate(ps[0], patternSize);
                    ps[3] = Reflect(ps[2], patternSize);
                    ps[4] = Rotate(ps[2], patternSize);
                    ps[5] = Reflect(ps[4], patternSize);
                    ps[6] = Rotate(ps[4], patternSize);
                    ps[7] = Reflect(ps[6], patternSize);

                    for (var k = 0; k < symmetry; k++)
                    {
                        var p = ps[k];
                        var h = Hash(p, count);
                        if (patternIndices.TryGetValue(h, out var index)) weightList[index] += 1;
                        else
                        {
                            patternIndices.Add(h, weightList.Count);
                            weightList.Add(1.0);
                            _patterns.Add(p);
                        }
                    }
                }
            }

            weights = weightList.ToArray();
            T = weights.Length;
            this.ground = ground;

            static bool Agrees(IReadOnlyList<byte> p1, IReadOnlyList<byte> p2, int dx, int dy, int n)
            {
                int xMin = dx < 0 ? 0 : dx,
                    xMax = dx < 0 ? dx + n : n,
                    yMin = dy < 0 ? 0 : dy,
                    yMax = dy < 0 ? dy + n : n;
                for (var y = yMin; y < yMax; y++)
                for (var x = xMin; x < xMax; x++)
                    if (p1[x + n * y] != p2[x - dx + n * (y - dy)])
                        return false;
                return true;
            }

            propagator = new int[4][][];
            for (var d = 0; d < 4; d++)
            {
                propagator[d] = new int[T][];
                for (var t = 0; t < T; t++)
                {
                    List<int> list = new();
                    for (var t2 = 0; t2 < T; t2++)
                        if (Agrees(_patterns[t], _patterns[t2], DX[d], DY[d], patternSize))
                            list.Add(t2);
                    propagator[d][t] = new int[list.Count];
                    for (var c = 0; c < list.Count; c++) propagator[d][t][c] = list[c];
                }
            }
        }

        public override void Save(string filename)
        {
            var bitmap = new int[width * height];
            if (observed[0] >= 0)
            {
                for (var y = 0; y < height; y++)
                {
                    var dy = y < height - patternSize + 1 ? 0 : patternSize - 1;
                    for (var x = 0; x < width; x++)
                    {
                        var dx = x < width - patternSize + 1 ? 0 : patternSize - 1;
                        var oi = x - dx + (y - dy) * width;
                        var px = observed[oi];
                        var py = dx + dy * patternSize;
                        if (px < 0 )
                        {
                            Debug.LogWarning($"observed[{oi}] = {px}");
                            break;
                        }
                        bitmap[x + y * width] = _colors[_patterns[px][py]];
                    }
                }
            }
            else
            {
                for (var i = 0; i < wave.Length; i++)
                {
                    int contributors = 0, r = 0, g = 0, b = 0;
                    int x = i % width, y = i / width;
                    for (var dy = 0; dy < patternSize; dy++)
                    {
                        for (var dx = 0; dx < patternSize; dx++)
                        {
                            var sx = x - dx;
                            if (sx < 0) sx += width;

                            var sy = y - dy;
                            if (sy < 0) sy += height;

                            var s = sx + sy * width;
                            if (!periodic && (sx + patternSize > width || sy + patternSize > height || sx < 0 ||
                                              sy < 0)) continue;
                            for (var t = 0; t < T; t++)
                                if (wave[s][t])
                                {
                                    contributors++;
                                    int argb = _colors[_patterns[t][dx + dy * patternSize]];
                                    r += (argb & 0xff0000) >> 16;
                                    g += (argb & 0xff00) >> 8;
                                    b += argb & 0xff;
                                }
                        }
                    }

                    var color = unchecked((int)0xff000000 | (r << 16) | (g << 8) | b);
                    if (contributors > 0)
                        color = unchecked((int)0xff000000 | ((r / contributors) << 16) | ((g / contributors) << 8) | b / contributors);
                    bitmap[i] = color;
                }
            }
            Helper.BitmapHelper.SaveBitmap(bitmap, width, height, filename);
        }
    }
}
