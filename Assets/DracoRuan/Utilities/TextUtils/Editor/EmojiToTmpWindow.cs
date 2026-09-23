using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TextCore;

namespace DracoRuan.Utilities.TextUtils.Editor
{
    public class EmojiToTmpWindow : EditorWindow
    {
        private const string EmojiOneAssetPath = "Assets/TextMesh Pro/Resources/Sprite Assets/EmojiOne.asset";

        private const string TwemojiUrlFormat =
            "https://cdn.jsdelivr.net/gh/jdecked/twemoji@latest/assets/72x72/{0}.png";

        private string _input = "";
        private int _codepoint;
        private bool _hasValidInput;

        private TMP_SpriteAsset _foundAsset;
        private int _foundSpriteIndex = -1;

        private TMP_SpriteAsset _emojiOneAsset;
        private TMP_SpriteAsset _extrasAsset;

        private string _statusMessage;
        private MessageType _statusType = MessageType.None;

        [MenuItem("Tools/DracoRuan/TextMeshPro/Emoji to TMP Tag")]
        private static void Open()
        {
            var window = GetWindow<EmojiToTmpWindow>("Emoji to TMP");
            window.minSize = new Vector2(360, 260);
        }

        private void OnEnable()
        {
            _emojiOneAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(EmojiOneAssetPath);
            _extrasAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(
                "Assets/DracoRuan/Utilities/TextUtils/Editor/Generated/EmojiOneExtras.asset");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Emoji / Unicode / Hex", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _input = EditorGUILayout.TextField(_input);
            if (EditorGUI.EndChangeCheck())
                RefreshLookup();

            EditorGUILayout.Space(8);

            if (!_hasValidInput)
            {
                EditorGUILayout.HelpBox("Enter an emoji, hex code (U+1F60A) or decimal code.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Codepoint: U+{EmojiCodepointParser.ToHexName(_codepoint).ToUpperInvariant()}");

            if (_foundSpriteIndex >= 0)
            {
                DrawFoundResult();
            }
            else
            {
                DrawNotFoundResult();
            }

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
            }
        }

        private void DrawFoundResult()
        {
            TMP_SpriteCharacter character = _foundAsset.spriteCharacterTable[_foundSpriteIndex];
            string tag = $"<sprite name=\"{character.name}\">";

            DrawPreview(_foundAsset, character);

            EditorGUILayout.Space(4);
            EditorGUILayout.TextField("TMP Tag", tag);

            if (GUILayout.Button("Copy Tag"))
                EditorGUIUtility.systemCopyBuffer = tag;
        }

        private void DrawPreview(TMP_SpriteAsset asset, TMP_SpriteCharacter character)
        {
            TMP_SpriteGlyph glyph = asset.spriteGlyphTable[(int)character.glyphIndex];
            var atlas = asset.spriteSheet as Texture2D;
            if (atlas == null)
                return;

            Rect previewRect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64));
            GlyphRect r = glyph.glyphRect;
            Rect texCoords = new Rect(
                (float)r.x / atlas.width,
                (float)r.y / atlas.height,
                (float)r.width / atlas.width,
                (float)r.height / atlas.height);

            GUI.DrawTextureWithTexCoords(previewRect, atlas, texCoords);
        }

        private void DrawNotFoundResult()
        {
            EditorGUILayout.HelpBox(
                $"Không có glyph cho U+{EmojiCodepointParser.ToHexName(_codepoint).ToUpperInvariant()} trong EmojiOne hoặc EmojiOneExtras.",
                MessageType.Warning);

            if (GUILayout.Button("Download from Twemoji"))
                DownloadFromTwemoji();

            EditorGUILayout.Space(4);
            DrawDragAndDropArea();
        }

        private void DrawDragAndDropArea()
        {
            Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Kéo thả file ảnh (PNG) vào đây để thêm emoji");

            Event evt = Event.current;
            if (!dropArea.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (string path in DragAndDrop.paths)
                {
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null)
                    {
                        AddEmojiToExtras(texture);
                        break;
                    }
                }

                evt.Use();
            }
        }

        private async void DownloadFromTwemoji()
        {
            string hex = EmojiCodepointParser.ToHexName(_codepoint);
            string url = string.Format(TwemojiUrlFormat, hex);

            using var request = UnityWebRequestTexture.GetTexture(url);
            var operation = request.SendWebRequest();

            while (!operation.isDone)
                await System.Threading.Tasks.Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                _statusMessage = $"Tải thất bại từ {url}: {request.error}";
                _statusType = MessageType.Error;
                Repaint();
                return;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            AddEmojiToExtras(texture);
        }

        private void AddEmojiToExtras(Texture2D glyphImage)
        {
            _extrasAsset = EmojiSpriteAssetBuilder.GetOrCreateExtrasAsset();
            string name = EmojiCodepointParser.ToHexName(_codepoint);

            EmojiSpriteAssetBuilder.AddEmoji(_extrasAsset, _codepoint, name, glyphImage);

            _statusMessage = $"Đã thêm U+{name.ToUpperInvariant()} vào EmojiOneExtras.";
            _statusType = MessageType.Info;

            RefreshLookup();
            Repaint();
        }

        private void RefreshLookup()
        {
            _statusMessage = null;
            _hasValidInput = EmojiCodepointParser.TryParse(_input, out _codepoint);
            _foundAsset = null;
            _foundSpriteIndex = -1;

            if (!_hasValidInput)
                return;

            if (_emojiOneAsset != null)
            {
                int index = _emojiOneAsset.GetSpriteIndexFromUnicode((uint)_codepoint);
                if (index >= 0)
                {
                    _foundAsset = _emojiOneAsset;
                    _foundSpriteIndex = index;
                    return;
                }
            }

            if (_extrasAsset != null)
            {
                int index = _extrasAsset.GetSpriteIndexFromUnicode((uint)_codepoint);
                if (index >= 0)
                {
                    _foundAsset = _extrasAsset;
                    _foundSpriteIndex = index;
                }
            }
        }
    }
}