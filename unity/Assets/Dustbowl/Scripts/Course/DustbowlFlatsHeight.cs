using System;
using UnityEngine;

namespace Dustbowl.Course
{
    public static class DustbowlFlatsHeight
    {
        private const int SeedKey = 0;
        private const double PlayRadius = 340d;

        public static float Sample(float x, float z)
        {
            double height = (Fbm(x * 0.0016d, z * 0.0016d, 5) - 0.5d) * 88d;
            height += (Fbm(x * 0.0062d + 91d, z * 0.0062d - 37d, 3) - 0.5d) * 26d;
            height += (Fbm(x * 0.017d + 43d, z * 0.017d - 12d, 2) - 0.5d) * 7d;
            height += (Fbm(x * 0.028d + 11d, z * 0.028d + 5d, 2) - 0.5d) * 1.4d;
            double radius = Math.Sqrt(x * x + z * z);
            double rim = SmoothStep(PlayRadius + 10d, PlayRadius + 190d, radius);
            height += rim * rim * 78d;
            return (float)height;
        }

        private static double Fbm(double x, double y, int octaves)
        {
            double sum = 0d;
            double amplitude = 0.5d;
            double normalizer = 0d;
            double frequencyX = x;
            double frequencyY = y;
            for (int octave = 0; octave < octaves; octave++)
            {
                sum += ValueNoise(frequencyX, frequencyY) * amplitude;
                normalizer += amplitude;
                amplitude *= 0.5d;
                frequencyX *= 2.03d;
                frequencyY *= 1.97d;
            }

            return sum / normalizer;
        }

        private static double ValueNoise(double x, double y)
        {
            int i = (int)Math.Floor(x);
            int j = (int)Math.Floor(y);
            double fractionalX = x - i;
            double fractionalY = y - j;
            double u = fractionalX * fractionalX * (3d - 2d * fractionalX);
            double v = fractionalY * fractionalY * (3d - 2d * fractionalY);
            double a = Hash2(i, j);
            double b = Hash2(i + 1, j);
            double c = Hash2(i, j + 1);
            double d = Hash2(i + 1, j + 1);
            return a * (1d - u) * (1d - v) + b * u * (1d - v) + c * (1d - u) * v + d * u * v;
        }

        private static double Hash2(int i, int j)
        {
            int value = unchecked(i * 374761393 + j * 668265263 + SeedKey);
            value = unchecked(value ^ (value >> 13));
            value = unchecked(value * 1274126177);
            uint unsigned = unchecked((uint)(value ^ (value >> 16)));
            return unsigned / 4294967295d;
        }

        private static double SmoothStep(double edge0, double edge1, double value)
        {
            double t = Math.Max(0d, Math.Min(1d, (value - edge0) / (edge1 - edge0)));
            return t * t * (3d - 2d * t);
        }
    }
}
