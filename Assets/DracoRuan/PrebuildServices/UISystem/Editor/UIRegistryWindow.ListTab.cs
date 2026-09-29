using System.Collections.Generic;
using System.Linq;
using DracoRuan.PrebuildServices.UISystem.Data;
using DracoRuan.PrebuildServices.UISystem.Editor.Registry;
using DracoRuan.PrebuildServices.UISystem.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Editor
{
    /// <summary>Registry tab: filterable, paged list on the left; buffered editor for the
    /// selected definition on the right (changes reach the asset only through Apply).</summary>
    public sealed partial class UIRegistryWindow
    {
        private const int PageSize = 20;
        private const float LeftWidth = 300f;
        private const float RowHeight = 36f;

        private static readonly Color SelectedColor = new(0.24f, 0.48f, 0.9f, 0.45f);

        private string[] _filterOptions = { "All", "Unregistered" };
        private int _filterIndex;
        private string _search = string.Empty;
        private string _searchLower = string.Empty;
        private List<RowInfo> _filtered = new();
        private int _page;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;

        private UIViewDefinition _selected;
        private SerializedObject _selectedSerialized;
        private bool _hasPending;

        private UIViewDefinition _requestedSelection;
        private bool _hasRequestedSelection;

        // ---------------------------------------------------------------
        // Filter / paging state
        // ---------------------------------------------------------------

        private void RebuildFilterOptions()
        {
            this._filterOptions = new[] { "All", "Unregistered" }.Concat(this._collectionNames).ToArray();
            if (this._filterIndex >= this._filterOptions.Length)
                this._filterIndex = 0;
        }

        private bool PassesFilter(RowInfo row)
        {
            if (this._filterIndex == 1 && row.Owners.Count > 0)
                return false;

            if (this._filterIndex >= 2 && !row.Owners.Contains(this._collections[this._filterIndex - 2]))
                return false;

            return this._searchLower.Length == 0 || row.SearchKey.Contains(this._searchLower);
        }

        private void RebuildFiltered()
        {
            this._searchLower = this._search.ToLowerInvariant();
            this._filtered = this._rows.Where(this.PassesFilter).ToList();
            this._page = Mathf.Clamp(this._page, 0, this.PageCount() - 1);
        }

        private int PageCount() => Mathf.Max(1, Mathf.CeilToInt(this._filtered.Count / (float)PageSize));

        private void GoToPageOf(UIViewDefinition definition)
        {
            int index = this._filtered.FindIndex(r => r.Definition == definition);
            if (index >= 0)
                this._page = index / PageSize;
        }

        // ---------------------------------------------------------------
        // Selection and buffered edits
        // ---------------------------------------------------------------

        /// <summary>Selection changes are queued from the row click and applied at the end of
        /// OnGUI, so the layout of the current event is never altered halfway through.</summary>
        private void RequestSelection(UIViewDefinition definition)
        {
            this._requestedSelection = definition;
            this._hasRequestedSelection = true;
        }

        private void ProcessRequestedSelection()
        {
            if (!this._hasRequestedSelection)
                return;

            this._hasRequestedSelection = false;
            this.SelectDefinition(this._requestedSelection);
            this._requestedSelection = null;
            this.Repaint();
            GUIUtility.ExitGUI();
        }

        /// <summary>False if the user cancelled out of unapplied edits on the current selection.</summary>
        private bool SelectDefinition(UIViewDefinition definition)
        {
            if (definition == this._selected)
                return true;

            if (!this.ResolvePending())
                return false;

            this._selected = definition;
            this._selectedSerialized = definition != null ? new SerializedObject(definition) : null;
            this._hasPending = false;
            this._detailScroll = Vector2.zero;
            return true;
        }

        private void ClearSelection()
        {
            this._selected = null;
            this._selectedSerialized = null;
            this._hasPending = false;
        }

        /// <summary>Asks what to do with buffered edits before something would lose them.
        /// False = the user cancelled, so the caller must not proceed.</summary>
        private bool ResolvePending()
        {
            if (!this._hasPending || this._selected == null)
            {
                this._hasPending = false;
                return true;
            }

            // DisplayDialogComplex: 0 = ok, 1 = cancel, 2 = alt.
            int choice = EditorUtility.DisplayDialogComplex("Unapplied changes",
                $"'{this._selected.name}' has changes that have not been applied.", "Apply", "Cancel", "Discard");

            switch (choice)
            {
                case 0:
                    this.ApplyPending();
                    return true;
                case 2:
                    this.DiscardPending();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Writes the buffer into the selected asset and saves it to disk.</summary>
        private void ApplyPending()
        {
            if (this._selected == null || this._selectedSerialized == null)
                return;

            this._selectedSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(this._selected);
            AssetDatabase.SaveAssetIfDirty(this._selected);
            this._hasPending = false;

            // A changed view model or prefab can create/resolve duplicates elsewhere, so
            // rows and findings are rebuilt in full rather than just for this asset.
            this.RebuildRows();
            this.RebuildFiltered();
        }

        private void DiscardPending()
        {
            this._selectedSerialized?.Update();
            this._hasPending = false;
        }

        // ---------------------------------------------------------------
        // Tab
        // ---------------------------------------------------------------

        private void DrawRegistryTab()
        {
            this.DrawRegistryToolbar();

            using (new EditorGUILayout.HorizontalScope())
            {
                this.DrawLeftColumn();
                this.DrawRightColumn();
            }
        }

        private void DrawRegistryToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                this._filterIndex = EditorGUILayout.Popup(this._filterIndex, this._filterOptions,
                    EditorStyles.toolbarPopup, GUILayout.Width(160));
                this._search = EditorGUILayout.TextField(this._search, EditorStyles.toolbarSearchField,
                    GUILayout.Width(200));
                if (EditorGUI.EndChangeCheck())
                {
                    this._page = 0;
                    this._listScroll = Vector2.zero;
                    this.RebuildFiltered();

                    // A selection the new filter hides is dropped (asking first if it has edits).
                    if (this._selected != null && this._filtered.All(r => r.Definition != this._selected))
                        this.RequestSelection(null);
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label(this._summary, EditorStyles.miniLabel);
            }
        }

        private void DrawLeftColumn()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(LeftWidth)))
            {
                this._listScroll = EditorGUILayout.BeginScrollView(this._listScroll);

                int start = this._page * PageSize;
                int end = Mathf.Min(start + PageSize, this._filtered.Count);
                for (int i = start; i < end; i++)
                    this.DrawRow(this._filtered[i]);

                if (this._filtered.Count == 0)
                    EditorGUILayout.HelpBox("No definitions match.", MessageType.Info);

                EditorGUILayout.EndScrollView();

                this.DrawPagingBar();
            }
        }

        /// <summary>Fixed-height rect drawn by hand: no nested layout groups per row, which is
        /// what keeps a page of rows cheap to lay out and repaint.</summary>
        private void DrawRow(RowInfo row)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
            bool selected = row.Definition == this._selected;

            if (Event.current.type == EventType.Repaint && selected)
                EditorGUI.DrawRect(rect, SelectedColor);

            string title = selected && this._hasPending ? row.Name + " *" : row.Name;
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 30f, 18f), title, _nameStyle);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 19f, rect.width - 12f, 16f), row.VmName, _vmStyle);

            if (row.Errors.Count > 0)
                GUI.Label(new Rect(rect.xMax - 26f, rect.y + 2f, 20f, 18f), "●", _badgeStyle);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                this.RequestSelection(row.Definition);
                e.Use();
            }
        }

        private void DrawPagingBar()
        {
            int pageCount = this.PageCount();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(this._page <= 0))
                {
                    if (GUILayout.Button("< Prev", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    {
                        this._page--;
                        this._listScroll = Vector2.zero;
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label($"Page {this._page + 1}/{pageCount}  ({this._filtered.Count})", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(this._page >= pageCount - 1))
                {
                    if (GUILayout.Button("Next >", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    {
                        this._page++;
                        this._listScroll = Vector2.zero;
                    }
                }
            }
        }

        private void DrawRightColumn()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                if (this._selected == null || this._selectedSerialized == null ||
                    !this._rowByDefinition.TryGetValue(this._selected, out RowInfo row))
                {
                    EditorGUILayout.HelpBox("Select a definition from the list.", MessageType.Info);
                    return;
                }

                this.DrawDetailHeader(row);

                this._detailScroll = EditorGUILayout.BeginScrollView(this._detailScroll);

                foreach (UIValidationFinding finding in row.Errors)
                    EditorGUILayout.HelpBox(finding.Message, MessageType.Error);

                if (this.DrawFields(this._selectedSerialized))
                    this._hasPending = true;

                if (GUILayout.Button("Apply preset defaults", GUILayout.Width(160)))
                {
                    ApplyPresetDefaults(this._selectedSerialized);
                    this._hasPending = true;
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDetailHeader(RowInfo row)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(this._hasPending ? row.Name + " *" : row.Name, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (row.Owners.Count == 0 && this._targetCollection != null &&
                    GUILayout.Button($"Add to '{this._targetCollection.name}'", EditorStyles.toolbarButton))
                {
                    UIRegistryAssetOps.AddToCollection(this._targetCollection, row.Definition);
                    this.RebuildRows();
                    this.RebuildFiltered();
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Ping", EditorStyles.toolbarButton))
                    EditorGUIUtility.PingObject(row.Definition);

                using (new EditorGUI.DisabledScope(!this._hasPending))
                {
                    if (GUILayout.Button("Revert", EditorStyles.toolbarButton))
                        this.DiscardPending();

                    if (GUILayout.Button("Apply", EditorStyles.toolbarButton))
                    {
                        this.ApplyPending();
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button("Delete", EditorStyles.toolbarButton))
                    this.ConfirmDelete(row.Definition);
            }
        }

        private void ConfirmDelete(UIViewDefinition definition)
        {
            bool confirmed = EditorUtility.DisplayDialog("Delete UIViewDefinition",
                $"Delete '{definition.name}' and unregister it from every collection?\nThis cannot be undone.",
                "Delete", "Cancel");
            if (!confirmed)
                return;

            this.ClearSelection();
            UIRegistryAssetOps.Delete(definition, this._collections);
            this.RefreshData();
            GUIUtility.ExitGUI();
        }
    }
}
