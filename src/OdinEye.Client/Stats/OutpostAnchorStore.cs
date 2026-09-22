namespace OdinEye.Client.Stats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Utf8Json;

    // Gilligan's Island: the small, LOCAL-only list of world positions
    // (one per landmass already credited) OutpostTracking uses to decide
    // whether a newly-qualifying outpost is on a landmass this character
    // has already been credited for. One JSON file per character, same
    // shape and atomic-write convention as CustomCounterStore, but never
    // synced to or seeded from the server: it is scratch state for a
    // client-only heuristic, not a stat, and the server has no way to
    // verify it anyway (WorldGenerator only exists in the running game).
    //
    // Storing only ONE anchor per landmass (not one entry per outpost)
    // is what makes this safe against re-counting: once a landmass has an
    // anchor, ANY later bed found on that same landmass -- including the
    // very same bed on a later scan -- samples as "not ocean-separated"
    // from that anchor and is correctly never credited again, with no
    // need to separately track which individual beds were already seen.
    public sealed class OutpostAnchorStore
    {
        private readonly object gate = new object();
        private readonly string path;
        private readonly Action<string> logWarning;
        private readonly List<float[]> anchors = new List<float[]>(); // each [x, z]
        private bool dirty;

        public OutpostAnchorStore(string path, Action<string> logWarning = null)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            this.logWarning = logWarning ?? (_ => { });
            Load();
        }

        public IReadOnlyList<(float X, float Z)> Anchors
        {
            get
            {
                lock (gate)
                {
                    var copy = new (float X, float Z)[anchors.Count];
                    for (var i = 0; i < anchors.Count; i++)
                    {
                        copy[i] = (anchors[i][0], anchors[i][1]);
                    }

                    return copy;
                }
            }
        }

        public void Add(float x, float z)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z))
            {
                return;
            }

            lock (gate)
            {
                anchors.Add(new[] { x, z });
                dirty = true;
            }
        }

        // Atomic: write a temp file, then swap it in. Same shape as
        // CustomCounterStore.Flush() -- see there for why.
        public bool Flush()
        {
            byte[] json;
            lock (gate)
            {
                if (!dirty)
                {
                    return true;
                }

                json = JsonSerializer.Serialize(anchors);
            }

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var temp = path + ".tmp";
                File.WriteAllBytes(temp, json);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temp, path);
                lock (gate)
                {
                    dirty = false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logWarning($"OdinEye client: could not save outpost anchors to '{path}': {ex.Message}");
                return false;
            }
        }

        private void Load()
        {
            var temp = path + ".tmp";
            try
            {
                if (!File.Exists(path) && File.Exists(temp))
                {
                    File.Move(temp, path);
                }

                if (!File.Exists(path))
                {
                    return;
                }

                var loaded = JsonSerializer.Deserialize<List<float[]>>(File.ReadAllBytes(path));
                if (loaded == null)
                {
                    throw new InvalidDataException("outpost anchor file was empty");
                }

                foreach (var a in loaded)
                {
                    if (a != null && a.Length == 2 && !float.IsNaN(a[0]) && !float.IsNaN(a[1]))
                    {
                        anchors.Add(a);
                    }
                }
            }
            catch (Exception ex)
            {
                // Corrupt or unreadable: start empty rather than crash. The
                // worst case is re-crediting a landmass already found once
                // before -- an over-count, not a silent loss -- which is
                // why this store's own state is never trusted as the sole
                // record: Custom:OutpostLandmasses (the real counter) is
                // seeded from and validated by the server like any other.
                logWarning($"OdinEye client: outpost anchor file '{path}' was unreadable ({ex.Message}); starting fresh");
                try
                {
                    var bad = path + ".bad";
                    if (File.Exists(bad))
                    {
                        File.Delete(bad);
                    }

                    File.Move(path, bad);
                }
                catch
                {
                    // best effort only
                }

                anchors.Clear();
            }
        }
    }
}
