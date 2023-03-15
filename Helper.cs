// Copyright (C) 2016 Maxim Gumin, The MIT License (MIT)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace UnityWaveFunctionCollapse
{
    public static class Helper
    {
        public static int Random(this double[] weights, float r)
        {
            var sum = weights.Sum();
            var threshold = r * sum;

            double partialSum = 0;
            for (var i = 0; i < weights.Length; i++)
            {
                partialSum += weights[i];
                if (partialSum >= threshold) return i;
            }

            return 0;
        }
        
        public static int Random(this double[] weights, double r)
        {
            var sum = weights.Sum();
            var threshold = r * sum;

            double partialSum = 0;
            for (var i = 0; i < weights.Length; i++)
            {
                partialSum += weights[i];
                if (partialSum >= threshold) return i;
            }

            return 0;
        }

        public static long ToPower(this int a, int n)
        {
            long product = 1;
            for (var i = 0; i < n; i++) product *= a;
            return product;
        }

        public static T Get<T>(this XElement xElem, string attribute, T defaultT = default)
        {
            XAttribute a = xElem.Attribute(attribute);
            return a == null ? defaultT : (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(a.Value);
        }

        public static IEnumerable<XElement> Elements(this XElement xElement, params string[] names) =>
            xElement.Elements().Where(e => names.Any(n => n == e.Name));

        public static class BitmapHelper
        {
            public static (int[], int, int) LoadBitmap(string fileName)
            {
                Texture2D texture = new(1, 1);
                //texture.LoadRawTextureData
                texture.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,
                    $"UnityWaveFunctionCollapse/{fileName}")));
                texture.Apply();

                int width = texture.width, height = texture.height;
                return (texture.GetPixelData<int>(0).ToArray(), width, height);
            }

            public static void SaveBitmap(int[] data, int width, int height, string fileName)
            {
                if (data.Length == 0)
                {
                    Debug.LogError("[Bitmap] Empty int array passed to Copy(...).");
                    return;
                }

                try
                {
                    //Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
                    Texture2D texture = new(width, height, TextureFormat.ARGB32, false);
                    
                    //https://stackoverflow.com/questions/5896680/converting-an-int-to-byte-in-c-sharp
                    //var bitmapData = new byte[data.Length * sizeof(int)];
                    //Buffer.BlockCopy(data, 0, bitmapData, 0, bitmapData.Length);
                    //texture.LoadRawTextureData(bitmapData);
                    
                    texture.SetPixelData(data, 0);
                    texture.Apply();

                    Debug.Log($"{fileName}: ({width}, {height})");
                    File.WriteAllBytes(Path.Combine(Application.dataPath, $"UnityWaveFunctionCollapse/{fileName}"), texture.EncodeToPNG());
                }
                catch (Exception e)
                {
                    Debug.LogError($"ERROR: {e.Message}");
                }
            }
        }
    }
}