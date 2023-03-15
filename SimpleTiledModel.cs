// Copyright (C) 2016 Maxim Gumin, The MIT License (MIT)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace UnityWaveFunctionCollapse
{
    public class SimpleTiledModel : Model
    {
        private readonly List<int[]> _tiles;
        private readonly List<string> _tileNames;
        private readonly int _tilesize;
        private readonly bool _blackBackground;

        public SimpleTiledModel(string name, int width, int height, bool periodic, Heuristic heuristic,
            string subsetName, bool blackBackground) : base(width, height, periodic, heuristic, 1)
        {
            _blackBackground = blackBackground;
            XElement xRoot = XDocument.Load($"Assets/UnityWaveFunctionCollapse/tilesets/{name}.xml").Root;
            var unique = xRoot.Get("unique", false);

            List<string> subset = null;
            if (subsetName != null)
            {
                XElement xSubset = xRoot!.Element("subsets")!.Elements("subset").
                    FirstOrDefault(x => x.Get<string>("name") == subsetName);
                if (xSubset == null) Debug.LogError($"ERROR: subset {subsetName} is not found");
                else subset = xSubset.Elements("tile").Select(x => x.Get<string>("name")).ToList();
            }

            static int[] Tile(Func<int, int, int> f, int size)
            {
                var result = new int[size * size];
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++) result[x + y * size] = f(x, y);
                }
                return result;
            }

            static int[] Rotate(IReadOnlyList<int> array, int size) => Tile((x, y) => array[size - 1 - y + x * size], size);
            static int[] Reflect(IReadOnlyList<int> array, int size) => Tile((x, y) => array[size - 1 - x + y * size], size);

            _tiles = new List<int[]>();
            _tileNames = new List<string>();
            List<double> weightList = new();

            List<int[]> action = new();
            Dictionary<string, int> firstOccurrence = new();

            foreach (XElement xTile in xRoot!.Element("tiles")!.Elements("tile"))
            {
                var tileName = xTile.Get<string>("name");
                if (subset != null && !subset.Contains(tileName)) continue;

                Func<int, int> a, b;
                int cardinality;

                var sym = xTile.Get("symmetry", 'X');
                switch (sym)
                {
                    case 'L':
                        cardinality = 4;
                        a = i => (i + 1) % 4;
                        b = i => i % 2 == 0 ? i + 1 : i - 1;
                        break;
                    case 'T':
                        cardinality = 4;
                        a = i => (i + 1) % 4;
                        b = i => i % 2 == 0 ? i : 4 - i;
                        break;
                    case 'I':
                        cardinality = 2;
                        a = i => 1 - i;
                        b = i => i;
                        break;
                    case '\\':
                        cardinality = 2;
                        a = i => 1 - i;
                        b = i => 1 - i;
                        break;
                    case 'F':
                        cardinality = 8;
                        a = i => i < 4 ? (i + 1) % 4 : 4 + (i - 1) % 4;
                        b = i => i < 4 ? i + 4 : i - 4;
                        break;
                    default:
                        cardinality = 1;
                        a = i => i;
                        b = i => i;
                        break;
                }

                T = action.Count;
                firstOccurrence.Add(tileName, T);

                var map = new int[cardinality][];
                for (var t = 0; t < cardinality; t++)
                {
                    map[t] = new int[8];

                    map[t][0] = t;
                    map[t][1] = a(t);
                    map[t][2] = a(a(t));
                    map[t][3] = a(a(a(t)));
                    map[t][4] = b(t);
                    map[t][5] = b(a(t));
                    map[t][6] = b(a(a(t)));
                    map[t][7] = b(a(a(a(t))));

                    for (var s = 0; s < 8; s++) map[t][s] += T;

                    action.Add(map[t]);
                }

                if (unique)
                {
                    for (var t = 0; t < cardinality; t++)
                    {
                        int[] bitmap;
                        (bitmap, _tilesize, _tilesize) = Helper.BitmapHelper.LoadBitmap($"tilesets/{name}/{tileName} {t}.png");
                        _tiles.Add(bitmap);
                        _tileNames.Add($"{tileName} {t}");
                    }
                }
                else
                {
                    int[] bitmap;
                    (bitmap, _tilesize, _tilesize) = Helper.BitmapHelper.LoadBitmap($"tilesets/{name}/{tileName}.png");
                    _tiles.Add(bitmap);
                    _tileNames.Add($"{tileName} 0");

                    for (var t = 1; t < cardinality; t++)
                    {
                        switch (t)
                        {
                            case <= 3:
                                _tiles.Add(Rotate(_tiles[T + t - 1], _tilesize));
                                break;
                            case >= 4:
                                _tiles.Add(Reflect(_tiles[T + t - 4], _tilesize));
                                break;
                        }

                        _tileNames.Add($"{tileName} {t}");
                    }
                }

                for (var t = 0; t < cardinality; t++) weightList.Add(xTile.Get("weight", 1.0));
            }

            T = action.Count;
            weights = weightList.ToArray();

            propagator = new int[4][][];
            var densePropagator = new bool[4][][];
            for (var d = 0; d < 4; d++)
            {
                densePropagator[d] = new bool[T][];
                propagator[d] = new int[T][];
                for (var t = 0; t < T; t++) densePropagator[d][t] = new bool[T];
            }

            foreach (XElement xNeighbor in xRoot.Element("neighbors").Elements("neighbor"))
            {
                var left = xNeighbor.Get<string>("left").
                    Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var right = xNeighbor.Get<string>("right").
                    Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (subset != null && (!subset.Contains(left[0]) || !subset.Contains(right[0]))) continue;

                int l = action[firstOccurrence[left[0]]][left.Length == 1 ? 0 : int.Parse(left[1])], d = action[l][1];
                int r = action[firstOccurrence[right[0]]][right.Length == 1 ? 0 : int.Parse(right[1])], u = action[r][1];

                densePropagator[0][r][l] = true;
                densePropagator[0][action[r][6]][action[l][6]] = true;
                densePropagator[0][action[l][4]][action[r][4]] = true;
                densePropagator[0][action[l][2]][action[r][2]] = true;

                densePropagator[1][u][d] = true;
                densePropagator[1][action[d][6]][action[u][6]] = true;
                densePropagator[1][action[u][4]][action[d][4]] = true;
                densePropagator[1][action[d][2]][action[u][2]] = true;
            }

            for (var t2 = 0; t2 < T; t2++)
            {
                for (var t1 = 0; t1 < T; t1++)
                {
                    densePropagator[2][t2][t1] = densePropagator[0][t1][t2];
                    densePropagator[3][t2][t1] = densePropagator[1][t1][t2];
                }
            }

            List<int>[][] sparsePropagator = new List<int>[4][];
            for (var d = 0; d < 4; d++)
            {
                sparsePropagator[d] = new List<int>[T];
                for (var t = 0; t < T; t++) sparsePropagator[d][t] = new List<int>();
            }

            for (var d = 0; d < 4; d++) for (var t1 = 0; t1 < T; t1++)
            {
                List<int> sp = sparsePropagator[d][t1];
                var tp = densePropagator[d][t1];

                for (var t2 = 0; t2 < T; t2++) if (tp[t2]) sp.Add(t2);

                var st = sp.Count;
                if (st == 0) Debug.LogError($"ERROR: tile {_tileNames[t1]} has no neighbors in direction {d}");
                propagator[d][t1] = new int[st];
                for (var st1 = 0; st1 < st; st1++) propagator[d][t1][st1] = sp[st1];
            }
        }

        public override void Save(string filename)
        {
            var bitmapData = new int[width * height * _tilesize * _tilesize];
            if (observed[0] >= 0)
            {
                for (var x = 0; x < width; x++)
                {
                    for (var y = 0; y < height; y++)
                    {
                        var tile = _tiles[observed[x + y * width]];
                        for (var dy = 0; dy < _tilesize; dy++)
                        {
                            for (var dx = 0; dx < _tilesize; dx++)
                            {
                                var bi = x * _tilesize + dx + (y * _tilesize + dy) * width * _tilesize;
                                if (bi < 0 || bi >= bitmapData.Length || bitmapData.Length < 1)
                                {
                                    Debug.LogWarning($"WARN: computed bitmapData index {bi} is not in range {bitmapData.Length}");
                                    break;
                                }
                                var ti = dx + dy * _tilesize;
                                if (ti < 0 || ti >= tile.Length)
                                {
                                    Debug.LogWarning($"WARN: computed tile index {ti} is not in range {tile.Length}");
                                    break;
                                }
                                bitmapData[bi] = tile[ti];
                            }
                        }
                    }
                }
            }
            else
            {
                for (var i = 0; i < wave.Length; i++)
                {
                    int x = i % width, y = i / width;
                    if (_blackBackground && sumsOfOnes[i] == T)
                        for (var yt = 0; yt < _tilesize; yt++)
                        for (var xt = 0; xt < _tilesize; xt++)
                            bitmapData[x * _tilesize + xt + (y * _tilesize + yt) * width * _tilesize] = 255 << 24;
                    else
                    {
                        var w = wave[i];
                        var normalization = 1.0 / sumsOfWeights[i];
                        for (var yt = 0; yt < _tilesize; yt++)
                        {
                            for (var xt = 0; xt < _tilesize; xt++)
                            {
                                var idi = x * _tilesize + xt + (y * _tilesize + yt) * width * _tilesize;
                                double r = 0, g = 0, b = 0;
                                for (var t = 0; t < T; t++)
                                    if (w[t])
                                    {
                                        var argb = _tiles[t][xt + yt * _tilesize];
                                        r += ((argb & 0xff0000) >> 16) * weights[t] * normalization;
                                        g += ((argb & 0xff00) >> 8) * weights[t] * normalization;
                                        b += (argb & 0xff) * weights[t] * normalization;
                                    }

                                bitmapData[idi] = unchecked((int)0xff000000 | ((int)r << 16) | ((int)g << 8) | (int)b);
                            }
                        }
                    }
                }
            }
            Helper.BitmapHelper.SaveBitmap(bitmapData, width * _tilesize, height * _tilesize, filename);
        }

        public string TextOutput()
        {
            StringBuilder result = new();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var oi = x + y * width;
                    var ti = observed[oi];
                    if (ti >= _tileNames.Count || ti < 0)
                    {
                        result.Append($"null(ti:observed[{oi}]={ti}), ");
                        continue;
                    }
                    result.Append($"{_tileNames[ti]}, ");
                }
                result.Append(Environment.NewLine);
            }
            return result.ToString();
        }
    }
}
