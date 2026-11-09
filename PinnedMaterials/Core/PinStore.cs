using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace PinnedMaterials.Core;

// Global (not per-save) list of pinned material tags, in pin order. Persisted to mods/config/PinnedMaterials.json.
public static class PinStore
{
    private static List<string> pins;

    private static string FilePath => Path.Combine(KMod.Manager.GetDirectory(), "config", "PinnedMaterials.json");

    private static List<string> Pins => pins ??= Load();

    // -1 when the tag is not pinned.
    public static int IndexOf(Tag tag)
    {
        return Pins.IndexOf(tag.ToString());
    }

    public static bool IsPinned(Tag tag)
    {
        return IndexOf(tag) >= 0;
    }

    public static void Toggle(Tag tag)
    {
        var key = tag.ToString();
        if (!Pins.Remove(key))
        {
            Pins.Add(key);
        }

        Save();
    }

    private static List<string> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(FilePath));
                if (loaded != null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PinnedMaterials] Failed to load pins: " + e.Message);
        }

        return new List<string>();
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(Pins, Formatting.Indented));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PinnedMaterials] Failed to save pins: " + e.Message);
        }
    }
}
