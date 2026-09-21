using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DracoRuan.Foundation.DataFlow.StaticData.Sources;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.Editor
{
    /// <summary>
    /// One static data table in the Static Config Data Manager: which controller declares it, where
    /// its source file is, and the fallback chain being edited.
    /// </summary>
    /// <remarks>
    /// <para><b>The chain is read from source, not from the controller.</b> <c>Sources</c> is
    /// <c>protected abstract</c> and a controller cannot be constructed without the container, so
    /// there is nothing to reflect over in edit mode. <see cref="StaticDataChainSource"/> does the
    /// reading and writing; this class owns the edited state and the mapping between assets and
    /// keys.</para>
    ///
    /// <para><b>Errors are returned, never thrown.</b> Every mutating method returns a bool and
    /// leaves the reason in <see cref="LastError"/>, matching <see cref="LocalDataEntry"/> so the
    /// window handles both tools the same way.</para>
    /// </remarks>
    public sealed class StaticDataEntry
    {
        private static readonly Regex PrettyNamePattern = new("(\\B[A-Z])", RegexOptions.Compiled);

        /// <summary>
        /// Trailing <c>Controller</c> on a controller class name. Stripped because every row in the
        /// list is a controller, so the word carries no information and only makes the names longer
        /// and harder to tell apart.
        /// </summary>
        private static readonly Regex ControllerSuffixPattern = new("Controller$", RegexOptions.Compiled);

        private static readonly Dictionary<Type, string> PrettyNameCache = new();

        private readonly List<StaticDataChainStep> _steps = new();

        private StaticDataChainSource _source;
        private DateTime _parsedWriteTimeUtc;

        public StaticDataEntry(string dataId, Type controllerType)
        {
            this.DataId = dataId;
            this.ControllerType = controllerType;
            this.DisplayName = GetPrettyName(controllerType);
            this.ScriptPath = FindScriptPath(controllerType);

            this.Reload();
        }

        public string DataId { get; }
        public Type ControllerType { get; }

        /// <summary>Human-readable name, from the controller type: <c>GachaRateController</c> → "Gacha Rate".</summary>
        public string DisplayName { get; }

        /// <summary>Project-relative path of the <c>.cs</c> file, or null when it could not be found.</summary>
        public string ScriptPath { get; }

        /// <summary>The chain being edited. Mutated in place by the window.</summary>
        public IReadOnlyList<StaticDataChainStep> Steps => this._steps;

        /// <summary>Why the chain could not be read, or null when it parsed.</summary>
        public string ParseError { get; private set; }

        /// <summary>Last failure from <see cref="Apply"/>, or null.</summary>
        public string LastError { get; private set; }

        /// <summary>True when the chain parsed and can be rewritten.</summary>
        public bool IsEditable => this._source != null;

        /// <summary>True when the edited chain differs from what is in the file.</summary>
        public bool IsDirty { get; private set; }

        // -----------------------------------------------------------------
        // Reading
        // -----------------------------------------------------------------

        /// <summary>Re-reads the chain from disk, discarding any unapplied edits.</summary>
        public void Reload()
        {
            this._steps.Clear();
            this._source = null;
            this.ParseError = null;
            this.LastError = null;
            this.IsDirty = false;

            if (string.IsNullOrEmpty(this.ScriptPath))
            {
                this.ParseError =
                    $"Could not locate a script file for {this.ControllerType.FullName}. " +
                    "The chain can only be edited when its source file is in this project.";
                return;
            }

            string text;
            try
            {
                text = File.ReadAllText(this.ScriptPath);
                this._parsedWriteTimeUtc = File.GetLastWriteTimeUtc(this.ScriptPath);
            }
            catch (Exception exception)
            {
                this.ParseError = $"Could not read {this.ScriptPath}: {exception.Message}";
                return;
            }

            this._source = StaticDataChainSource.TryParse(this.ScriptPath, text);
            if (this._source == null)
            {
                this.ParseError = StaticDataChainSource.LastParseError;
                return;
            }

            foreach (StaticDataChainStep step in this._source.Steps)
                this._steps.Add(step);

            this.ResolveAssetsForSteps();
        }

        /// <summary>Marks the chain as edited. Called by the window after any change to a step.</summary>
        public void MarkDirty()
        {
            this.IsDirty = true;
        }

        // -----------------------------------------------------------------
        // Editing
        // -----------------------------------------------------------------

        public void AddStep()
        {
            this._steps.Add(StaticDataChainStep.NewEditable(StaticDataSourceType.Resources, string.Empty));
            this.MarkDirty();
        }

        public void RemoveStep(int index)
        {
            if (index < 0 || index >= this._steps.Count)
                return;

            this._steps.RemoveAt(index);
            this.MarkDirty();
        }

        /// <summary>
        /// Moves the step at <paramref name="from"/> to <paramref name="to"/>, shifting the rest —
        /// how a drag reorders the chain.
        /// </summary>
        /// <remarks>
        /// Removes and re-inserts rather than swapping. Dragging a row from the top to the bottom
        /// should leave everything between it shifted up one, which is what the eye expects from a
        /// row following the cursor; a swap would instead fling the bottom row to the top.
        /// </remarks>
        public void MoveStepTo(int from, int to)
        {
            if (from < 0 || from >= this._steps.Count || to < 0 || to >= this._steps.Count || from == to)
                return;

            StaticDataChainStep moved = this._steps[from];
            this._steps.RemoveAt(from);
            this._steps.Insert(to, moved);

            MoveAsset(this._assets, from, to);
            this.MarkDirty();
        }

        /// <summary>
        /// Keeps the object-field list lined up with the steps it describes. Without this, the asset
        /// shown against a row would be the one that used to sit at that position.
        /// </summary>
        private static void MoveAsset(List<UnityEngine.Object> assets, int from, int to)
        {
            if (from < 0 || from >= assets.Count)
                return;

            UnityEngine.Object moved = assets[from];
            assets.RemoveAt(from);
            assets.Insert(Mathf.Clamp(to, 0, assets.Count), moved);
        }

        // -----------------------------------------------------------------
        // Writing
        // -----------------------------------------------------------------

        /// <summary>The chain call as it stands in the file.</summary>
        public string GetCurrentChainText() =>
            this._source == null ? string.Empty : this._source.GetCurrentChainText();

        /// <summary>The chain call the current edits would produce.</summary>
        public string GetPreviewChainText() =>
            this._source == null ? string.Empty : this._source.PreviewChainText(this._steps);

        /// <summary>
        /// Checks the edited chain for anything that would not compile or would not resolve at
        /// runtime. Returns null when it is fine.
        /// </summary>
        public string Validate()
        {
            if (this._steps.Count == 0)
                return "The chain has no sources, so this table could never load.";

            bool hasEnabled = false;

            foreach (StaticDataChainStep step in this._steps)
            {
                if (!step.IsEnabled)
                    continue;

                hasEnabled = true;

                if (step.IsKeyEditable && string.IsNullOrWhiteSpace(step.Key))
                    return $"The {step.SourceType} step has an empty key.";
            }

            return hasEnabled ? null : "Every source is disabled, so this table could never load.";
        }

        /// <summary>
        /// Writes the edited chain back to the source file. Returns false and sets
        /// <see cref="LastError"/> without touching the file on any failure.
        /// </summary>
        public bool Apply()
        {
            this.LastError = null;

            if (this._source == null)
            {
                this.LastError = this.ParseError ?? "This chain cannot be edited.";
                return false;
            }

            string invalid = this.Validate();
            if (invalid != null)
            {
                this.LastError = invalid;
                return false;
            }

            try
            {
                // Someone may have edited the file since it was parsed - in Rider, or by the
                // previous Apply in this same batch. Writing the remembered text back would silently
                // revert their change, so the write is refused instead.
                if (File.GetLastWriteTimeUtc(this.ScriptPath) != this._parsedWriteTimeUtc)
                {
                    this.LastError =
                        "The file changed on disk after it was read. Press Refresh and redo the edit.";
                    return false;
                }

                File.WriteAllText(this.ScriptPath, this._source.Render(this._steps));
            }
            catch (Exception exception)
            {
                this.LastError = exception.Message;
                return false;
            }

            this.Reload();
            return this.ParseError == null;
        }

        // -----------------------------------------------------------------
        // Assets and keys
        // -----------------------------------------------------------------

        /// <summary>
        /// The asset a Resources or Addressable step points at, so the object field shows what is
        /// already configured instead of starting empty.
        /// </summary>
        public UnityEngine.Object GetAsset(int index)
        {
            if (index < 0 || index >= this._assets.Count)
                return null;

            return this._assets[index];
        }

        private readonly List<UnityEngine.Object> _assets = new();

        /// <summary>
        /// Sets a step's key from a dropped asset. Returns a warning when the asset cannot be
        /// addressed by that source, rather than writing a key that would miss at runtime.
        /// </summary>
        public string SetAssetForStep(int index, UnityEngine.Object asset)
        {
            if (index < 0 || index >= this._steps.Count)
                return null;

            while (this._assets.Count <= index)
                this._assets.Add(null);

            this._assets[index] = asset;
            this.MarkDirty();

            if (asset == null)
                return null;

            StaticDataChainStep step = this._steps[index];
            string assetPath = AssetDatabase.GetAssetPath(asset);

            if (step.SourceType == StaticDataSourceType.Resources)
            {
                string resourcesKey = ToResourcesKey(assetPath);
                if (resourcesKey == null)
                    return "That asset is not under a Resources folder, so Resources.Load cannot find it.";

                step.Key = resourcesKey;
                return null;
            }

            if (step.SourceType != StaticDataSourceType.Addressable)
                return null;

            string address = ToAddressableKey(assetPath, out string addressError);
            if (address == null)
                return addressError;

            step.Key = address;
            return null;
        }

        /// <summary>Re-resolves the object fields after a reload, so they show the configured assets.</summary>
        private void ResolveAssetsForSteps()
        {
            this._assets.Clear();

            foreach (StaticDataChainStep step in this._steps)
                this._assets.Add(FindAssetForStep(step));
        }

        private static UnityEngine.Object FindAssetForStep(StaticDataChainStep step)
        {
            if (!step.IsKeyEditable || string.IsNullOrEmpty(step.Key))
                return null;

            if (step.SourceType == StaticDataSourceType.Resources)
                return Resources.Load(step.Key);

            if (step.SourceType != StaticDataSourceType.Addressable ||
                !AddressableAssetSettingsDefaultObject.SettingsExists)
                return null;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return null;

            List<AddressableAssetEntry> entries = new();
            settings.GetAllAssets(entries, includeSubObjects: false);

            foreach (AddressableAssetEntry entry in entries)
            {
                if (!string.Equals(entry.address, step.Key, StringComparison.Ordinal))
                    continue;

                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(entry.AssetPath);
            }

            return null;
        }

        /// <summary>
        /// Converts an asset path to the key <c>Resources.Load</c> expects: the part after
        /// <c>Resources/</c>, without the extension. Returns null when the asset is not under one.
        /// </summary>
        private static string ToResourcesKey(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            string normalized = assetPath.Replace('\\', '/');
            const string Marker = "/Resources/";

            int marker = normalized.LastIndexOf(Marker, StringComparison.Ordinal);
            if (marker < 0)
                return null;

            string relative = normalized.Substring(marker + Marker.Length);
            int dot = relative.LastIndexOf('.');

            return dot > 0 ? relative.Substring(0, dot) : relative;
        }

        /// <summary>
        /// Reads the Addressables address for an asset, or explains why there is not one. An asset
        /// that was never marked addressable has no address at all, and guessing a path would
        /// produce a key that always misses.
        /// </summary>
        private static string ToAddressableKey(string assetPath, out string error)
        {
            error = null;

            if (!AddressableAssetSettingsDefaultObject.SettingsExists)
            {
                error = "This project has no Addressables settings yet, so the asset has no address.";
                return null;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                error = "This project has no Addressables settings yet, so the asset has no address.";
                return null;
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            AddressableAssetEntry entry = settings.FindAssetEntry(guid);

            if (entry != null)
                return entry.address;

            error = "That asset is not marked Addressable, so it has no address to load by.";
            return null;
        }

        // -----------------------------------------------------------------
        // Discovery helpers
        // -----------------------------------------------------------------

        /// <summary>
        /// Finds the <c>.cs</c> file declaring <paramref name="type"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="MonoScript.GetClass"/> only resolves when the class name matches the file
        /// name, which is not guaranteed here — <c>StaticDataController&lt;TData&gt;</c> already
        /// lives in <c>ScriptableStaticDataController.cs</c>. So the name match is tried first and a
        /// text search for the declaration is the fallback.
        /// </remarks>
        private static string FindScriptPath(Type type)
        {
            string[] guids = AssetDatabase.FindAssets($"t:MonoScript {type.Name}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script != null && script.GetClass() == type)
                    return path;
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (DeclaresType(path, type))
                    return path;
            }

            return null;
        }

        /// <summary>Cheap textual check that a file declares the given class.</summary>
        private static bool DeclaresType(string path, Type type)
        {
            try
            {
                string text = File.ReadAllText(path);
                return text.Contains($"class {type.Name}");
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string GetPrettyName(Type type)
        {
            if (PrettyNameCache.TryGetValue(type, out string cached))
                return cached;

            // Strip before spacing out the capitals, so the suffix is one token rather than a word
            // the pattern would then have to cope with a space in front of.
            string stem = ControllerSuffixPattern.Replace(type.Name, string.Empty);

            // A class named nothing but "Controller" would strip to nothing. Keep the original: a
            // blank row is worse than a redundant one.
            if (stem.Length == 0)
                stem = type.Name;

            string pretty = PrettyNamePattern.Replace(stem, " $1");
            PrettyNameCache[type] = pretty;
            return pretty;
        }
    }
}