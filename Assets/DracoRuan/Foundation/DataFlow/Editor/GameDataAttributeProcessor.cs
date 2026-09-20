using System;
using System.Collections.Generic;
using System.Reflection;
using DracoRuan.Foundation.DataFlow.LocalData;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// Makes plain public properties on save data models visible in the Local Data Manager.
    /// </summary>
    /// <remarks>
    /// <para><b>The generic type parameter is the entire point of this class.</b> An
    /// <see cref="OdinAttributeProcessor"/> with no type argument is a <i>global</i> processor: Odin
    /// invokes it for every member of every property of every <c>PropertyTree</c> in the editor —
    /// every Inspector, every Odin window, all the time — and each call did a type check plus up to
    /// four string comparisons. That cost was paid project-wide, not just in this tool, which is why
    /// unrelated Inspectors felt slow.</para>
    ///
    /// <para>Constraining it to <c>OdinAttributeProcessor&lt;T&gt; where T : IGameData</c> lets Odin
    /// resolve applicability <b>once per type</b>, from its own cache, and never consider it for
    /// anything that is not a save model. The filtering that used to live inside the method is no
    /// longer needed, because the method is no longer called for unrelated types.</para>
    /// </remarks>
    public sealed class GameDataAttributeProcessor<T> : OdinAttributeProcessor<T>
        where T : IGameData
    {
        public override void ProcessChildMemberAttributes(
            InspectorProperty parentProperty,
            MemberInfo member,
            List<Attribute> attributes)
        {
            if (!IsPublic(member))
                return;

            if (attributes.Exists(attribute => attribute is ShowInInspectorAttribute))
                return;

            attributes.Add(new ShowInInspectorAttribute());
        }

        private static bool IsPublic(MemberInfo member) => member switch
        {
            PropertyInfo property => property.GetMethod is { IsPublic: true },
            FieldInfo field => field.IsPublic,
            _ => false
        };
    }
}
