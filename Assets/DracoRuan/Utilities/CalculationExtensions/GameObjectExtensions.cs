using UnityEngine;

namespace DracoRuan.Utilities.CalculationExtensions
{
    public static class GameObjectExtensions
    {
        public static T GetComponentThatInheritFrom<T, TBase>(this GameObject gameObject) where T : Component, TBase
            => GetComponentOrDefault<T>(gameObject);
        
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : Component =>
            gameObject.TryGetComponent(out T component) ? component : gameObject.AddComponent<T>();
        
        public static T GetComponentOrDefault<T>(this GameObject gameObject) where T : Component =>
            gameObject.TryGetComponent(out T component) ? component : null;
        
        public static bool HasLayerMask(this GameObject gameObject, LayerMask layerMask) =>
            (layerMask.value & (1 << gameObject.layer)) > 0;

        public static bool HasLayerMask(this Collider collider, LayerMask layerMask) =>
            collider && collider.gameObject.HasLayerMask(layerMask);

        public static bool HasLayerMask(this Collision collision, LayerMask layerMask) =>
            collision != null && collision.gameObject.HasLayerMask(layerMask);

        public static bool HasLayerMask(this Collider2D collider, LayerMask layerMask) =>
            collider && collider.gameObject.HasLayerMask(layerMask);

        public static bool HasLayerMask(this Collision2D collision, LayerMask layerMask) =>
            collision != null && collision.gameObject.HasLayerMask(layerMask);
    }
}
