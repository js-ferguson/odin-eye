namespace OdinEye.Client.Hud
{
    using System;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;

    // ODINEYE-32: a small clock label just below the minimap, showing
    // TemporalClock.Format(EnvManTimeReader.GetTotalSeconds()) live.
    //
    // Built by cloning Minimap.instance.m_biomeNameSmall -- a real,
    // already-styled/positioned vanilla label (confirmed public, both
    // in this project's compile-time stub and the real running game,
    // via IL disassembly) -- rather than constructing a new
    // Canvas/RectTransform from scratch, so this inherits Valheim's own
    // font/material/rendering with none of the from-scratch UI-layout
    // risk that would carry.
    //
    // TMPro.TMP_Text (the field's actual type) lives in
    // Unity.TextMeshPro.dll -- an assembly this project has no
    // NuGet-restorable compile-time reference for (unlike
    // assembly_valheim/Assembly-CSharp, TextMeshPro isn't published
    // anywhere as a redistributable reference package, and vendoring a
    // raw copy would only build locally, not on GitHub Actions'
    // windows-latest runner). Resolved and driven entirely via
    // reflection instead, off the SAME already-loaded runtime assembly
    // Valheim's own UI already uses (guaranteed present -- Valheim's
    // whole UI is built on it) -- consistent with this project's
    // established pattern for any out-of-stub type (see
    // CheatStatusReader's Achievements lookup).
    //
    // Not visually verified by this project's own author -- unlike
    // every other reader here, correctness of an on-screen HUD position
    // can only be confirmed by someone actually looking at their game
    // client, not by inspecting an HTTP response.
    public sealed class ClockHudElement
    {
        private static readonly Lazy<PropertyInfo> TmpTextProperty = new Lazy<PropertyInfo>(() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Unity.TextMeshPro")
                ?.GetType("TMPro.TMP_Text")
                ?.GetProperty("text"));

        private static readonly FieldInfo BiomeNameSmallField =
            typeof(Minimap).GetField("m_biomeNameSmall", BindingFlags.Public | BindingFlags.Instance);

        private Component clockLabel;
        private bool triedToCreate;
        private Minimap ownerMinimap;

        public void SetText(string text)
        {
            var label = EnsureCreated();
            var property = TmpTextProperty.Value;
            if (label == null || property == null)
            {
                return;
            }

            try
            {
                property.SetValue(label, text);
            }
            catch
            {
                // Best-effort HUD element -- never let a UI hiccup here
                // interrupt the rest of this plugin's Update() loop.
            }
        }

        private Component EnsureCreated()
        {
            // A fresh Minimap instance is created on every world
            // load/reconnect -- our clone (parented under the PREVIOUS
            // instance's now-destroyed hierarchy) doesn't survive that,
            // so re-attempt creation against the new one rather than
            // permanently skipping after the first world.
            if (Minimap.instance != ownerMinimap)
            {
                ownerMinimap = Minimap.instance;
                clockLabel = null;
                triedToCreate = false;
            }

            if (clockLabel != null || triedToCreate || Minimap.instance == null || BiomeNameSmallField == null)
            {
                return clockLabel;
            }

            // Only ever attempted once per Minimap instance (a fresh
            // one is created on every world load) -- a failed attempt
            // isn't retried every frame.
            triedToCreate = true;

            try
            {
                if (!(BiomeNameSmallField.GetValue(Minimap.instance) is Component template))
                {
                    return null;
                }

                var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                clone.name = "OdinEyeClockText";

                var rect = clone.GetComponent<RectTransform>();
                if (rect != null)
                {
                    // Just below the biome-name label this was cloned
                    // from, which itself sits just below the small
                    // minimap circle.
                    rect.anchoredPosition += new Vector2(0f, -30f);
                }

                clockLabel = clone.GetComponent(template.GetType());
            }
            catch
            {
                clockLabel = null;
            }

            return clockLabel;
        }
    }
}
