using System.Runtime.CompilerServices;
using Assets.Scripts;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using HarmonyLib;
using JetBrains.Annotations;

namespace RustMod.Patch {
[HarmonyPatch]
internal static class CircuitHousingPatch {
    [CanBeNull]
    private static IProgrammable GetIProgrammable(CircuitHousing __instance) =>
        __instance._ProgrammableChipSlot.Get<IProgrammable>();

    [HarmonyPatch(typeof(CircuitHousing), "IsOperable", MethodType.Getter)]
    [HarmonyPostfix]
    public static void PatchIsOperable(ref bool __result, CircuitHousing __instance) {
        var chip = GetIProgrammable(__instance);
        if (chip != null) {
            __result = chip.ReadErrorState() != ErrorState.CompileError;
        }
    }

    [HarmonyPatch(typeof(CircuitHousing), nameof(CircuitHousing.Execute))]
    [HarmonyPostfix]
    public static void PatchExecute(CircuitHousing __instance) {
        var chip = GetIProgrammable(__instance);
        if (chip != null && GameManager.RunSimulation && !__instance.IsCursor && __instance.OnOff &&
            __instance.Powered &&
            GameManager.GameState == GameState.Running && !WorldManager.IsGamePaused &&
            chip.ReadErrorState() != ErrorState.CompileError) {
            var result = chip.Execute(__instance);
            if (result != null) {
                __instance.RaiseError(1);
            }
        }
    }

    [HarmonyPatch(typeof(CircuitHousing), "GetDeviceNameWithLabel")]
    [HarmonyPostfix]
    public static void PatchGetDeviceNameWithLabel(int deviceIndex, CircuitHousing __instance, ref string __result,
        string[] ____DeviceLabels) {
        var chip = GetIProgrammable(__instance);
        if (chip != null) {
            var text2 = ____DeviceLabels[deviceIndex];
            if (!string.IsNullOrEmpty(text2)) {
                __result = $"<color=yellow>{text2}</color> " + __result;
            }
        }
    }

    [HarmonyPatch(typeof(CircuitHousing), nameof(CircuitHousing.InteractWith))]
    [HarmonyPostfix]
    public static void PatchInteractWith(Interactable interactable, Interaction interaction, bool doAction,
        CircuitHousing __instance, ref Thing.DelayedActionInstance __result) {
        var chip = GetIProgrammable(__instance);
        if (chip != null && interactable.Action is not (InteractableType.Button1 or InteractableType.Button2
                or InteractableType.Button3 or InteractableType.Button4 or InteractableType.Button5
                or InteractableType.Button6)) {
            chip.AppendErrorsToActionInstance(__result, interactable, interaction, doAction);
        }
    }

    [HarmonyPatch(typeof(CircuitHousing), nameof(CircuitHousing.GetLogicValue), typeof(LogicType))]
    [HarmonyPostfix]
    public static void PatchGetLogicValue(LogicType logicType, CircuitHousing __instance, ref double __result) {
        var logicValue = GetIProgrammable(__instance)?.ChipGetLogicValue(logicType);
        if (logicValue.HasValue) {
            __result = logicValue.Value;
        }
    }

    [HarmonyPatch(typeof(CircuitHousing), nameof(CircuitHousing.SetLogicValue))]
    [HarmonyPostfix]
    public static void PatchSetLogicValue(CircuitHousing __instance, LogicType logicType, double value) {
        GetIProgrammable(__instance)?.SetLogicValue(logicType, value);
    }

    [HarmonyPatch(typeof(CircuitHousing), nameof(CircuitHousing.OnFinishedLoad))]
    [HarmonyPostfix]
    public static void PatchOnFinishedLoad(CircuitHousing __instance) {
        if (GetIProgrammable(__instance) != null) {
            __instance.ClearError();
            __instance.RefreshError();
        }
    }

    [HarmonyPatch(typeof(CircuitHousing), nameof(CircuitHousing.SetSourceCode))]
    [HarmonyPostfix]
    public static void PatchSetSourceCode(string sourceCode, CircuitHousing __instance) {
        var chip = GetIProgrammable(__instance);
        if (chip != null) {
            chip.SetSourceCode(sourceCode);
            __instance._MemoryLight?.Flash(LogicMemoryState.Write);
        }
    }

    [HarmonyPatch(typeof(CircuitHousing), nameof(CircuitHousing.GetSourceCode))]
    [HarmonyPostfix]
    public static void PatchGetSourceCode(CircuitHousing __instance, ref string __result) {
        var chip = GetIProgrammable(__instance);
        if (chip != null) {
            __instance._MemoryLight?.Flash(LogicMemoryState.Read);
            __result = chip.GetSourceCode();
        }
    }

    [HarmonyReversePatch]
    [HarmonyPatch(typeof(Thing), nameof(Thing.OnChildEnterInventory))]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void BaseOnChildEnterInventory(CircuitHousing instance, DynamicThing newChild) { }

    [HarmonyPatch(typeof(CircuitHousing), nameof(CircuitHousing.OnChildEnterInventory))]
    [HarmonyPrefix]
    public static bool PatchOnChildEnterInventory(DynamicThing newChild, CircuitHousing __instance,
        ref byte ____processingUpdateFlags) {
        var chip = GetIProgrammable(__instance);
        if (chip != null) {
            BaseOnChildEnterInventory(__instance, newChild);
            __instance.RefreshError();

            chip.Placed(__instance);
            __instance._MemoryLight?.Flash(LogicMemoryState.Write);
            if (NetworkManager.IsServer) {
                ____processingUpdateFlags |= 63;
                __instance.NetworkUpdateFlags |= 512;
            }

            __instance.ClearError();

            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(CircuitHousing), nameof(CircuitHousing.OnChildExitInventory))]
    [HarmonyPostfix]
    public static void PatchOnChildExitInventory(DynamicThing previousChild, CircuitHousing __instance,
        ref byte ____processingUpdateFlags) {
        if (previousChild is IProgrammable programmable) {
            programmable.Unplaced(__instance);
        }
    }

    [HarmonyPatch(typeof(CircuitHousing), nameof(CircuitHousing.ReadMemory))]
    [HarmonyPrefix]
    public static bool PatchReadMemory(int address, CircuitHousing __instance, ref double __result) {
        var chip = GetIProgrammable(__instance);
        if (chip != null) {
            __instance._MemoryLight?.Flash(LogicMemoryState.Read);
            __result = chip.ReadMemory(address);
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(CircuitHousing), nameof(CircuitHousing.WriteMemory))]
    [HarmonyPrefix]
    public static bool PatchWriteMemory(int address, double value, CircuitHousing __instance) {
        var chip = GetIProgrammable(__instance);
        if (chip != null) {
            chip.WriteMemory(address, value);
            __instance._MemoryLight?.Flash(LogicMemoryState.Write);

            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(CircuitHousing), nameof(CircuitHousing.ClearMemory))]
    [HarmonyPrefix]
    public static bool PatchClearMemory(CircuitHousing __instance) {
        var chip = GetIProgrammable(__instance);
        if (chip != null) {
            chip.ClearMemory();
            return false;
        }

        return true;
    }
}
}