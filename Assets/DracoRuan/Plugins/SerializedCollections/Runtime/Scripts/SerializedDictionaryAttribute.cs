using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace AYellowpaper.SerializedCollections
{
    [Conditional("UNITY_EDITOR")]
    public class SerializedDictionaryAttribute : Attribute
    {
        public readonly string KeyName;
        public readonly string ValueName;
        public bool IsReadOnlyKey { get; set; }
        public bool IsReadOnlyValue { get; set; }
        public bool CanAddElement { get; set; } = true;
        public bool CanRemoveElement { get; set; } = true;
        public string CustomAddAction { get; set; }

        public SerializedDictionaryAttribute(string keyName = null, string valueName = null)
        {
            KeyName = keyName;
            ValueName = valueName;
        }
    }
}