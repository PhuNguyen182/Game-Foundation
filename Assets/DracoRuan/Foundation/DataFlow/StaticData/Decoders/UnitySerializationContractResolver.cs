using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.StaticData.Decoders
{
    /// <summary>
    /// Makes Newtonsoft follow Unity's serialization rules instead of its own.
    /// </summary>
    /// <remarks>
    /// <para><b>This is the difference between remote config working and failing silently.</b>
    /// Newtonsoft looks at public fields and public properties. Unity looks at public fields and
    /// private fields marked <c>[SerializeField]</c>, and ignores properties entirely. Config
    /// ScriptableObjects are written the Unity way, so left to its defaults Newtonsoft would populate
    /// none of a typical config asset's fields — returning an object full of zeroes with no exception
    /// and nothing in the log. Remote config is the first source in a normal chain, so that empty
    /// object would then win over the perfectly good asset shipped in the build.</para>
    ///
    /// <para>Matching Unity's rules also means the remote JSON's shape is exactly the shape of the
    /// <c>.asset</c> file and of the Inspector. One data model, two sources, one field naming
    /// convention — nobody has to keep a second schema in their head.</para>
    ///
    /// <para><b>The walk stops at <see cref="ScriptableObject"/>.</b> <c>UnityEngine.Object</c> holds
    /// private native bookkeeping fields (the cached native pointer among them); serializing or,
    /// worse, populating those would corrupt the object.</para>
    /// </remarks>
    public sealed class UnitySerializationContractResolver : DefaultContractResolver
    {
        public static readonly UnitySerializationContractResolver Instance = new();

        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            List<MemberInfo> members = new();

            // DeclaredOnly plus an explicit walk up the hierarchy: GetFields with NonPublic does not
            // return private fields declared on base types, which would quietly drop every field of a
            // shared config base class.
            for (Type type = objectType;
                 type != null && type != typeof(ScriptableObject) && type != typeof(object);
                 type = type.BaseType)
            {
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);

                foreach (FieldInfo field in fields)
                {
                    if (IsUnitySerialized(field))
                        members.Add(field);
                }
            }

            return members;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            // A field selected above is always both readable and writable, whatever its accessibility.
            // Without this, Newtonsoft skips the private ones it cannot see a setter for.
            if (member is FieldInfo)
            {
                property.Readable = true;
                property.Writable = true;
            }

            return property;
        }

        private static bool IsUnitySerialized(FieldInfo field)
        {
            // readonly and const fields cannot be written back, and Unity does not serialize them.
            if (field.IsStatic || field.IsInitOnly || field.IsLiteral)
                return false;

            if (field.IsDefined(typeof(NonSerializedAttribute), inherit: false))
                return false;

            return field.IsPublic || field.IsDefined(typeof(SerializeField), inherit: false);
        }
    }
}
