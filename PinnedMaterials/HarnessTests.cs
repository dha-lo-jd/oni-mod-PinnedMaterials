#if DEBUG
// In-game tests run by Gravitas.TestHarness:
//   python Gravitas.TestProbe/probe.py run --project <this repo> --tests '{"tags":["pinnedmaterials"]}'
// Needs HarnessDllPath in Local.Build.props (Check comes from the harness assembly).
// The tests change the player's real pin list (mods/config/PinnedMaterials.json); Session restores it afterwards.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gravitas.TestHarness;
using HarmonyLib;
using Newtonsoft.Json;
using PinnedMaterials.Core;
using PinnedMaterials.Ui;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PinnedMaterials;

internal static class HarnessTests
{
    private static string ConfigFile => Path.Combine(KMod.Manager.GetDirectory(), "config", "PinnedMaterials.json");

    // Starts every test from "nothing pinned" with every material discovered, and puts the player's pins back at the end.
    private sealed class Session : System.IDisposable
    {
        private readonly bool hadFile;
        private readonly byte[] fileBytes;
        private readonly bool instantBuild;

        public Session()
        {
            hadFile = File.Exists(ConfigFile);
            fileBytes = hadFile ? File.ReadAllBytes(ConfigFile) : null;
            instantBuild = DebugHandler.InstantBuildMode;
            DebugHandler.InstantBuildMode = true; // every material is listed, whatever the save has discovered
            Traverse.Create(typeof(PinStore)).Field("pins").SetValue(new List<string>());
        }

        public void Dispose()
        {
            if (PlayerController.Instance.ActiveTool == BuildTool.Instance) BuildTool.Instance.Deactivate();
            DebugHandler.InstantBuildMode = instantBuild;
            if (hadFile) File.WriteAllBytes(ConfigFile, fileBytes);
            else if (File.Exists(ConfigFile)) File.Delete(ConfigFile);
            Traverse.Create(typeof(PinStore)).Field("pins").SetValue(null); // read again from the restored file
        }
    }

    // Opens the product screen of a building as the build menu does and returns its material selectors.
    private static List<MaterialSelector> Open(string prefabId)
    {
        var def = Assets.GetBuildingDef(prefabId);
        var screen = PlanScreen.Instance.ProductInfoScreen;
        screen.ClearProduct(false);
        screen.Show();
        screen.ConfigureScreen(def);
        var panel = screen.materialSelectionPanel;
        var all = Traverse.Create(panel).Field("materialSelectors").GetValue<List<MaterialSelector>>();
        return all.Where(s => s.gameObject.activeInHierarchy && s.ElementToggles.Count > 0).ToList();
    }

    // The visible materials, top to bottom as laid out in the list.
    private static List<Tag> Order(MaterialSelector selector) =>
        selector.ElementToggles.Where(p => p.Value.gameObject.activeSelf)
            .OrderBy(p => p.Value.transform.GetSiblingIndex()).Select(p => p.Key).ToList();

    private static void Same(IEnumerable<string> expected, IEnumerable<string> actual, string message) =>
        Check.Equal(string.Join(", ", expected), string.Join(", ", actual), message);

    private static string Names(IEnumerable<Tag> tags) => string.Join(", ", tags.Select(t => t.ToString()));

    private static GameObject PinButton(MaterialSelector selector, Tag tag)
    {
        var found = Play.FindUi("PinButton", selector.ElementToggles[tag]);
        Check.Equal(1, found.Count, "pin buttons on the " + tag + " toggle");
        return found[0];
    }

    private static void ClickPin(MaterialSelector selector, Tag tag) => Play.ClickUi(PinButton(selector, tag));

    private static List<string> SavedPins() =>
        JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(ConfigFile));

    // The game never offers fewer than three materials for a Tile, but the test must not depend on that silently.
    private static List<Tag> TilePicks(MaterialSelector selector, int count)
    {
        var order = Order(selector);
        Check.True(order.Count >= count + 1, "need at least " + (count + 1) + " materials in the Tile list, got " + order.Count);
        // From the bottom of the list, so that pinning moves them up.
        return Enumerable.Range(1, count).Select(i => order[order.Count - i]).ToList();
    }

    [HarnessTest(Name = "pinnedmaterials.pin_button_on_every_material", Tags = "pinnedmaterials")]
    public static void PinButtonOnEveryMaterial()
    {
        using var session = new Session();
        var selector = Open("Tile").Single();
        Check.True(selector.ElementToggles.Count >= 3, "too few materials to test with");
        foreach (var pair in selector.ElementToggles)
        {
            var overlays = pair.Value.GetComponents<PinToggleOverlay>();
            Check.Equal(1, overlays.Length, "pin overlays on " + pair.Key);
            var button = PinButton(selector, pair.Key);
            var image = button.GetComponent<Image>();
            Check.NotNull(image.sprite, "pin sprite on " + pair.Key);
            Check.True(image.color.a == 0f, pair.Key + ": the pin of an unpinned, unhovered material must be invisible");
        }

        // Opening the screen again must not stack a second pin on the same toggles.
        selector = Open("Tile").Single();
        foreach (var pair in selector.ElementToggles)
            Check.Equal(1, pair.Value.GetComponentsInChildren<PinDragHandle>(true).Length, "pin buttons on " + pair.Key + " after reopening");
    }

    [HarnessTest(Name = "pinnedmaterials.pin_moves_to_top_and_unpin_restores", Tags = "pinnedmaterials")]
    public static void PinMovesToTopAndUnpinRestores()
    {
        using var session = new Session();
        var selector = Open("Tile").Single();
        var original = Order(selector);
        var target = TilePicks(selector, 1)[0];
        Check.False(original[0] == target, "the material to pin is already on top");

        ClickPin(selector, target);
        var pinned = Order(selector);
        Check.True(pinned[0] == target, "pinned material is not first: " + Names(pinned));
        Check.Equal(original.Count, pinned.Count, "number of listed materials after pinning");
        Same(original.Where(t => t != target).Select(t => t.ToString()),
            pinned.Skip(1).Select(t => t.ToString()), "the other materials must keep the game's order");
        var image = PinButton(selector, target).GetComponent<Image>();
        Check.True(image.color.a > 0f, "the pin of a pinned material must be visible");
        Same(new[] { target.ToString() }, SavedPins(), "pins saved to PinnedMaterials.json");

        ClickPin(selector, target);
        var restored = Order(selector);
        Same(original.Select(t => t.ToString()), restored.Select(t => t.ToString()), "order after unpinning");
        Check.Equal(0, SavedPins().Count, "pins saved after unpinning");
    }

    [HarnessTest(Name = "pinnedmaterials.pins_keep_pin_order", Tags = "pinnedmaterials")]
    public static void PinsKeepPinOrder()
    {
        using var session = new Session();
        var selector = Open("Tile").Single();
        var original = Order(selector);
        var picks = TilePicks(selector, 3); // bottom, second from bottom, third from bottom
        foreach (var tag in picks) ClickPin(selector, tag);

        var order = Order(selector);
        Same(picks.Select(t => t.ToString()), order.Take(3).Select(t => t.ToString()),
            "pinned materials must come first, in the order they were pinned");
        Same(original.Where(t => !picks.Contains(t)).Select(t => t.ToString()),
            order.Skip(3).Select(t => t.ToString()), "unpinned materials keep the game's order");
        Same(picks.Select(t => t.ToString()), SavedPins(), "saved pin order");

        // Unpinning one in the middle leaves the others in place.
        ClickPin(selector, picks[1]);
        Same(new[] { picks[0].ToString(), picks[2].ToString() }, Order(selector).Take(2).Select(t => t.ToString()),
            "pins after unpinning the middle one");
    }

    [HarnessTest(Name = "pinnedmaterials.pins_are_global", Tags = "pinnedmaterials")]
    public static void PinsAreGlobal()
    {
        using var session = new Session();
        // The selectors are reused by every building: read what each lists before opening the next one.
        var tileTags = Order(Open("Tile").Single());
        // Another building drawing on the same material category as Tile.
        var tileCategory = Assets.GetBuildingDef("Tile").MaterialCategory[0];
        var otherDef = Assets.BuildingDefs.First(d => d.PrefabID != "Tile" && d.MaterialCategory.Length == 1 && d.MaterialCategory[0] == tileCategory);
        var doorTags = Order(Open(otherDef.PrefabID).Single());
        // A material both buildings offer, not first in either list.
        var target = tileTags.Last(t => doorTags.Contains(t) && t != doorTags[0] && t != tileTags[0]);
        var tile = Open("Tile").Single();
        ClickPin(tile, target);
        Check.True(Order(tile)[0] == target, "pin not first in Tile");

        // Another building with a raw-material list shows the same pin first.
        var other = Open(otherDef.PrefabID).Single();
        Check.True(Order(other)[0] == target, "pin not applied to " + otherDef.PrefabID + ": " + Names(Order(other)));

        // Every material slot of a building with several slots.
        var def = Assets.BuildingDefs.FirstOrDefault(d => d.MaterialCategory.Length >= 2);
        Check.NotNull(def, "no building with two material slots found");
        var slots = Open(def.PrefabID);
        Check.True(slots.Count >= 2, def.PrefabID + " shows " + slots.Count + " material slots");
        foreach (var slot in slots)
        {
            var last = Order(slot).Last();
            ClickPin(slot, last);
            var after = Order(slot);
            Check.True(after.IndexOf(last) < after.Count(t => PinStore.IsPinned(t)),
                def.PrefabID + " slot " + slot.selectorIndex + ": " + last + " is not among the pinned materials on top: " + Names(after));
        }

        // A pin made in one slot applies to every slot that lists that material: pinned materials come first in each.
        foreach (var slot in slots)
        {
            var order = Order(slot);
            var pinnedCount = order.Count(t => PinStore.IsPinned(t));
            Check.True(pinnedCount >= 1, "slot " + slot.selectorIndex + " lists no pinned material");
            Check.True(order.Take(pinnedCount).All(t => PinStore.IsPinned(t)),
                "slot " + slot.selectorIndex + ": pinned materials are not all first: " + Names(order));
        }
    }

    [HarnessTest(Name = "pinnedmaterials.drag_reorders_pins", Tags = "pinnedmaterials")]
    public static IEnumerator DragReordersPins()
    {
        using var session = new Session();
        var selector = Open("Tile").Single();
        var picks = TilePicks(selector, 3);
        foreach (var tag in picks) ClickPin(selector, tag);
        var (a, b, c) = (picks[0], picks[1], picks[2]);

        // Drag c's pin over a's material: c takes a's place, the others shift down.
        var system = UnityEngine.EventSystems.EventSystem.current;
        var button = PinButton(selector, c);
        var handle = button.GetComponent<PinDragHandle>();
        var begin = new PointerEventData(system) { position = Vector2.zero, pointerPress = button };
        handle.OnBeginDrag(begin);
        var ghost = GameObject.Find("PinDragGhost");
        Check.NotNull(ghost, "no drag ghost while dragging");
        var group = selector.ElementToggles[c].GetComponent<CanvasGroup>();
        Check.NotNull(group, "no CanvasGroup on the dragged material");
        Check.True(group.alpha < 1f, "the dragged material is not dimmed");

        var hover = new PointerEventData(system)
        {
            position = Vector2.zero,
            pointerCurrentRaycast = new RaycastResult { gameObject = PinButton(selector, a) }
        };
        handle.OnDrag(hover);
        yield return null;
        Same(new[] { c, a, b }.Select(t => t.ToString()), Order(selector).Take(3).Select(t => t.ToString()),
            "order while dragging c over a");
        Check.False(File.Exists(ConfigFile) && SavedPins()[0] == c.ToString(), "the order must be saved on drop, not while dragging");

        handle.OnEndDrag(hover);
        yield return null;
        Check.True(GameObject.Find("PinDragGhost") == null, "drag ghost left behind");
        Check.True(group != null && group.alpha == 1f, "the dragged material stays dimmed (alpha " + (group == null ? "destroyed" : group.alpha.ToString()) + ")");
        Same(new[] { c, a, b }.Select(t => t.ToString()), SavedPins(), "saved order after the drop");

        // Dragging an unpinned material's pin does nothing.
        var free = Order(selector).Last();
        var freeHandle = PinButton(selector, free).GetComponent<PinDragHandle>();
        freeHandle.OnBeginDrag(begin);
        freeHandle.OnDrag(new PointerEventData(system) { pointerCurrentRaycast = new RaycastResult { gameObject = PinButton(selector, c) } });
        freeHandle.OnEndDrag(begin);
        Check.True(GameObject.Find("PinDragGhost") == null, "an unpinned material started a drag");
        Same(new[] { c, a, b }.Select(t => t.ToString()), Order(selector).Take(3).Select(t => t.ToString()),
            "order after dragging an unpinned material");
    }

    [HarnessTest(Name = "pinnedmaterials.pins_persist_in_config_file", Tags = "pinnedmaterials")]
    public static void PinsPersistInConfigFile()
    {
        using var session = new Session();
        var selector = Open("Tile").Single();
        var picks = TilePicks(selector, 2);
        foreach (var tag in picks) ClickPin(selector, tag);
        Check.True(File.Exists(ConfigFile), "config file was not written");
        var saved = SavedPins();
        Same(picks.Select(t => t.ToString()), saved, "saved pins");

        // A restart reads the file again: forget the in-memory list and reopen the screen.
        Traverse.Create(typeof(PinStore)).Field("pins").SetValue(null);
        Check.Equal(0, PinStore.IndexOf(picks[0]), "first pin after reloading from file");
        Check.Equal(1, PinStore.IndexOf(picks[1]), "second pin after reloading from file");
        selector = Open("Tile").Single();
        Same(picks.Select(t => t.ToString()), Order(selector).Take(2).Select(t => t.ToString()),
            "list order after reloading pins from the file");
    }

    [HarnessTest(Name = "pinnedmaterials.undiscovered_stay_hidden", Tags = "pinnedmaterials")]
    public static void UndiscoveredStayHidden()
    {
        using var session = new Session();
        Check.False(Game.Instance.SandboxModeActive, "the save is in sandbox mode, where every material is listed");
        UndiscoveredCheck();
    }

    private static void UndiscoveredCheck()
    {
        var all = Order(Open("Tile").Single()); // everything, as the session lists it (instant build)
        Check.True(all.Count >= 3, "too few materials to test with");
        var discovered = Traverse.Create(DiscoveredResources.Instance).Field("Discovered");
        var original = discovered.GetValue<HashSet<Tag>>();
        DebugHandler.InstantBuildMode = false; // the game's own discovery rules
        try
        {
            // Only the last material counts as discovered.
            var known = all.Last();
            discovered.SetValue(new HashSet<Tag> { known });
            var selector = Open("Tile").Single();
            Check.Equal(known.ToString(), Names(Order(selector)), "listed materials with one discovered");

            // Pin an undiscovered one and the discovered one: the undiscovered stays hidden, the other goes first.
            PinStore.Toggle(all[0]);
            PinStore.Toggle(known);
            PinToggleOverlay.RefreshAll();
            Check.False(selector.ElementToggles[all[0]].gameObject.activeSelf, "an undiscovered material became visible by pinning");
            Check.Equal(known.ToString(), Names(Order(selector)), "listed materials after pinning");

            // Once discovered, the pinned material shows up on top.
            discovered.SetValue(new HashSet<Tag> { known, all[1], all[0] });
            selector = Open("Tile").Single();
            Check.Equal(string.Join(", ", new[] { all[0], known, all[1] }.Select(t => t.ToString())), Names(Order(selector)),
                "listed materials after discovering the pinned one");
        }
        finally
        {
            discovered.SetValue(original);
        }
    }
}
#endif
