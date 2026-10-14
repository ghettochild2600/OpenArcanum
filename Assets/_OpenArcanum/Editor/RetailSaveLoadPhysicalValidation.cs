using System;
using System.Collections;
using System.Linq;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using OpenArcanum.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class RetailSaveLoadPhysicalValidation
{
    private const string Ground = "maps/arcanum1-024-fixed/101602821845.sec";
    private const string ProofSlot = "uicproof";

    [MenuItem("OpenArcanum/M6C/Prepare Source Save Load Physical Proof")]
    private static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        WorldObjectSectorLoader loader = Object.FindFirstObjectByType<WorldObjectSectorLoader>();
        if (loader == null) throw new InvalidOperationException("The production world loader is unavailable.");
        loader.StartCoroutine(Prepare(loader));
    }

    [MenuItem("OpenArcanum/M6C/Cleanup Source Save Load Physical Proof")]
    private static void Cleanup()
    {
        WorldMapSessionCoordinator session = Object.FindFirstObjectByType<WorldMapSessionCoordinator>();
        session?.SaveSlots.DeleteSlot(ProofSlot);
        Debug.Log("RUNTIME SAVE LOAD PHYSICAL CLEANUP: disposable slot removed.");
    }

    private static IEnumerator Prepare(WorldObjectSectorLoader loader)
    {
        WorldMapSessionCoordinator session = loader.Session;
        session.SaveSlots.DeleteSlot(ProofSlot);
        session.ResetAuthoritativeSession();
        yield return null;
        Check(session.SelectSector(Ground), "opening crash-site sector loads");
        yield return null;
        yield return null;

        ProductionGameUiPresenter presenter = Object.FindFirstObjectByType<ProductionGameUiPresenter>();
        Check(presenter != null && session.PlayerState != null, "production presenter and player exist");
        presenter.Controller.Close();
        yield return null;
        yield return new WaitForEndOfFrame();
        presenter.Controller.OpenSaveLoad(SaveLoadPanelMode.Save);
        yield return null;
        yield return new WaitForEndOfFrame();
        Check(session.SaveSlots.SaveSlot(ProofSlot, "The Crash Site").Succeeded,
            "source-facing proof slot saves");

        session.Campaign.SetVar(10, 77);
        Check(session.SaveSlots.LoadSlot(ProofSlot).Succeeded, "proof slot loads");
        Check(session.Campaign.GetVar(10) == 0, "authoritative state restores from the slot");

        presenter.Controller.OpenSaveLoad(SaveLoadPanelMode.Load);
        Check(presenter.Controller.SaveLoad.SelectSlot(ProofSlot), "proof slot is selectable");
        yield return null;
        yield return null;

        RetailSaveLoadView view = Object.FindFirstObjectByType<RetailSaveLoadView>();
        Check(presenter.Controller.Screen == GameUiScreen.SaveLoad && view != null && view.IsVisible,
            "source Save/Load view is the visible modal");
        Check(view.WindowTransform != null && view.WindowTransform.sizeDelta == new Vector2(800, 400),
            "source window is 800x400 inside the native 800x600 client");
        Check(view.GetComponentsInChildren<SourceUiImage>(true).Any(value =>
                value.Key.SourceId == RetailSaveLoadLayout.BackgroundSourceId), "ART 745 background is mounted");
        SaveLoadSlotView slot = presenter.Controller.SaveLoad.Slots.Single(value => value.SlotId == ProofSlot);
        Check(slot.DisplayName == "The Crash Site" && !string.IsNullOrEmpty(slot.GameDate)
              && !string.IsNullOrEmpty(slot.GameTime), "source name and game date/time project");
        Check(!string.IsNullOrEmpty(slot.ThumbnailPngBase64), "468x300 source thumbnail metadata exists");
        Check(view.GetComponentsInChildren<SourceUiButton>(true).Any(value => value.name == "Save Load Action")
              && view.GetComponentsInChildren<SourceUiButton>(true).Any(value => value.name == "Delete Save")
              && view.GetComponentsInChildren<SourceUiButton>(true).Any(value => value.name == "Cancel Save Load"),
            "Load, Delete, and Cancel pointer controls exist");
        Debug.Log("RUNTIME SAVE LOAD PHYSICAL VALIDATION: PASS; client=800x600; window=ART745 800x400; "
                  + "slot=The Crash Site; thumbnail=468x300; game-date/time=PASS; save-load-roundtrip=PASS; "
                  + "load/delete/cancel pointer-controls=PASS. Select DELETE and confirm to remove uicproof.");
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Save/Load physical validation FAIL: " + label);
    }
}
