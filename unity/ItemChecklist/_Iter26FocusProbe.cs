using System.Collections.Generic;
using HarmonyLib;
using ItemChecklist.UI;
using UnityEngine;

namespace ItemChecklist
{
    // THROWAWAY Iter-26 passive focus-race logger — remove before the fix commit.
    //
    // The bug is intermittent (timing race, 0 exceptions). This logs to Player.log
    // ONLY on the bug edge: SearchBar caret ON while Manager.input.activeInputField
    // is NOT the SearchBar (keystrokes routed nowhere → typing dead). On the edge it
    // dumps a ring buffer of the recent input-routing events so the culprit is visible.
    //
    // IMPORTANT (learned the hard way): Harmony can only patch trusted GAME-DLL types
    // (Pug.Other: InputManager, TextInputField…), NOT the mod's own Roslyn-compiled
    // types. Patching ItemChecklistWindow/SearchBar throws "patching failed: unknown
    // assembly" and aborts PatchAll for the WHOLE mod (no OnOccupied bake → empty
    // catalog). So the two mod-side hooks below are PLAIN CALLS from mod code
    // (SearchBar.LateUpdate → DetectFrame, ItemChecklistWindow.ShowUI → Record),
    // never Harmony patches. Only CK types are patched here.
    //
    // Grep:  grep ICL-DIAG26 Player.log
    internal static class Iter26FocusProbe
    {
        private static readonly Queue<string> s_events = new Queue<string>();
        private const int MaxEvents = 20;
        private static bool s_bug;

        internal static void Record(string ev)
        {
            s_events.Enqueue($"f{Time.frameCount} {ev}");
            while (s_events.Count > MaxEvents) s_events.Dequeue();
        }

        internal static void Dump(string header)
        {
            Debug.Log($"[ICL-DIAG26] ===== {header} =====");
            foreach (var e in s_events)
                Debug.Log($"[ICL-DIAG26]   {e}");
        }

        internal static string Kind(object o) =>
            o == null ? "null" : o is SearchBar ? "SearchBar"
            : o is TextInputField ? "TextInputField" : "other";

        // Called every frame from SearchBar.LateUpdate (mod code — NOT a Harmony patch).
        // Silent unless an EDGE occurs: caret ON but activeInputField != SearchBar.
        internal static void DetectFrame(SearchBar sb)
        {
            bool active = ReferenceEquals(Manager.input.activeInputField, sb);
            bool caret = sb.characterMarkBlinker != null
                && sb.characterMarkBlinker.gameObject.activeSelf;
            bool bug = caret && !active;

            if (bug && !s_bug)
            {
                s_bug = true;
                Record($"BUG-ONSET inputIsActive={sb.inputIsActive} "
                    + $"activeField={Kind(Manager.input.activeInputField)} "
                    + $"selected={Kind(Manager.ui.currentSelectedUIElement)} "
                    + $"textInputIsActive={Manager.input.textInputIsActive}");
                Dump("BUG DETECTED (caret on, typing dead)");
            }
            else if (!bug && s_bug)
            {
                s_bug = false;
                Record("BUG-CLEARED (focus recovered)");
                Dump("BUG CLEARED");
            }
        }
    }

    [HarmonyPatch(typeof(InputManager), nameof(InputManager.SetActiveInputField))]
    internal static class Iter26_SetActivePatch
    {
        static void Postfix(InputManager __instance) =>
            Iter26FocusProbe.Record(
                $"SetActiveInputField -> {Iter26FocusProbe.Kind(__instance.activeInputField)}");
    }

    [HarmonyPatch(typeof(TextInputField), nameof(TextInputField.OnLeftClicked))]
    internal static class Iter26_ClickPatch
    {
        static void Postfix(TextInputField __instance)
        {
            if (__instance is SearchBar)
                Iter26FocusProbe.Record(
                    $"SearchBar.OnLeftClicked selected={Iter26FocusProbe.Kind(Manager.ui.currentSelectedUIElement)}");
        }
    }

    [HarmonyPatch(typeof(TextInputField), nameof(TextInputField.Deactivate))]
    internal static class Iter26_DeactivatePatch
    {
        static void Postfix(TextInputField __instance, bool commit)
        {
            if (__instance is SearchBar)
                Iter26FocusProbe.Record($"SearchBar.Deactivate commit={commit}");
        }
    }
}
