using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.AudioSystem.Data.Attributes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AudioSystem.Data
{
    /// <summary>
    /// A group of entries that belong together, usually everything on one channel.
    /// </summary>
    /// <remarks>
    /// Categories exist for the humans: they keep the collection navigable and let a designer see
    /// at a glance what plays on the music bus. They carry no runtime meaning — an entry's channel
    /// comes from the entry itself, so filing it under the wrong category cannot mis-route it.
    /// </remarks>
    [Serializable]
    public class AudioCategory
    {
        [Tooltip("What this group is for. Free text; purely organisational.")]
        [SerializeField] private string _name;

        [AudioChannelId(allowEmpty: true)]
        [Tooltip("The channel these entries are expected to use. Advisory only.")]
        [SerializeField] private string _channelId;

        [ListDrawerSettings(ShowFoldout = true)]
        [SerializeField] private List<AudioEntry> _entries = new List<AudioEntry>();

        public string Name => this._name;
        public string ChannelId => this._channelId;
        public List<AudioEntry> Entries => this._entries;
    }
}
