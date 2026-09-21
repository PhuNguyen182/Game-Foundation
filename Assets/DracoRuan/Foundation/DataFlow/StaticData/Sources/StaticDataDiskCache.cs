using System;
using System.IO;
using System.Text;
using DracoRuan.Foundation.DataFlow.Core.Envelope;
using DracoRuan.Foundation.DataFlow.Core.Storage;
using UnityEngine;

namespace DracoRuan.Foundation.DataFlow.StaticData.Sources
{
    /// <summary>
    /// On-disk copy of the last payload a network source downloaded, with its ETag.
    /// </summary>
    /// <remarks>
    /// <para><b>Without this, a CDN source is an online-only source.</b> A player who opens the game
    /// on a train gets no config table at all, and the fallback chain drops to whatever shipped in
    /// the build — silently reverting every liveops change. The cache turns "no network" into "last
    /// known good" instead.</para>
    ///
    /// <para>Writes go through <see cref="AtomicFileStore"/>, so a kill mid-write leaves the previous
    /// entry intact rather than a truncated file that reads back as a valid-but-wrong table.</para>
    ///
    /// <para>Layout is one line of ETag followed by the body. Keeping both in a single file means
    /// there is no window where the body has been replaced but the ETag still points at the old one,
    /// which would make the next <c>If-None-Match</c> ask for a version the cache no longer holds.</para>
    /// </remarks>
    public sealed class StaticDataDiskCache
    {
        private const string LogTag = "StaticData/Cache";
        private const string FileExtension = ".cache";

        private readonly string _rootDirectory;
        private readonly AtomicFileStore _store = new();

        public StaticDataDiskCache(string rootDirectory)
        {
            this._rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        }

        /// <summary>Default location: a folder beside the save files.</summary>
        public static StaticDataDiskCache CreateDefault() =>
            new(Path.Combine(Application.persistentDataPath, "StaticDataCache"));

        public bool TryRead(string key, out string etag, out string body)
        {
            etag = null;
            body = null;

            try
            {
                string path = this.GetPath(key);
                if (!File.Exists(path))
                    return false;

                byte[] content = this._store.Read(path, out _);
                if (content == null || content.Length == 0)
                    return false;

                Split(Encoding.UTF8.GetString(content), out etag, out body);
                return body != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{LogTag}] Could not read cache for '{key}': {exception.Message}");
                return false;
            }
        }

        public void Write(string key, string etag, string body)
        {
            if (body == null)
                return;

            try
            {
                Directory.CreateDirectory(this._rootDirectory);

                string content = $"{etag ?? string.Empty}\n{body}";
                this._store.Write(this.GetPath(key), Encoding.UTF8.GetBytes(content));
            }
            catch (Exception exception)
            {
                // A cache that cannot be written is a performance problem, never a correctness one:
                // the payload already downloaded is returned regardless.
                Debug.LogWarning($"[{LogTag}] Could not cache '{key}': {exception.Message}");
            }
        }

        private static void Split(string content, out string etag, out string body)
        {
            int newLineIndex = content.IndexOf('\n');
            if (newLineIndex < 0)
            {
                etag = null;
                body = content;
                return;
            }

            etag = content[..newLineIndex];
            body = content[(newLineIndex + 1)..];

            if (etag.Length == 0)
                etag = null;
        }

        /// <summary>
        /// Hashes the key rather than sanitising it: a URL contains characters no file system accepts,
        /// and any escaping scheme eventually collides or exceeds the path length limit.
        /// </summary>
        private string GetPath(string key)
        {
            ulong hash = Fnv1A.Hash(Encoding.UTF8.GetBytes(key));
            return Path.Combine(this._rootDirectory, hash.ToString("x16") + FileExtension);
        }
    }
}
