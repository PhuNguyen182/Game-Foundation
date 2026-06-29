using System;
using UnityEngine;

namespace DracoRuan.Utilities.CalculationExtensions
{
    public static class ColorExtension
    {
    /// <summary>
        /// Normalize the RGB values of Color from range of 0 - 255 into range of 0 - 1
        /// </summary>
        public static Color Normalize(this Color color)
        {
            Color result = new Color(color.r / 255, color.g / 255, color.b / 255, color.a);
            return result;
        }

        /// <summary>
        /// Lighten a color if <i>factor</i> is greater then 1, darken if <i>factor</i> is smaller than 1.
        /// </summary>
        public static Color Adjust(this Color color, float factor)
        {
            Color result = new Color(Mathf.Clamp01(color.r * factor), Mathf.Clamp01(color.g * factor),
                Mathf.Clamp01(color.b * factor), color.a);
            return result;
        }

        /// <summary>
        /// Convert from RGBA Color into HEX Color.
        /// </summary>
        public static string ToHex(this Color color)
        {
            string result = $"#{(int)(color.r * 255):X2}{(int)(color.g * 255):X2}{(int)(color.b * 255):X2}"; 
            return result;
        }

        /// <summary>
        /// Convert from HEX Color into RGBA Color.
        /// </summary>
        public static Color ToColor(this string hex)
        {
            Color color = Color.white;
            string newHex = hex;

            if (hex.Contains("#")) 
                newHex = hex.Replace("#", "");

            try
            {
                if (newHex.Length != 6 && newHex.Length != 8)
                {
                    Debug.Log("Format Exception: Invalid HEX Color Format!");
                    return color;
                }

                switch (newHex.Length)
                {
                    case 6:
                        color.r = newHex.Substring(0, 2).HexToInt() / (float)255;
                        color.g = newHex.Substring(2, 2).HexToInt() / (float)255;
                        color.b = newHex.Substring(4, 2).HexToInt() / (float)255;
                        break;
                    case 8:
                        color.r = newHex.Substring(0, 2).HexToInt() / (float)255;
                        color.g = newHex.Substring(2, 2).HexToInt() / (float)255;
                        color.b = newHex.Substring(4, 2).HexToInt() / (float)255;
                        color.a = newHex.Substring(6, 2).HexToInt() / (float)255;
                        break;
                }
            }
            catch (FormatException)
            {
                Debug.Log("Format Exception: Invalid HEX Format.");
            }
            
            return color;
        }

        /// <summary>
        /// Get a random color.
        /// </summary>
        public static Color RandomColor(int min, int max, float alpha = 1)
        {
            int r = UnityEngine.Random.Range(min, max);
            int g = UnityEngine.Random.Range(min, max);
            int b = UnityEngine.Random.Range(min, max);
            Color result = new Color(r, g, b, alpha).Normalize();
            return result;
        }
    }
}
