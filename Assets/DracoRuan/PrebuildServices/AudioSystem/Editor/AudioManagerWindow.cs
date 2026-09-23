using System;
using System.Collections.Generic;
using DracoRuan.PrebuildServices.AudioSystem.Data;
using DracoRuan.PrebuildServices.AudioSystem.Logic;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.AudioSystem.Editor
{
    /// <summary>
    /// Creates, inspects and deletes audio entries, and generates the identifier class.
    /// </summary>
    /// <remarks>
    /// <para>Plain IMGUI, like the other two Foundations tools, and built on the same skeleton:
    /// coalesced repaints through <see cref="_needsRepaint"/>, an <c>EditorPrefs</c> splitter, a
    /// paged list, and Odin used as a helper rather than as a base class.</para>
    ///
    /// <para>The detail pane draws the selected asset with Odin's <c>PropertyTree</c>, which is one
    /// line and renders the entry exactly as the Inspector would, <c>[ShowIf]</c> on the hybrid clip
    /// field included.</para>
    /// </remarks>
    public sealed class AudioManagerWindow : EditorWindow
    {
        private enum Tab
        {
            Entries = 0,
            Channels = 1,
            GeneratedCode = 2,
        }

        private const float ToolbarHeight = 34f;
        private const float ToolbarControlHeight = 24f;
        private const float SplitterWidth = 4f;
        private const float MinListWidth = 200f;
        private const float MaxListWidth = 460f;
        private const float EntryRowHeight = 44f;
        private const int EntriesPerPage = 10;

        private static readonly Color ToolbarBackgroundColor = new Color(0.19f, 0.19f, 0.19f, 1f);
        private static readonly Color SplitterColor = new Color(0.14f, 0.14f, 0.14f, 1f);
        private static readonly Color SplitterHoverColor = new Color(0.35f, 0.55f, 0.85f, 1f);
        private static readonly Color NeutralColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color GoodColor = new Color(0.45f, 0.78f, 0.55f, 1f);
        private static readonly Color WarnColor = new Color(0.95f, 0.78f, 0.35f, 1f);
        private static readonly Color BadColor = new Color(0.95f, 0.45f, 0.45f, 1f);

        private readonly List<AudioEntry> _entries = new List<AudioEntry>();
        private readonly List<AudioEntry> _visibleEntries = new List<AudioEntry>();

        private PropertyTree _detailTree;
        private AudioEntry _selected;

        private PropertyTree _channelsTree;
        private AudioConfig _channelsTreeConfig;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private Vector2 _codeScroll;
        private string _search = string.Empty;
        private float _listWidth = 260f;
        private int _currentPage;
        private Tab _tab = Tab.Entries;

        private bool _isHoveringSplitter;
        private bool _isDraggingSplitter;
        private bool _needsRepaint;

        private bool _isCreating;
        private string _newId = string.Empty;
        private string _newChannelId = string.Empty;
        private AudioClip _newClip;
        private string _newIdFeedback;
        private MessageType _newIdFeedbackType = MessageType.None;
        private string _lastCheckedId;

        private AudioIdGenerationPlan _plan;

        [MenuItem("Tools/Foundations/Audio Editor/Audio Manager", false, 102)]
        public static void ShowWindow()
        {
            AudioManagerWindow window = GetWindow<AudioManagerWindow>();
            window.titleContent = new GUIContent("🔊 Audio Manager");
            window.minSize = new Vector2(860f, 460f);
            window.Show();
        }

        [MenuItem("Tools/Foundations/Audio Editor/Regenerate Audio Ids", false, 103)]
        public static void RegenerateFromMenu()
        {
            AudioIdIndex.Invalidate();
            AudioIdGenerationPlan plan = AudioIdGenerationService.BuildPlan();

            if (!plan.CanApply)
            {
                AudioDialogs.Report("Cannot generate audio ids", plan.BlockingError);
                return;
            }

            if (!plan.IsNoOp && !AudioDialogs.ConfirmGenerate(plan))
                return;

            if (!AudioIdGenerationService.Apply(plan, out string error))
                AudioDialogs.Report("Cannot generate audio ids", error);
        }

        private void OnEnable()
        {
            this._listWidth = EditorPrefs.GetFloat(AudioEditorState.SplitWidthKey, 260f);
            this._tab = (Tab)EditorPrefs.GetInt(AudioEditorState.TabKey, 0);

            this.Rescan();
            this.RestoreSelection();
        }

        private void OnDisable()
        {
            EditorPrefs.SetFloat(AudioEditorState.SplitWidthKey, this._listWidth);
            EditorPrefs.SetInt(AudioEditorState.TabKey, (int)this._tab);
            this.ReleaseDetailTree();
            this.ReleaseChannelsTree();
        }

        private void OnGUI()
        {
            using (NoFoldoutAnimationScope.Enter())
            {
                this.DrawToolbar();
                this.DrawTabs();
                this.DrawMissingCollectionBanner();
            }

            // DrawDetail is deliberately outside the scope above: it is the only place this window
            // draws an Odin PropertyTree, and AudioEntry's [ShowIf] groups (clip mode, 3D settings)
            // rely on SirenixEditorGUI's fade groups. A zeroed duration drives their internal
            // AnimBool speed to infinity, and on the first draw after selecting an entry that
            // multiplies against a zero elapsed time, producing NaN heights — which is what showed up
            // as large blank gaps swallowing several fields. The list and status bar never draw Odin
            // content, so they keep the zero-duration perf fix with no such risk.
            if (this._tab == Tab.GeneratedCode)
            {
                this.DrawGeneratedCode();
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (NoFoldoutAnimationScope.Enter())
                        this.DrawList();

                    this.DrawSplitter();
                    this.DrawDetail();
                }
            }

            using (NoFoldoutAnimationScope.Enter())
                this.DrawStatusBar();

            if (!this._needsRepaint)
                return;

            this._needsRepaint = false;
            this.Repaint();
        }

        /// <summary>
        /// Zeroes Odin's global fade-group duration for one <see cref="OnGUI"/> call and puts it
        /// back. The static is shared with every Odin-drawn window, so it cannot just be set once.
        /// </summary>
        private readonly struct NoFoldoutAnimationScope : IDisposable
        {
            private readonly float _previousDuration;

            private NoFoldoutAnimationScope(float previousDuration) => this._previousDuration = previousDuration;

            public static NoFoldoutAnimationScope Enter()
            {
                float previous = SirenixEditorGUI.DefaultFadeGroupDuration;
                SirenixEditorGUI.DefaultFadeGroupDuration = 0f;
                return new NoFoldoutAnimationScope(previous);
            }

            public void Dispose() => SirenixEditorGUI.DefaultFadeGroupDuration = this._previousDuration;
        }

        #region Toolbar and tabs

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(ToolbarHeight)))
            {
                Rect bar = GUILayoutUtility.GetRect(0f, ToolbarHeight, GUILayout.ExpandWidth(true));
                if (Event.current.type == EventType.Repaint)
                    EditorGUI.DrawRect(bar, ToolbarBackgroundColor);

                float centerY = bar.y + (bar.height * 0.5f);
                float cursorX = bar.x + 6f;

                cursorX = DrawButtonAt(cursorX, centerY, "🔄 Refresh", NeutralColor, 90f, this.Rescan) + 6f;
                cursorX = DrawButtonAt(cursorX, centerY, "➕ New Entry", GoodColor, 108f, this.BeginCreate) + 6f;

                bool stale = SessionState.GetBool(AudioEditorState.IdsStaleKey, false);
                cursorX = DrawButtonAt(cursorX, centerY, stale ? "⚙️ Generate Ids *" : "⚙️ Generate Ids",
                    stale ? WarnColor : NeutralColor, 132f, this.Generate) + 6f;

                bool hasCollection = AudioDatabaseLocator.Find().Found;
                using (new EditorGUI.DisabledScope(!hasCollection))
                    DrawButtonAt(cursorX, centerY, "📌 Ping Collection", NeutralColor, 140f, this.PingCollection);

                float deleteWidth = 104f;
                float deleteX = bar.xMax - 6f - deleteWidth;
                DrawButtonAt(deleteX, centerY, "🗑 Delete", BadColor, deleteWidth, this.DeleteSelected);

                float searchWidth = Mathf.Clamp(bar.width - 620f, 120f, 360f);
                float searchX = deleteX - 12f - searchWidth;
                Rect searchRect = new Rect(searchX, centerY - (ToolbarControlHeight * 0.5f),
                    searchWidth, ToolbarControlHeight);

                EditorGUI.BeginChangeCheck();
                this._search = EditorGUI.TextField(searchRect, this._search, EditorStyles.toolbarSearchField);
                if (EditorGUI.EndChangeCheck())
                {
                    this._currentPage = 0;
                    this.RebuildVisible();
                    this._needsRepaint = true;
                }
            }
        }

        private void DrawTabs()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                this.DrawTabToggle(Tab.Entries, "Entries");
                this.DrawTabToggle(Tab.Channels, "Channels");
                this.DrawTabToggle(Tab.GeneratedCode, "Generated Code");
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawMissingCollectionBanner()
        {
            AudioDatabaseLocator.Result database = AudioDatabaseLocator.Find();
            if (database.Found)
                return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(database.Error, EditorStyles.wordWrappedLabel);

                // Offering "Create" while more than one collection already exists would only make the
                // ambiguity worse - the fix there is deleting or merging, not adding a third.
                if (database.Candidates.Count == 0 && GUILayout.Button("Create Collection…", GUILayout.Width(140f)))
                {
                    AudioCollection created = AudioCollectionCreationService.CreateInteractive(out string error);

                    if (error != null)
                        AudioDialogs.Report("Could not create the collection", error);
                    else if (created != null)
                        AssetDatabase.Refresh();
                }
            }
        }

        private void PingCollection()
        {
            AudioDatabaseLocator.Result database = AudioDatabaseLocator.Find();
            if (database.Found)
                EditorGUIUtility.PingObject(database.Collection);
        }

        private void DrawTabToggle(Tab tab, string label)
        {
            bool active = this._tab == tab;
            if (GUILayout.Toggle(active, label, EditorStyles.toolbarButton, GUILayout.Width(120f)) == active)
                return;

            this._tab = tab;
            this._plan = null;
            this._needsRepaint = true;
        }

        private static float DrawButtonAt(float x, float centerY, string label, Color tint, float width, Action clicked)
        {
            Rect rect = new Rect(x, centerY - (ToolbarControlHeight * 0.5f), width, ToolbarControlHeight);

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = tint;

            if (GUI.Button(rect, label, ButtonStyle))
                clicked();

            GUI.backgroundColor = previous;
            return x + width;
        }

        private static GUIStyle _buttonStyle;

        private static GUIStyle ButtonStyle => _buttonStyle ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };

        #endregion

        #region List

        private int PageCount => Mathf.Max(1, Mathf.CeilToInt(this._visibleEntries.Count / (float)EntriesPerPage));

        private void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(this._listWidth)))
            {
                this._listScroll = EditorGUILayout.BeginScrollView(this._listScroll);

                int start = this._currentPage * EntriesPerPage;
                int count = Mathf.Min(EntriesPerPage, this._visibleEntries.Count - start);

                for (int i = 0; i < count; i++)
                    this.DrawRow(this._visibleEntries[start + i]);

                EditorGUILayout.EndScrollView();

                this.DrawPager();
            }
        }

        private void DrawRow(AudioEntry entry)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, EntryRowHeight);
            bool isSelected = ReferenceEquals(entry, this._selected);

            if (Event.current.type == EventType.Repaint && isSelected)
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.36f, 0.52f, 0.5f));

            (string glyph, Color tint, string reason) = this.DescribeState(entry);

            Rect glyphRect = new Rect(rect.x + 4f, rect.y + 4f, 20f, 18f);
            Rect nameRect = new Rect(rect.x + 26f, rect.y + 3f, rect.width - 30f, 18f);
            Rect subRect = new Rect(rect.x + 26f, rect.y + 22f, rect.width - 30f, 16f);

            GUI.Label(glyphRect, new GUIContent(glyph, reason));
            GUI.Label(nameRect, string.IsNullOrEmpty(entry.Id) ? "(no id)" : entry.Id, EditorStyles.boldLabel);

            Color previous = GUI.contentColor;
            GUI.contentColor = tint;
            GUI.Label(subRect, reason, EditorStyles.miniLabel);
            GUI.contentColor = previous;

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                this.Select(entry);
                Event.current.Use();
            }
        }

        private void DrawPager()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(this._currentPage <= 0))
                {
                    if (GUILayout.Button("◀ Prev", EditorStyles.miniButtonLeft))
                    {
                        this._currentPage--;
                        this._needsRepaint = true;
                    }
                }

                GUILayout.Label($"Page {this._currentPage + 1} / {this.PageCount}",
                    EditorStyles.centeredGreyMiniLabel);

                using (new EditorGUI.DisabledScope(this._currentPage >= this.PageCount - 1))
                {
                    if (GUILayout.Button("Next ▶", EditorStyles.miniButtonRight))
                    {
                        this._currentPage++;
                        this._needsRepaint = true;
                    }
                }
            }

            GUILayout.Label($"{this._entries.Count} entr(ies), {this.UnregisteredCount()} not in the collection",
                EditorStyles.miniLabel);
        }

        private void DrawSplitter()
        {
            Rect splitter = EditorGUILayout.GetControlRect(false,
                GUILayout.Width(SplitterWidth), GUILayout.ExpandHeight(true));

            EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeHorizontal);

            bool isHovering = splitter.Contains(Event.current.mousePosition);
            if (isHovering != this._isHoveringSplitter)
            {
                this._isHoveringSplitter = isHovering;
                this._needsRepaint = true;
            }

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(splitter, SplitterColor);

                if (this._isHoveringSplitter || this._isDraggingSplitter)
                {
                    Rect accent = new Rect(splitter.x + (splitter.width * 0.5f) - 1f, splitter.y, 2f, splitter.height);
                    EditorGUI.DrawRect(accent, SplitterHoverColor);
                }
            }

            if (Event.current.type == EventType.MouseDown && isHovering)
                this._isDraggingSplitter = true;

            if (Event.current.type == EventType.MouseUp)
                this._isDraggingSplitter = false;

            if (!this._isDraggingSplitter || Event.current.type != EventType.MouseDrag)
                return;

            this._listWidth = Mathf.Clamp(Event.current.mousePosition.x, MinListWidth, MaxListWidth);
            this._needsRepaint = true;
        }

        #endregion

        #region Detail

        private void DrawDetail()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                this._detailScroll = EditorGUILayout.BeginScrollView(this._detailScroll);

                if (this._isCreating)
                    this.DrawCreateForm();
                else if (this._tab == Tab.Channels)
                    this.DrawChannels();
                else
                    this.DrawSelectedEntry();

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSelectedEntry()
        {
            if (this._selected == null)
            {
                EditorGUILayout.HelpBox("Select an entry, or press New Entry.", MessageType.Info);
                return;
            }

            string path = AssetDatabase.GetAssetPath(this._selected);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(this._selected.Id, EditorStyles.boldLabel);

                if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(50f)))
                    EditorGUIUtility.PingObject(this._selected);
            }

            EditorGUILayout.LabelField(path, EditorStyles.miniLabel);

            if (!this._selected.IsPlayable(out string reason))
                EditorGUILayout.HelpBox(reason, MessageType.Warning);

            AudioDatabaseLocator.Result database = AudioDatabaseLocator.Find();
            if (database.Found && !database.Collection.Contains(this._selected))
            {
                EditorGUILayout.HelpBox("This entry is not registered in the AudioCollection, so the game "
                                        + "will not load it. Its identifier is still generated.", MessageType.Warning);

                if (GUILayout.Button("Add to collection"))
                {
                    AudioDatabaseLocator.Register(database.Collection, this._selected, out string error);
                    if (error != null)
                        AudioDialogs.Report("Could not register the entry", error);
                }
            }

            EditorGUILayout.Space(6f);

            // One line renders the whole entry exactly as the Inspector would, ShowIf included.
            this._detailTree ??= PropertyTree.Create(this._selected);
            this._detailTree.Draw(false);
        }

        private void DrawChannels()
        {
            // Ambiguity is checked here rather than trusted to AudioIdIndex, which silently picks
            // whichever FindAssets returns first - fine for a read-only dropdown, not fine for a
            // pane that is about to let someone edit "the" config.
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(AudioConfig)}");

            if (guids.Length > 1)
            {
                EditorGUILayout.HelpBox("More than one AudioConfig exists in the project. Delete or "
                                        + "merge all but one, so it is unambiguous which channels the game loads.",
                    MessageType.Error);
                return;
            }

            AudioConfig config = AudioIdIndex.Config;

            if (config == null)
            {
                this.ReleaseChannelsTree();
                this.DrawCreateConfigPrompt();
                return;
            }

            if (!ReferenceEquals(this._channelsTreeConfig, config))
            {
                this.ReleaseChannelsTree();
                this._channelsTree = PropertyTree.Create(config);
                this._channelsTreeConfig = config;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(config), EditorStyles.miniLabel);

                if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(50f)))
                    EditorGUIUtility.PingObject(config);
            }

            EditorGUILayout.Space(6f);

            // One line renders the whole config exactly as the Inspector would - mixer, the channel
            // list, voice pool, timing, loading and diagnostics all included, so nothing here
            // duplicates a field list AudioConfig already owns.
            this._channelsTree.Draw(false);
        }

        private void DrawCreateConfigPrompt()
        {
            EditorGUILayout.HelpBox("No AudioConfig asset exists yet. Create one so channels can be "
                                    + "authored here.", MessageType.Info);

            if (GUILayout.Button("Create AudioConfig…", GUILayout.Width(160f)))
                this.CreateConfig();
        }

        private void CreateConfig()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Audio Config",
                "AudioConfig",
                "asset",
                "Choose where to store the AudioConfig asset.",
                ResolveDefaultConfigDirectory());

            // A cancel must leave nothing written and raise nothing - the same contract as entry
            // creation.
            if (string.IsNullOrEmpty(path))
                return;

            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                AudioDialogs.Report("Could not create the AudioConfig", $"{path} is outside the Assets folder.");
                return;
            }

            // AssetDatabase.CreateAsset over an existing path deletes and recreates it, issuing a new
            // GUID and breaking every reference to whatever was there before.
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                string unique = AssetDatabase.GenerateUniqueAssetPath(path);

                if (!AudioDialogs.ConfirmSaveAsUnique(path, unique))
                    return;

                path = unique;
            }

            AudioConfig config = ScriptableObject.CreateInstance<AudioConfig>();
            AssetDatabase.CreateAsset(config, path);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            AudioIdIndex.Invalidate();
            this._needsRepaint = true;
        }

        private static string ResolveDefaultConfigDirectory()
        {
            AudioDatabaseLocator.Result database = AudioDatabaseLocator.Find();
            if (database.Found)
                return DirectoryOfAsset(database.AssetPath);

            return "Assets";
        }

        private static string DirectoryOfAsset(string assetPath)
        {
            int end = assetPath.LastIndexOf('/');
            return end <= 0 ? "Assets" : assetPath.Substring(0, end);
        }

        #endregion

        #region Create form

        private void BeginCreate()
        {
            this._isCreating = true;
            this._newId = string.Empty;
            this._newChannelId = AudioIdIndex.ChannelIds.Length > 0 ? AudioIdIndex.ChannelIds[0] : string.Empty;
            this._newClip = null;
            this._newIdFeedback = null;
            this._lastCheckedId = null;
            this._needsRepaint = true;
        }

        private void DrawCreateForm()
        {
            EditorGUILayout.LabelField("New audio entry", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            this._newId = EditorGUILayout.TextField("Identifier", this._newId);

            // Recomputed only when the text changed, so repaint stays free.
            if (EditorGUI.EndChangeCheck() || this._lastCheckedId != this._newId)
                this.RecheckNewId();

            if (!string.IsNullOrEmpty(this._newIdFeedback))
            {
                EditorGUILayout.HelpBox(this._newIdFeedback, this._newIdFeedbackType);

                string suggestion = AudioIdSanitizer.Suggest(this._newId);
                bool canSuggest = this._newIdFeedbackType == MessageType.Error
                                  && !string.IsNullOrEmpty(suggestion)
                                  && suggestion != this._newId
                                  && AudioIdFormat.IsValid(suggestion, out _);

                if (canSuggest && GUILayout.Button($"Use '{suggestion}'"))
                {
                    this._newId = suggestion;
                    this.RecheckNewId();
                }
            }

            string[] channels = AudioIdIndex.ChannelIds;
            if (channels.Length > 0)
            {
                int current = Mathf.Max(0, Array.IndexOf(channels, this._newChannelId));
                int picked = EditorGUILayout.Popup("Channel", current, channels);
                this._newChannelId = channels[picked];
            }
            else
            {
                EditorGUILayout.HelpBox("No channels are declared in the AudioConfig yet.", MessageType.Info);
            }

            this._newClip = (AudioClip)EditorGUILayout.ObjectField("Clip", this._newClip, typeof(AudioClip), false);

            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel"))
                {
                    this._isCreating = false;
                    this._needsRepaint = true;
                }

                using (new EditorGUI.DisabledScope(this._newIdFeedbackType == MessageType.Error
                                                   || string.IsNullOrEmpty(this._newId)))
                {
                    // The ellipsis signals that a file dialog follows.
                    if (GUILayout.Button("Create…"))
                        this.CreateEntry();
                }
            }
        }

        private void RecheckNewId()
        {
            this._lastCheckedId = this._newId;

            if (string.IsNullOrEmpty(this._newId))
            {
                this._newIdFeedback = null;
                this._newIdFeedbackType = MessageType.None;
                return;
            }

            AudioIdSanitizeResult sanitized = AudioIdSanitizer.ToMemberName(this._newId);

            if (sanitized.Status == AudioIdStatus.Rejected)
            {
                this._newIdFeedback = sanitized.Message;
                this._newIdFeedbackType = MessageType.Error;
                return;
            }

            AudioIdConflict? conflict = AudioIdCollisionDetector.CheckAgainst(
                this._newId, AudioIdIndex.Entries, ignoreOwnerPath: null);

            if (conflict.HasValue)
            {
                this._newIdFeedback = conflict.Value.Message;
                this._newIdFeedbackType = MessageType.Error;
                return;
            }

            this._newIdFeedback = sanitized.Status == AudioIdStatus.Adjusted
                ? sanitized.Message
                : $"Available. Generates as AudioId.{sanitized.MemberName}";

            this._newIdFeedbackType = sanitized.Status == AudioIdStatus.Adjusted
                ? MessageType.Warning
                : MessageType.Info;
        }

        private void CreateEntry()
        {
            AudioEntryCreationRequest request = new AudioEntryCreationRequest
            {
                Id = this._newId,
                ChannelId = this._newChannelId,
                DirectClip = this._newClip,
            };

            AudioEntry created = AudioEntryCreationService.CreateInteractive(request, out string error);

            if (created == null)
            {
                // A cancel reports no error and must leave the form exactly as it was, so nothing
                // has to be retyped after a mis-click.
                if (error != null)
                    AudioDialogs.Report("Could not create the entry", error);
                else
                    SessionState.SetString(AudioEditorState.LastStatusKey,
                        "Creation cancelled. Nothing was written.");

                this._needsRepaint = true;
                return;
            }

            if (error != null)
                AudioDialogs.Report("Entry created, but not registered", error);

            this._isCreating = false;
            this.Rescan();
            this.Select(created);
            this.Generate();
        }

        #endregion

        #region Generated code

        private void DrawGeneratedCode()
        {
            this._plan ??= AudioIdGenerationService.BuildPlan();

            EditorGUILayout.LabelField("Target", this._plan.TargetPath);

            if (!this._plan.CanApply)
            {
                EditorGUILayout.HelpBox(this._plan.BlockingError, MessageType.Error);

                IReadOnlyList<AudioIdConflict> conflicts = this._plan.Conflicts;
                for (int i = 1; i < conflicts.Count; i++)
                    EditorGUILayout.HelpBox(conflicts[i].Message, MessageType.Error);

                return;
            }

            if (this._plan.IsNoOp)
                EditorGUILayout.HelpBox("The file on disk already matches. Generating would write nothing.",
                    MessageType.Info);

            if (this._plan.AddedIds.Count > 0)
                EditorGUILayout.HelpBox("Adding: " + string.Join(", ", this._plan.AddedIds), MessageType.Info);

            if (this._plan.RemovedIds.Count > 0)
                EditorGUILayout.HelpBox("Removing: " + string.Join(", ", this._plan.RemovedIds)
                                                     + "\nCode using these will stop compiling until it is updated.",
                    MessageType.Warning);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            this._codeScroll = EditorGUILayout.BeginScrollView(this._codeScroll);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextArea(this._plan.RenderedText ?? string.Empty, GUILayout.ExpandHeight(true));

            EditorGUILayout.EndScrollView();
        }

        private void Generate()
        {
            AudioIdIndex.Invalidate();
            this._plan = AudioIdGenerationService.BuildPlan();

            if (!this._plan.CanApply)
            {
                AudioDialogs.Report("Cannot generate audio ids", this._plan.BlockingError);
                return;
            }

            if (!this._plan.IsNoOp && !AudioDialogs.ConfirmGenerate(this._plan))
                return;

            if (!AudioIdGenerationService.Apply(this._plan, out string error))
                AudioDialogs.Report("Cannot generate audio ids", error);

            this._plan = null;
            this._needsRepaint = true;
        }

        #endregion

        #region Commands and state

        private void DeleteSelected()
        {
            if (this._selected == null)
                return;

            AudioEntryDeletionService.DeleteInteractive(new[] { this._selected }, out string error);

            if (error != null)
                AudioDialogs.Report("Could not delete the entry", error);

            this._selected = null;
            this.ReleaseDetailTree();
            this.Rescan();
        }

        private void Rescan()
        {
            AudioIdIndex.Invalidate();

            this._entries.Clear();
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(AudioEntry)}");

            for (int i = 0; i < guids.Length; i++)
            {
                AudioEntry entry = AssetDatabase.LoadAssetAtPath<AudioEntry>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (entry != null)
                    this._entries.Add(entry);
            }

            this._entries.Sort(static (left, right) => string.CompareOrdinal(left.Id, right.Id));

            this.RebuildVisible();
            this._plan = null;
            this._needsRepaint = true;
        }

        private void RebuildVisible()
        {
            this._visibleEntries.Clear();

            for (int i = 0; i < this._entries.Count; i++)
            {
                if (string.IsNullOrEmpty(this._search)
                    || (this._entries[i].Id?.IndexOf(this._search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0)
                    this._visibleEntries.Add(this._entries[i]);
            }

            this._currentPage = Mathf.Clamp(this._currentPage, 0, this.PageCount - 1);
        }

        private void Select(AudioEntry entry)
        {
            this._selected = entry;
            this._isCreating = false;
            this.ReleaseDetailTree();

            // Stored as a GUID, not a path, so the selection survives the asset being moved.
            string path = AssetDatabase.GetAssetPath(entry);
            EditorPrefs.SetString(AudioEditorState.SelectedGuidKey, AssetDatabase.AssetPathToGUID(path));

            this._needsRepaint = true;
        }

        private void RestoreSelection()
        {
            string guid = EditorPrefs.GetString(AudioEditorState.SelectedGuidKey, null);
            if (string.IsNullOrEmpty(guid))
                return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                this._selected = AssetDatabase.LoadAssetAtPath<AudioEntry>(path);
        }

        private void ReleaseDetailTree()
        {
            this._detailTree?.Dispose();
            this._detailTree = null;
        }

        private void ReleaseChannelsTree()
        {
            this._channelsTree?.Dispose();
            this._channelsTree = null;
            this._channelsTreeConfig = null;
        }

        private int UnregisteredCount()
        {
            AudioDatabaseLocator.Result database = AudioDatabaseLocator.Find();
            if (!database.Found)
                return this._entries.Count;

            int count = 0;
            for (int i = 0; i < this._entries.Count; i++)
            {
                if (!database.Collection.Contains(this._entries[i]))
                    count++;
            }

            return count;
        }

        private (string glyph, Color tint, string reason) DescribeState(AudioEntry entry)
        {
            AudioIdSanitizeResult sanitized = AudioIdSanitizer.ToMemberName(entry.Id);
            if (sanitized.Status == AudioIdStatus.Rejected)
                return ("🔴", BadColor, sanitized.Message);

            IReadOnlyList<AudioIdConflict> conflicts = AudioIdIndex.Conflicts;
            for (int i = 0; i < conflicts.Count; i++)
            {
                for (int owner = 0; owner < conflicts[i].OwnerPaths.Count; owner++)
                {
                    if (conflicts[i].OwnerPaths[owner] == AssetDatabase.GetAssetPath(entry))
                        return ("🔴", BadColor, conflicts[i].Message);
                }
            }

            if (!entry.IsPlayable(out string reason))
                return ("🟡", WarnColor, reason);

            return ("🟢", GoodColor, entry.ChannelId ?? "no channel");
        }

        private void DrawStatusBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label($"Ids generate to {AudioIdGenerationService.ResolveTargetPath()}",
                    EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                // SessionState, not EditorPrefs: this survives the domain reload that generation
                // causes, and dies with the Editor, which is exactly the right lifetime.
                GUILayout.Label(SessionState.GetString(AudioEditorState.LastStatusKey, string.Empty),
                    EditorStyles.miniLabel);
            }
        }

        #endregion
    }
}