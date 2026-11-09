using HarmonyLib;
using KMod;

namespace PinnedMaterials;

public sealed class PinnedMaterialsMod : UserMod2
{
    public override void OnLoad(Harmony harmony)
    {
        base.OnLoad(harmony);
        harmony.PatchAll(typeof(PinnedMaterialsMod).Assembly);
    }
}
