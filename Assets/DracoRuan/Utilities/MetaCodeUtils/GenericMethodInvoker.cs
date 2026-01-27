using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace DracoRuan.Utilities.MetaCodeUtils
{
    /// <summary>
    /// Utility class để gọi generic methods với Type variables sử dụng Expression Trees.
    /// Provides near-native performance sau khi compile lần đầu.
    /// </summary>
    public static class GenericMethodInvoker
    {
        // Cache compiled delegates để tránh compile lại nhiều lần
        private static readonly Dictionary<CacheKey, Delegate> CompiledDelegatesCache = new();

        /// <summary>
        /// Gọi một generic method không có parameters và không có return value.
        /// </summary>
        /// <param name="instance">Instance object chứa method cần gọi</param>
        /// <param name="methodName">Tên của generic method</param>
        /// <param name="genericType">Type argument cho generic method</param>
        public static void InvokeGenericAction(object instance, string methodName, Type genericType)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            if (genericType == null) throw new ArgumentNullException(nameof(genericType));

            Type instanceType = instance.GetType();
            var cacheKey = new CacheKey(instanceType, methodName, genericType, Array.Empty<Type>());

            if (!CompiledDelegatesCache.TryGetValue(cacheKey, out Delegate cachedDelegate))
            {
                cachedDelegate = CompileGenericAction(instanceType, methodName, genericType);
                CompiledDelegatesCache[cacheKey] = cachedDelegate;
            }

            ((Action<object>)cachedDelegate).Invoke(instance);
        }

        /// <summary>
        /// Gọi một generic method với 1 parameter và không có return value.
        /// </summary>
        public static void InvokeGenericAction<TParam>(object instance, string methodName, Type genericType,
            TParam parameter)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            if (genericType == null) throw new ArgumentNullException(nameof(genericType));

            Type instanceType = instance.GetType();
            Type[] paramTypes = { typeof(TParam) };
            var cacheKey = new CacheKey(instanceType, methodName, genericType, paramTypes);

            if (!CompiledDelegatesCache.TryGetValue(cacheKey, out Delegate cachedDelegate))
            {
                cachedDelegate = CompileGenericAction<TParam>(instanceType, methodName, genericType);
                CompiledDelegatesCache[cacheKey] = cachedDelegate;
            }

            ((Action<object, TParam>)cachedDelegate).Invoke(instance, parameter);
        }

        /// <summary>
        /// Gọi một generic method với 2 parameters và không có return value.
        /// </summary>
        public static void InvokeGenericAction<TParam1, TParam2>(object instance, string methodName, Type genericType,
            TParam1 param1, TParam2 param2)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            if (genericType == null) throw new ArgumentNullException(nameof(genericType));

            Type instanceType = instance.GetType();
            Type[] paramTypes = { typeof(TParam1), typeof(TParam2) };
            var cacheKey = new CacheKey(instanceType, methodName, genericType, paramTypes);

            if (!CompiledDelegatesCache.TryGetValue(cacheKey, out Delegate cachedDelegate))
            {
                cachedDelegate = CompileGenericAction<TParam1, TParam2>(instanceType, methodName, genericType);
                CompiledDelegatesCache[cacheKey] = cachedDelegate;
            }

            ((Action<object, TParam1, TParam2>)cachedDelegate).Invoke(instance, param1, param2);
        }

        /// <summary>
        /// Gọi một generic method không có parameters và có return value.
        /// </summary>
        public static TResult InvokeGenericFunc<TResult>(object instance, string methodName, Type genericType)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            if (genericType == null) throw new ArgumentNullException(nameof(genericType));

            Type instanceType = instance.GetType();
            var cacheKey = new CacheKey(instanceType, methodName, genericType, Array.Empty<Type>(), typeof(TResult));

            if (!CompiledDelegatesCache.TryGetValue(cacheKey, out Delegate cachedDelegate))
            {
                cachedDelegate = CompileGenericFunc<TResult>(instanceType, methodName, genericType);
                CompiledDelegatesCache[cacheKey] = cachedDelegate;
            }

            return ((Func<object, TResult>)cachedDelegate).Invoke(instance);
        }

        /// <summary>
        /// Gọi một generic method với 1 parameter và có return value.
        /// </summary>
        public static TResult InvokeGenericFunc<TParam, TResult>(object instance, string methodName, Type genericType,
            TParam parameter)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            if (genericType == null) throw new ArgumentNullException(nameof(genericType));

            Type instanceType = instance.GetType();
            Type[] paramTypes = { typeof(TParam) };
            var cacheKey = new CacheKey(instanceType, methodName, genericType, paramTypes, typeof(TResult));

            if (!CompiledDelegatesCache.TryGetValue(cacheKey, out Delegate cachedDelegate))
            {
                cachedDelegate = CompileGenericFunc<TParam, TResult>(instanceType, methodName, genericType);
                CompiledDelegatesCache[cacheKey] = cachedDelegate;
            }

            return ((Func<object, TParam, TResult>)cachedDelegate).Invoke(instance, parameter);
        }

        #region Static Method Invocation

        /// <summary>
        /// Gọi một static generic method không có parameters và không có return value.
        /// </summary>
        /// <param name="targetType">Type chứa static method</param>
        /// <param name="methodName">Tên của generic method</param>
        /// <param name="genericType">Type argument cho generic method</param>
        public static void InvokeStaticGenericAction(Type targetType, string methodName, Type genericType)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            if (genericType == null) throw new ArgumentNullException(nameof(genericType));

            var cacheKey = new CacheKey(targetType, methodName, genericType, Array.Empty<Type>(), isStatic: true);

            if (!CompiledDelegatesCache.TryGetValue(cacheKey, out Delegate cachedDelegate))
            {
                cachedDelegate = CompileStaticGenericAction(targetType, methodName, genericType);
                CompiledDelegatesCache[cacheKey] = cachedDelegate;
            }

            ((Action)cachedDelegate).Invoke();
        }

        /// <summary>
        /// Gọi một static generic method với 1 parameter và không có return value.
        /// </summary>
        public static void InvokeStaticGenericAction<TParam>(Type targetType, string methodName, Type genericType,
            TParam parameter)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            if (genericType == null) throw new ArgumentNullException(nameof(genericType));

            Type[] paramTypes = { typeof(TParam) };
            var cacheKey = new CacheKey(targetType, methodName, genericType, paramTypes, isStatic: true);

            if (!CompiledDelegatesCache.TryGetValue(cacheKey, out Delegate cachedDelegate))
            {
                cachedDelegate = CompileStaticGenericAction<TParam>(targetType, methodName, genericType);
                CompiledDelegatesCache[cacheKey] = cachedDelegate;
            }

            ((Action<TParam>)cachedDelegate).Invoke(parameter);
        }

        /// <summary>
        /// Gọi một static generic method với 2 parameters và không có return value.
        /// </summary>
        public static void InvokeStaticGenericAction<TParam1, TParam2>(Type targetType, string methodName,
            Type genericType, TParam1 param1, TParam2 param2)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            if (genericType == null) throw new ArgumentNullException(nameof(genericType));

            Type[] paramTypes = { typeof(TParam1), typeof(TParam2) };
            var cacheKey = new CacheKey(targetType, methodName, genericType, paramTypes, isStatic: true);

            if (!CompiledDelegatesCache.TryGetValue(cacheKey, out Delegate cachedDelegate))
            {
                cachedDelegate = CompileStaticGenericAction<TParam1, TParam2>(targetType, methodName, genericType);
                CompiledDelegatesCache[cacheKey] = cachedDelegate;
            }

            ((Action<TParam1, TParam2>)cachedDelegate).Invoke(param1, param2);
        }

        /// <summary>
        /// Gọi một static generic method không có parameters và có return value.
        /// Ví dụ: Resources.Load&lt;T&gt;() trong Unity
        /// </summary>
        public static TResult InvokeStaticGenericFunc<TResult>(Type targetType, string methodName, Type genericType)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            if (genericType == null) throw new ArgumentNullException(nameof(genericType));

            var cacheKey = new CacheKey(targetType, methodName, genericType, Array.Empty<Type>(), typeof(TResult),
                isStatic: true);

            if (!CompiledDelegatesCache.TryGetValue(cacheKey, out Delegate cachedDelegate))
            {
                cachedDelegate = CompileStaticGenericFunc<TResult>(targetType, methodName, genericType);
                CompiledDelegatesCache[cacheKey] = cachedDelegate;
            }

            return ((Func<TResult>)cachedDelegate).Invoke();
        }

        /// <summary>
        /// Gọi một static generic method với 1 parameter và có return value.
        /// Ví dụ: Resources.Load&lt;T&gt;(string path) trong Unity
        /// </summary>
        public static TResult InvokeStaticGenericFunc<TParam, TResult>(Type targetType, string methodName,
            Type genericType, TParam parameter)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            if (genericType == null) throw new ArgumentNullException(nameof(genericType));

            Type[] paramTypes = { typeof(TParam) };
            var cacheKey = new CacheKey(targetType, methodName, genericType, paramTypes, typeof(TResult),
                isStatic: true);

            if (!CompiledDelegatesCache.TryGetValue(cacheKey, out Delegate cachedDelegate))
            {
                cachedDelegate = CompileStaticGenericFunc<TParam, TResult>(targetType, methodName, genericType);
                CompiledDelegatesCache[cacheKey] = cachedDelegate;
            }

            return ((Func<TParam, TResult>)cachedDelegate).Invoke(parameter);
        }

        /// <summary>
        /// Gọi một static generic method với 2 parameters và có return value.
        /// </summary>
        public static TResult InvokeStaticGenericFunc<TParam1, TParam2, TResult>(Type targetType, string methodName,
            Type genericType, TParam1 param1, TParam2 param2)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            if (string.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            if (genericType == null) throw new ArgumentNullException(nameof(genericType));

            Type[] paramTypes = { typeof(TParam1), typeof(TParam2) };
            var cacheKey = new CacheKey(targetType, methodName, genericType, paramTypes, typeof(TResult),
                isStatic: true);

            if (!CompiledDelegatesCache.TryGetValue(cacheKey, out Delegate cachedDelegate))
            {
                cachedDelegate =
                    CompileStaticGenericFunc<TParam1, TParam2, TResult>(targetType, methodName, genericType);
                CompiledDelegatesCache[cacheKey] = cachedDelegate;
            }

            return ((Func<TParam1, TParam2, TResult>)cachedDelegate).Invoke(param1, param2);
        }

        #endregion

        /// <summary>
        /// Clear cache để giải phóng memory nếu cần.
        /// </summary>
        public static void ClearCache()
        {
            CompiledDelegatesCache.Clear();
        }

        #region Compilation Methods

        private static Action<object> CompileGenericAction(Type instanceType, string methodName, Type genericType)
        {
            MethodInfo methodInfo = GetGenericMethod(instanceType, methodName, Array.Empty<Type>());
            MethodInfo genericMethod = methodInfo.MakeGenericMethod(genericType);

            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var instanceCast = Expression.Convert(instanceParam, instanceType);
            var callExpr = Expression.Call(instanceCast, genericMethod);
            var lambda = Expression.Lambda<Action<object>>(callExpr, instanceParam);

            return lambda.Compile();
        }

        private static Action<object, TParam> CompileGenericAction<TParam>(Type instanceType, string methodName,
            Type genericType)
        {
            MethodInfo methodInfo = GetGenericMethod(instanceType, methodName, new[] { typeof(TParam) });
            MethodInfo genericMethod = methodInfo.MakeGenericMethod(genericType);

            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var param1 = Expression.Parameter(typeof(TParam), "param1");
            var instanceCast = Expression.Convert(instanceParam, instanceType);
            var callExpr = Expression.Call(instanceCast, genericMethod, param1);
            var lambda = Expression.Lambda<Action<object, TParam>>(callExpr, instanceParam, param1);

            return lambda.Compile();
        }

        private static Action<object, TParam1, TParam2> CompileGenericAction<TParam1, TParam2>(Type instanceType,
            string methodName, Type genericType)
        {
            MethodInfo methodInfo =
                GetGenericMethod(instanceType, methodName, new[] { typeof(TParam1), typeof(TParam2) });
            MethodInfo genericMethod = methodInfo.MakeGenericMethod(genericType);

            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var param1 = Expression.Parameter(typeof(TParam1), "param1");
            var param2 = Expression.Parameter(typeof(TParam2), "param2");
            var instanceCast = Expression.Convert(instanceParam, instanceType);
            var callExpr = Expression.Call(instanceCast, genericMethod, param1, param2);
            var lambda = Expression.Lambda<Action<object, TParam1, TParam2>>(callExpr, instanceParam, param1, param2);

            return lambda.Compile();
        }

        private static Func<object, TResult> CompileGenericFunc<TResult>(Type instanceType, string methodName,
            Type genericType)
        {
            MethodInfo methodInfo = GetGenericMethod(instanceType, methodName, Array.Empty<Type>());
            MethodInfo genericMethod = methodInfo.MakeGenericMethod(genericType);

            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var instanceCast = Expression.Convert(instanceParam, instanceType);
            var callExpr = Expression.Call(instanceCast, genericMethod);
            var lambda = Expression.Lambda<Func<object, TResult>>(callExpr, instanceParam);

            return lambda.Compile();
        }

        private static Func<object, TParam, TResult> CompileGenericFunc<TParam, TResult>(Type instanceType,
            string methodName, Type genericType)
        {
            MethodInfo methodInfo = GetGenericMethod(instanceType, methodName, new[] { typeof(TParam) });
            MethodInfo genericMethod = methodInfo.MakeGenericMethod(genericType);

            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var param1 = Expression.Parameter(typeof(TParam), "param1");
            var instanceCast = Expression.Convert(instanceParam, instanceType);
            var callExpr = Expression.Call(instanceCast, genericMethod, param1);
            var lambda = Expression.Lambda<Func<object, TParam, TResult>>(callExpr, instanceParam, param1);

            return lambda.Compile();
        }

        private static Action CompileStaticGenericAction(Type targetType, string methodName, Type genericType)
        {
            MethodInfo methodInfo = GetGenericMethod(targetType, methodName, Array.Empty<Type>(), isStatic: true);
            MethodInfo genericMethod = methodInfo.MakeGenericMethod(genericType);

            var callExpr = Expression.Call(null, genericMethod);
            var lambda = Expression.Lambda<Action>(callExpr);

            return lambda.Compile();
        }

        private static Action<TParam> CompileStaticGenericAction<TParam>(Type targetType, string methodName,
            Type genericType)
        {
            MethodInfo methodInfo = GetGenericMethod(targetType, methodName, new[] { typeof(TParam) }, isStatic: true);
            MethodInfo genericMethod = methodInfo.MakeGenericMethod(genericType);

            var param1 = Expression.Parameter(typeof(TParam), "param1");
            var callExpr = Expression.Call(null, genericMethod, param1);
            var lambda = Expression.Lambda<Action<TParam>>(callExpr, param1);

            return lambda.Compile();
        }

        private static Action<TParam1, TParam2> CompileStaticGenericAction<TParam1, TParam2>(Type targetType,
            string methodName, Type genericType)
        {
            MethodInfo methodInfo = GetGenericMethod(targetType, methodName, new[] { typeof(TParam1), typeof(TParam2) },
                isStatic: true);
            MethodInfo genericMethod = methodInfo.MakeGenericMethod(genericType);

            var param1 = Expression.Parameter(typeof(TParam1), "param1");
            var param2 = Expression.Parameter(typeof(TParam2), "param2");
            var callExpr = Expression.Call(null, genericMethod, param1, param2);
            var lambda = Expression.Lambda<Action<TParam1, TParam2>>(callExpr, param1, param2);

            return lambda.Compile();
        }

        private static Func<TResult> CompileStaticGenericFunc<TResult>(Type targetType, string methodName,
            Type genericType)
        {
            MethodInfo methodInfo = GetGenericMethod(targetType, methodName, Array.Empty<Type>(), isStatic: true);
            MethodInfo genericMethod = methodInfo.MakeGenericMethod(genericType);

            var callExpr = Expression.Call(null, genericMethod);
            var lambda = Expression.Lambda<Func<TResult>>(callExpr);

            return lambda.Compile();
        }

        private static Func<TParam, TResult> CompileStaticGenericFunc<TParam, TResult>(Type targetType,
            string methodName, Type genericType)
        {
            MethodInfo methodInfo = GetGenericMethod(targetType, methodName, new[] { typeof(TParam) }, isStatic: true);
            MethodInfo genericMethod = methodInfo.MakeGenericMethod(genericType);

            var param1 = Expression.Parameter(typeof(TParam), "param1");
            var callExpr = Expression.Call(null, genericMethod, param1);
            var lambda = Expression.Lambda<Func<TParam, TResult>>(callExpr, param1);

            return lambda.Compile();
        }

        private static Func<TParam1, TParam2, TResult> CompileStaticGenericFunc<TParam1, TParam2, TResult>(
            Type targetType, string methodName, Type genericType)
        {
            MethodInfo methodInfo = GetGenericMethod(targetType, methodName, new[] { typeof(TParam1), typeof(TParam2) },
                isStatic: true);
            MethodInfo genericMethod = methodInfo.MakeGenericMethod(genericType);

            var param1 = Expression.Parameter(typeof(TParam1), "param1");
            var param2 = Expression.Parameter(typeof(TParam2), "param2");
            var callExpr = Expression.Call(null, genericMethod, param1, param2);
            var lambda = Expression.Lambda<Func<TParam1, TParam2, TResult>>(callExpr, param1, param2);

            return lambda.Compile();
        }

        private static MethodInfo GetGenericMethod(Type instanceType, string methodName, Type[] parameterTypes,
            bool isStatic = false)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic;
            flags |= isStatic ? BindingFlags.Static : BindingFlags.Instance;

            MethodInfo methodInfo = null;

            foreach (var method in instanceType.GetMethods(flags))
            {
                if (method.Name != methodName || !method.IsGenericMethodDefinition)
                    continue;

                var parameters = method.GetParameters();
                if (parameters.Length != parameterTypes.Length)
                    continue;

                bool match = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    Type paramType = parameters[i].ParameterType;
                    // Handle generic parameter types
                    if (!paramType.IsGenericParameter && paramType != parameterTypes[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    methodInfo = method;
                    break;
                }
            }

            if (methodInfo == null)
                throw new InvalidOperationException(
                    $"Generic method '{methodName}' not found on type '{instanceType.Name}'");

            return methodInfo;
        }

        #endregion

        #region Cache Key

        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            private readonly Type _instanceType;
            private readonly string _methodName;
            private readonly Type _genericType;
            private readonly Type[] _parameterTypes;
            private readonly Type _returnType;
            private readonly bool _isStatic;
            private readonly int _hashCode;

            public CacheKey(Type instanceType, string methodName, Type genericType, Type[] parameterTypes,
                Type returnType = null, bool isStatic = false)
            {
                _instanceType = instanceType;
                _methodName = methodName;
                _genericType = genericType;
                _parameterTypes = parameterTypes;
                _returnType = returnType;
                _isStatic = isStatic;

                // Pre-calculate hash code
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + (_instanceType?.GetHashCode() ?? 0);
                    hash = hash * 31 + (_methodName?.GetHashCode() ?? 0);
                    hash = hash * 31 + (_genericType?.GetHashCode() ?? 0);
                    hash = hash * 31 + (_returnType?.GetHashCode() ?? 0);
                    hash = hash * 31 + _isStatic.GetHashCode();

                    if (_parameterTypes != null)
                    {
                        foreach (var paramType in _parameterTypes)
                        {
                            hash = hash * 31 + (paramType?.GetHashCode() ?? 0);
                        }
                    }

                    _hashCode = hash;
                }
            }

            public bool Equals(CacheKey other)
            {
                if (_instanceType != other._instanceType) return false;
                if (_methodName != other._methodName) return false;
                if (_genericType != other._genericType) return false;
                if (_returnType != other._returnType) return false;
                if (_isStatic != other._isStatic) return false;

                if (_parameterTypes == null && other._parameterTypes == null) return true;
                if (_parameterTypes == null || other._parameterTypes == null) return false;
                if (_parameterTypes.Length != other._parameterTypes.Length) return false;

                for (int i = 0; i < _parameterTypes.Length; i++)
                {
                    if (_parameterTypes[i] != other._parameterTypes[i])
                        return false;
                }

                return true;
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }
        }

        #endregion
    }
}
