using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace DracoRuan.Utilities.TextUtils.Editor
{
    public static class EmojiSpriteAssetBuilder
    {
        private const string GeneratedFolder = "Assets/DracoRuan/Utilities/TextUtils/Editor/Generated";
        private const string ExtrasAssetPath = GeneratedFolder + "/EmojiOneExtras.asset";
        private const string ExtrasTexturePath = GeneratedFolder + "/EmojiOneExtras.png";

        private const int CellSize = 128;
        private const int InitialAtlasSize = 1024;

        public static TMP_SpriteAsset GetOrCreateExtrasAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(ExtrasAssetPath);
            if (existing != null)
                return existing;

            EnsureFolderExists();

            var atlas = CreateAtlasTexture(InitialAtlasSize);
            SaveAtlasTexture(atlas);

            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "EmojiOneExtras";
            asset.spriteCharacterTable.Clear();
            asset.spriteGlyphTable.Clear();
            AssetDatabase.CreateAsset(asset, ExtrasAssetPath);

            AssignAtlasAndMaterial(asset, ExtrasTexturePath);

            AssetDatabase.SaveAssets();
            return asset;
        }

        public static void AddEmoji(TMP_SpriteAsset asset, int codepoint, string name, Texture2D glyphImage)
        {
            var atlas = LoadWritableAtlas(asset);
            var cellIndex = asset.spriteGlyphTable.Count;
            var atlasSize = atlas.width;
            var cellsPerRow = atlasSize / CellSize;

            if (cellIndex >= cellsPerRow * cellsPerRow)
            {
                atlas = GrowAtlas(atlas);
                atlasSize = atlas.width;
                cellsPerRow = atlasSize / CellSize;
            }

            var column = cellIndex % cellsPerRow;
            var row = cellIndex / cellsPerRow;
            var pixelX = column * CellSize;
            var pixelY = atlasSize - CellSize - row * CellSize;

            var resized = ResizeToCell(glyphImage);
            atlas.SetPixels(pixelX, pixelY, CellSize, CellSize, resized.GetPixels());
            atlas.Apply();
            Object.DestroyImmediate(resized);

            SaveAtlasTexture(atlas);
            AssignAtlasAndMaterial(asset, AssetDatabase.GetAssetPath(atlas));

            var metrics = new GlyphMetrics(CellSize, CellSize, 0, CellSize * 0.9f, CellSize);
            var glyphRect = new GlyphRect(pixelX, pixelY, CellSize, CellSize);
            var glyph = new TMP_SpriteGlyph((uint)cellIndex, metrics, glyphRect, 1f, 0);
            var character = new TMP_SpriteCharacter((uint)codepoint, glyph) { name = name };

            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(character);
            asset.UpdateLookupTables();

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolderExists()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/DracoRuan/Utilities/TextUtils/Editor", "Generated");
        }

        private static Texture2D CreateAtlasTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clearPixels = new Color32[size * size];
            texture.SetPixels32(clearPixels);
            texture.Apply();
            return texture;
        }

        private static void SaveAtlasTexture(Texture2D atlas)
        {
            var png = atlas.EncodeToPNG();
            File.WriteAllBytes(ExtrasTexturePath, png);
            AssetDatabase.ImportAsset(ExtrasTexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(ExtrasTexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static Texture2D LoadWritableAtlas(TMP_SpriteAsset asset)
        {
            var path = AssetDatabase.GetAssetPath(asset.spriteSheet);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return texture;
        }

        private static Texture2D GrowAtlas(Texture2D atlas)
        {
            var newSize = atlas.width * 2;
            var grown = CreateAtlasTexture(newSize);

            grown.SetPixels(0, newSize - atlas.height, atlas.width, atlas.height, atlas.GetPixels());
            grown.Apply();
            return grown;
        }

        private static Texture2D ResizeToCell(Texture2D source)
        {
            if (source.width == CellSize && source.height == CellSize)
                return source;

            var rt = RenderTexture.GetTemporary(CellSize, CellSize);
            Graphics.Blit(source, rt);

            var previous = RenderTexture.active;
            RenderTexture.active = rt;

            var resized = new Texture2D(CellSize, CellSize, TextureFormat.RGBA32, false);
            resized.ReadPixels(new Rect(0, 0, CellSize, CellSize), 0, 0);
            resized.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return resized;
        }

        private static void AssignAtlasAndMaterial(TMP_SpriteAsset asset, string atlasPath)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            asset.spriteSheet = atlas;

            if (asset.material == null)
            {
                var shader = Shader.Find("TextMeshPro/Sprite");
                var material = new Material(shader) { name = asset.name };
                material.SetTexture("_MainTex", atlas);
                AssetDatabase.AddObjectToAsset(material, asset);
                asset.material = material;
            }
            else
            {
                asset.material.SetTexture("_MainTex", atlas);
            }
        }
    }
}