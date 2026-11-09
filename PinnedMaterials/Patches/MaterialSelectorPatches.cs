using HarmonyLib;
using PinnedMaterials.Core;
using PinnedMaterials.Ui;

namespace PinnedMaterials.Patches;

public static class MaterialSelectorPatches
{
    // Pinned tags sort before everything else, in pin order. The game uses this comparer for the toggle layout,
    // the scroll position and auto selection, so all of them stay consistent.
    [HarmonyPatch(typeof(MaterialSelector), "ElementSorter")]
    public static class ElementSorterPatch
    {
        public static void Postfix(Tag at, Tag bt, ref int __result)
        {
            var a = PinStore.IndexOf(at);
            var b = PinStore.IndexOf(bt);
            if (a < 0 && b < 0)
            {
                return;
            }

            if (a >= 0 && b >= 0)
            {
                __result = a.CompareTo(b);
                return;
            }

            __result = a >= 0 ? -1 : 1;
        }
    }

    // Adds the pin button to every material toggle once.
    [HarmonyPatch(typeof(MaterialSelector), nameof(MaterialSelector.ConfigureScreen))]
    public static class ConfigureScreenPatch
    {
        public static void Postfix(MaterialSelector __instance)
        {
            foreach (var pair in __instance.ElementToggles)
            {
                var toggle = pair.Value;
                if (toggle == null || toggle.GetComponent<PinToggleOverlay>() != null)
                {
                    continue;
                }

                toggle.gameObject.AddComponent<PinToggleOverlay>().Init(pair.Key);
            }
        }
    }
}
