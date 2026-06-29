using System;
using UnityEngine;

namespace DracoRuan.Utilities.CalculationExtensions
{
    public enum FlatPlane
    {
        XY,
        XZ,
        YZ,
    }
    
    public static class VectorExtension
    {
        public static Vector3 GetFlatVector(this Vector3 vector, FlatPlane flatPlane = FlatPlane.XZ)
        {
            Vector3 result = flatPlane switch
            {
                FlatPlane.XY => new Vector3(vector.x, vector.y, 0),
                FlatPlane.YZ => new Vector3(0, vector.y, vector.z),
                FlatPlane.XZ => new Vector3(vector.x, 0, vector.z),
                _ => Vector3.zero
            };
            
            return result;
        }

        /// <summary>
        /// Use this instead of adding normal Vector3 for performance. It's ~17 - 18 % faster. Use this version if you want to add optimizing for performance in an efficiency sense'.
        /// </summary>
        /// <param name="vector"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Vector3 FasterAdd(this Vector3 vector, Vector3 value)
        {
            vector.x += value.x;
            vector.y += value.y;
            vector.z += value.z;
            return vector;
        }

        /// <summary>
        /// Use this instead of adding normal Vector3 for performance. It's ~25 - 30 % faster. Use this version if you want to add separated values, and optimization is the top priority.
        /// </summary>
        /// <param name="vector"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public static Vector3 FasterAdd(this Vector3 vector, float x = 0, float y = 0, float z = 0)
        {
            vector.x += x;
            vector.y += y;
            vector.z += z;
            return vector;
        }
        
        /// <summary>
        /// Use this instead of adding normal Vector3 for performance. It's ~25 - 30 % faster. Use this version if you want to add separated values, and optimization is the top priority.
        /// </summary>
        /// <param name="vector"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public static Vector3Int FasterAdd(this Vector3Int vector, int x = 0, int y = 0, int z = 0)
        {
            vector.x += x;
            vector.y += y;
            vector.z += z;
            return vector;
        }

        /// <summary>
        /// Use this instead of adding normal Vector3 for performance. It's ~16 - 17 % faster. Use this version if you want to add optimizing for performance in an efficiency sense'.
        /// </summary>
        /// <param name="vector"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static Vector2 FasterAdd(this Vector2 vector, Vector2 value = default)
        {
            vector.x += value.x;
            vector.y += value.y;
            return vector;
        }
        
        /// <summary>
        /// Use this instead of adding normal Vector3 for performance. It's ~22 - 24 % faster. Use this version if you want to add separated values, and optimization is the top priority.
        /// </summary>
        /// <param name="vector"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static Vector2 FasterAdd(this Vector2 vector, float x = 0, float y = 0)
        {
            vector.x += x;
            vector.y += y;
            return vector;
        }
        
        public static Vector3 Quantize(this Vector3Int vector, Vector3 quantization = default)
        {
            Vector3 scaleVector = new()
            {
                x = Mathf.Floor(vector.x / quantization.x),
                y = Mathf.Floor(vector.y / quantization.y),
                z = Mathf.Floor(vector.z / quantization.z)
            };
            
            Vector3 quantizedVector = Vector3.Scale(quantization, scaleVector);
            return quantizedVector;
        }

        public static Vector3 Quantize(this Vector3Int vector, float xQuantization = 1, float yQuantization = 1,
            float zQuantization = 1)
        {
            Vector3 scaleVector = new()
            {
                x = Mathf.Floor(vector.x / xQuantization),
                y = Mathf.Floor(vector.y / yQuantization),
                z = Mathf.Floor(vector.z / zQuantization)
            };

            Vector3 quantizedVector = Vector3.Scale(new Vector3(xQuantization, yQuantization, zQuantization), scaleVector);
            return quantizedVector;
        }
        
        public static Vector3 ForwardToOrientation(this Vector3 vector, Quaternion rotation)
        {
            Vector3 forward = rotation * vector;
            return forward.normalized;
        }

        public static Vector3 ScaleWith(this Vector3 vector, Vector3 scale)
        {
            Vector3 scaledVector = Vector3.Scale(vector, scale);
            return scaledVector;
        }
    }
}
