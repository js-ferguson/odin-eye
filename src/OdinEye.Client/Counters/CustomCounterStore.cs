namespace OdinEye.Client.Counters
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Utf8Json;

    // ODINEYE-36: counters Valheim does not track (arrow hits, bread taken out
    // of the oven, ...) and high-water marks for derived totals. One small
    // JSON file per character, so two characters on one PC never mix.
    //
    // The values ride the EXISTING stats submission under a "Custom:" /
    // "Derived:" key prefix, so nothing on the server or agent changed for
    // them. The server rejects a WHOLE submission if any value is lower than
    // the one it already holds (CharacterStatsController.IsValidStatValue),
    // so this store must never send a smaller number than the server has --
    // e.g. after a reinstall lost this file. SeedFrom() is how it learns what
    // the server already knows.
    public sealed class CustomCounterStore
    {
        public static readonly string[] SeedablePrefixes = { "Custom:", "Derived:" };

        private readonly object gate = new object();
        private readonly string path;
        private readonly Action<string> logWarning;
        private readonly Dictionary<string, float> values = new Dictionary<string, float>();
        private bool dirty;

        public CustomCounterStore(string path, Action<string> logWarning = null)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
            this.logWarning = logWarning ?? (_ => { });
            Load();
        }

        public bool IsDirty
        {
            get { lock (gate) { return dirty; } }
        }

        public void Increment(string key, float amount = 1f)
        {
            if (string.IsNullOrEmpty(key) || float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f)
            {
                return;
            }

            lock (gate)
            {
                values.TryGetValue(key, out var current);
                values[key] = current + amount;
                dirty = true;
            }
        }

        public float Get(string key)
        {
            lock (gate)
            {
                return values.TryGetValue(key, out var v) ? v : 0f;
            }
        }

        public IReadOnlyDictionary<string, float> Snapshot()
        {
            lock (gate)
            {
                return new Dictionary<string, float>(values);
            }
        }

        // Stores max(existing, value) and returns what is stored: a derived
        // total that is recomputed from game data (and so could shrink if the
        // set it sums over shrinks) can never be sent lower than before.
        public float RaiseTo(string key, float value)
        {
            if (string.IsNullOrEmpty(key) || float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return Get(key);
            }

            lock (gate)
            {
                values.TryGetValue(key, out var current);
                if (value > current || !values.ContainsKey(key))
                {
                    values[key] = Math.Max(value, current);
                    dirty = true;
                }

                return values[key];
            }
        }

        // max(local, server) for OUR OWN key families only -- never adopt an
        // arbitrary server key into a file we own.
        public void SeedFrom(IEnumerable<KeyValuePair<string, float>> serverValues)
        {
            if (serverValues == null)
            {
                return;
            }

            lock (gate)
            {
                foreach (var kv in serverValues)
                {
                    if (!SeedablePrefixes.Any(p => kv.Key != null && kv.Key.StartsWith(p, StringComparison.Ordinal)) ||
                        float.IsNaN(kv.Value) || float.IsInfinity(kv.Value) || kv.Value < 0f)
                    {
                        continue;
                    }

                    values.TryGetValue(kv.Key, out var local);
                    if (kv.Value > local)
                    {
                        values[kv.Key] = kv.Value;
                        dirty = true;
                    }
                }
            }
        }

        // Atomic: write a temp file, then swap it in. Returns whether the
        // data is now on disk (true also when there was nothing to write). A
        // failure keeps the store dirty so the next flush retries; it never
        // throws into the game.
        public bool Flush()
        {
            byte[] json;
            lock (gate)
            {
                if (!dirty)
                {
                    return true;
                }

                json = JsonSerializer.Serialize(values);
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
                logWarning($"OdinEye client: could not save counters to '{path}': {ex.Message}");
                return false;
            }
        }

        private void Load()
        {
            var temp = path + ".tmp";
            try
            {
                // A crash between deleting the old file and moving the new
                // one in leaves only the temp file: adopt it.
                if (!File.Exists(path) && File.Exists(temp))
                {
                    File.Move(temp, path);
                }

                if (!File.Exists(path))
                {
                    return;
                }

                var loaded = JsonSerializer.Deserialize<Dictionary<string, float>>(File.ReadAllBytes(path));
                if (loaded == null)
                {
                    throw new InvalidDataException("counter file was empty");
                }

                foreach (var kv in loaded)
                {
                    if (!float.IsNaN(kv.Value) && !float.IsInfinity(kv.Value) && kv.Value >= 0f)
                    {
                        values[kv.Key] = kv.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                // Corrupt or unreadable: keep it aside for a human, start
                // empty, and let SeedFrom() restore what the server knows.
                logWarning($"OdinEye client: counter file '{path}' was unreadable ({ex.Message}); starting fresh and keeping the old one as .bad");
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

                values.Clear();
            }
        }
    }
}
