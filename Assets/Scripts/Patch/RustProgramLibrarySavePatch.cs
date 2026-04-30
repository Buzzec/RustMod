using Assets.Scripts.Serialization;
using Assets.Scripts.Objects;
using HarmonyLib;

namespace RustMod.Patch {
[HarmonyPatch]
internal static class RustProgramLibrarySavePatch {
    [HarmonyPatch(typeof(XmlSaveLoad), nameof(XmlSaveLoad.LoadWorld))]
    [HarmonyPrefix]
    public static void ResetLibraryForWorldLoad() {
        RustMod.Instance?.ResetProgramLibrary();
    }

    [HarmonyPatch(typeof(XmlSaveLoad), nameof(XmlSaveLoad.GetWorldData))]
    [HarmonyPostfix]
    public static void AddLibrarySaveData(XmlSaveLoad.WorldData __result) {
        var mod = RustMod.Instance;
        if (mod == null || __result?.OrderedThings == null) {
            return;
        }

        __result.OrderedThings.RemoveAll(t => t is RustProgramLibrarySaveData);
        __result.OrderedThings.Insert(0, new RustProgramLibrarySaveData {
            ReferenceId = 0,
            LibraryState = mod.CaptureLibraryStateBase64(),
        });
    }

    [HarmonyPatch(typeof(XmlSaveLoad), nameof(XmlSaveLoad.LoadThing))]
    [HarmonyPrefix]
    public static bool LoadLibrarySaveData(ThingSaveData thingData, ref Thing __result) {
        if (thingData is not RustProgramLibrarySaveData librarySaveData) {
            return true;
        }

        var mod = RustMod.Instance;
        mod?.ResetProgramLibrary();
        mod?.ApplyLibraryStateBase64(librarySaveData.LibraryState);
        __result = null;
        return false;
    }
}
}
