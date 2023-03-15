// Copyright (C) 2016 Maxim Gumin, The MIT License (MIT)

using System;
using Random = UnityEngine.Random;

namespace UnityWaveFunctionCollapse
{
    public abstract class Model
    {
        protected static readonly int[] DX = { -1, 0, 1, 0 };
        protected static readonly int[] DY = { 0, 1, 0, -1 };
        private static readonly int[] Opposite = { 2, 3, 0, 1 };

        protected bool[][] wave;

        protected int[][][] propagator;
        private int[][][] _compatible;

        protected int[] observed, sumsOfOnes;

        private (int, int)[] _stack;

        protected double[] weights, sumsOfWeights;
        private double[] _weightLogWeights, _sumsOfWeightLogWeights, _entropies, _distribution;

        private double _sumOfWeights, _sumOfWeightLogWeights, _startingEntropy;

        protected readonly int width, height, patternSize;
        protected int T;
        private int _stackSize, _observedSoFar;

        protected readonly bool periodic;
        protected bool ground;

        public enum Heuristic { Entropy, MRV, Scanline };

        private readonly Heuristic _heuristic;

        protected Model(int width, int height, bool periodic, Heuristic heuristic, int patternSize)
        {
            this.width = width;
            this.height = height;
            this.patternSize = patternSize;
            this.periodic = periodic;
            _heuristic = heuristic;
        }

        public abstract void Save(string filename);

        public bool Run(int seed, int limit)
        {
            if (wave == null) Init();

            Clear();
            Random.InitState(seed);

            for (var l = 0; l < limit || limit < 0; l++)
            {
                var node = NextUnobservedNode();
                if (node >= 0)
                {
                    Observe(node);
                    var success = Propagate();
                    if (!success) return false;
                }
                else
                {
                    for (var i = 0; i < wave!.Length; i++)
                    for (var t = 0; t < T; t++)
                        if (wave[i][t])
                        {
                            observed[i] = t;
                            break;
                        }
                    return true;
                }
            }

            return true;
        }

        private void Init()
        {
            wave = new bool[width * height][];
            _compatible = new int[wave.Length][][];
            for (var i = 0; i < wave.Length; i++)
            {
                wave[i] = new bool[T];
                _compatible[i] = new int[T][];
                for (var t = 0; t < T; t++) _compatible[i][t] = new int[4];
            }
            _distribution = new double[T];
            observed = new int[width * height];

            _weightLogWeights = new double[T];
            _sumOfWeights = 0;
            _sumOfWeightLogWeights = 0;

            for (var t = 0; t < T; t++)
            {
                _weightLogWeights[t] = weights[t] * Math.Log(weights[t]);
                _sumOfWeights += weights[t];
                _sumOfWeightLogWeights += _weightLogWeights[t];
            }

            _startingEntropy = Math.Log(_sumOfWeights) - _sumOfWeightLogWeights / _sumOfWeights;

            sumsOfOnes = new int[width * height];
            sumsOfWeights = new double[width * height];
            _sumsOfWeightLogWeights = new double[width * height];
            _entropies = new double[width * height];

            _stack = new (int, int)[wave.Length * T];
            _stackSize = 0;
        }

        private int NextUnobservedNode()
        {
            if (_heuristic == Heuristic.Scanline)
            {
                for (var i = _observedSoFar; i < wave.Length; i++)
                {
                    if (!periodic && (i % width + patternSize > width || i / width + patternSize > height)) continue;
                    if (sumsOfOnes[i] > 1)
                    {
                        _observedSoFar = i + 1;
                        return i;
                    }
                }
                return -1;
            }

            var min = 1E+4;
            var argMin = -1;
            for (var i = 0; i < wave.Length; i++)
            {
                if (!periodic && (i % width + patternSize > width || i / width + patternSize > height)) continue;
                var remainingValues = sumsOfOnes[i];
                var entropy = _heuristic == Heuristic.Entropy ? _entropies[i] : remainingValues;
                if (remainingValues <= 1 || !(entropy <= min)) continue;
                var noise = 1E-6 * Random.value;
                if (!(entropy + noise < min)) continue;
                min = entropy + noise;
                argMin = i;
            }
            return argMin;
        }

        private void Observe(int node)
        {
            var w = wave[node];
            for (var t = 0; t < T; t++) _distribution[t] = w[t] ? weights[t] : 0.0;
            var r = _distribution.Random(Random.value);
            for (var t = 0; t < T; t++) if (w[t] != (t == r)) Ban(node, t);
        }

        private bool Propagate()
        {
            while (_stackSize > 0)
            {
                var (i1, t1) = _stack[_stackSize - 1];
                _stackSize--;

                var x1 = i1 % width;
                var y1 = i1 / width;

                for (var d = 0; d < 4; d++)
                {
                    var x2 = x1 + DX[d];
                    var y2 = y1 + DY[d];
                    if (!periodic && (x2 < 0 || y2 < 0 || x2 + patternSize > width || y2 + patternSize > height))
                        continue;

                    if (x2 < 0) x2 += width;
                    else if (x2 >= width) x2 -= width;
                    if (y2 < 0) y2 += height;
                    else if (y2 >= height) y2 -= height;

                    var i2 = x2 + y2 * width;
                    var p = propagator[d][t1];
                    var compat = _compatible[i2];

                    foreach (var t2 in p)
                    {
                        var comp = compat[t2];

                        comp[d]--;
                        if (comp[d] == 0) Ban(i2, t2);
                    }
                }
            }

            return sumsOfOnes[0] > 0;
        }

        private void Ban(int i, int t)
        {
            wave[i][t] = false;

            var comp = _compatible[i][t];
            for (var d = 0; d < 4; d++) comp[d] = 0;
            _stack[_stackSize] = (i, t);
            _stackSize++;

            sumsOfOnes[i] -= 1;
            sumsOfWeights[i] -= weights[t];
            _sumsOfWeightLogWeights[i] -= _weightLogWeights[t];

            var sum = sumsOfWeights[i];
            _entropies[i] = Math.Log(sum) - _sumsOfWeightLogWeights[i] / sum;
        }

        private void Clear()
        {
            for (var i = 0; i < wave.Length; i++)
            {
                for (var t = 0; t < T; t++)
                {
                    wave[i][t] = true;
                    for (var d = 0; d < 4; d++) _compatible[i][t][d] = propagator[Opposite[d]][t].Length;
                }

                sumsOfOnes[i] = weights.Length;
                sumsOfWeights[i] = _sumOfWeights;
                _sumsOfWeightLogWeights[i] = _sumOfWeightLogWeights;
                _entropies[i] = _startingEntropy;
                observed[i] = -1;
            }
            _observedSoFar = 0;

            if (!ground) return;
            for (var x = 0; x < width; x++)
            {
                for (var t = 0; t < T - 1; t++) Ban(x + (height - 1) * width, t);
                for (var y = 0; y < height - 1; y++) Ban(x + y * width, T - 1);
            }
            Propagate();
        }
    }
}
