using System.Collections.Generic;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Data
{
    /// <summary>A set of view definitions fed to the installer to build a UIRegistry from.</summary>
    [CreateAssetMenu(fileName = "UIViewCollection", menuName = "DracoRuan/UISystem/View Collection")]
    public sealed class UIViewCollection : ScriptableObject
    {
        [SerializeField] private List<UIViewDefinition> definitions = new List<UIViewDefinition>();

        public IReadOnlyList<UIViewDefinition> Definitions => this.definitions;
    }
}
