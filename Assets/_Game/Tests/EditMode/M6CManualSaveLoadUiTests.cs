using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Runtime.Dialogue;
using Arcanum.Runtime.Save;
using Arcanum.Runtime.UI;
using Arcanum.Runtime.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arcanum.Formats.Tests
{
    [Category("M6C")]
    public sealed class M6CManualSaveLoadUiTests
    {
        private FakeSlots _slots;
        private SaveLoadPanelController _panel;

        [SetUp]
        public void SetUp()
        {
            _slots = new FakeSlots();
            _panel = new SaveLoadPanelController(_slots);
        }

        [Test]
        public void EmptyCatalogShowsNoSlotsAndFirstGeneratedId()
        {
            _panel.Open(SaveLoadPanelMode.Save);
            Assert.That(_panel.Slots, Is.Empty);
            Assert.That(_panel.SelectedSlotId, Is.Null);
            Assert.That(_panel.SuggestedSlotId, Is.EqualTo("manual01"));
        }

        [Test]
        public void AuthoritativeMetadataIsFormattedWithoutGameplayRecomputation()
        {
            _slots.Add("alpha", true, "2026-09-13T20:15:30.0000000Z", "maps/test/1.sec", 7, 1);
            _panel.Open(SaveLoadPanelMode.Load);
            SaveLoadSlotView slot = _panel.Slots.Single();
            Assert.That(slot.Timestamp, Is.EqualTo("2026-09-13 20:15 UTC"));
            Assert.That(slot.Location, Is.EqualTo("test"));
            Assert.That(slot.Level, Is.EqualTo("7"));
            Assert.That(slot.Version, Is.EqualTo("slot 1 / session 1"));
        }

        [Test]
        public void SourceFacingMetadataUsesGameClockNameLocationAndThumbnail()
        {
            string thumbnail = Convert.ToBase64String(new byte[] { 1, 2, 3 });
            _slots.Add("alpha", true, displayName: "Crash Site", pcName: "Morgan",
                areaName: "The Crash Site", gameTime: SourceTimeService.InitialMilliseconds + 37_000,
                thumbnail: thumbnail);
            _panel.Open(SaveLoadPanelMode.Load);
            SaveLoadSlotView slot = _panel.Slots.Single();
            Assert.That(slot.DisplayName, Is.EqualTo("Crash Site"));
            Assert.That(slot.PcName, Is.EqualTo("Morgan"));
            Assert.That(slot.Location, Is.EqualTo("The Crash Site"));
            Assert.That(slot.GameDate, Is.EqualTo("1/1/1885"));
            Assert.That(slot.GameTime, Is.EqualTo("3:00pm"));
            Assert.That(slot.ThumbnailPngBase64, Is.EqualTo(thumbnail));
        }

        [Test]
        public void RetailLayoutMatchesMainmenuSourceRectangles()
        {
            Assert.That(RetailSaveLoadLayout.BackgroundSourceId, Is.EqualTo(745));
            Assert.That(RetailSaveLoadLayout.PcLensFrameSourceId, Is.EqualTo(746));
            Assert.That(RetailSaveLoadLayout.ScreenshotFrameSourceId, Is.EqualTo(748));
            Assert.That(RetailSaveLoadLayout.SlotList, Is.EqualTo(new Rect(42, 120, 145, 213)));
            Assert.That(RetailSaveLoadLayout.Screenshot, Is.EqualTo(new Rect(281, 55, 468, 300)));
            Assert.That(RetailSaveLoadLayout.Location, Is.EqualTo(new Rect(281, 212, 465, 18)));
            Assert.That(RetailSaveLoadLayout.Action, Is.EqualTo(new Vector2(675, 55)));
            Assert.That(RetailSaveLoadLayout.Cancel, Is.EqualTo(new Vector2(69, 55)));
            Assert.That(RetailSaveLoadLayout.Delete, Is.EqualTo(new Vector2(115, 396)));
        }

        [Test]
        public void LoadModeSelectsFirstValidSlotWhileSaveModeStartsNew()
        {
            _slots.Add("broken", false);
            _slots.Add("valid", true);
            _panel.Open(SaveLoadPanelMode.Save);
            Assert.That(_panel.SelectedSlotId, Is.Null);
            _panel.SetMode(SaveLoadPanelMode.Load);
            Assert.That(_panel.SelectedSlotId, Is.EqualTo("valid"));
            _panel.SetMode(SaveLoadPanelMode.Save);
            Assert.That(_panel.SelectedSlotId, Is.Null);
        }

        [Test]
        public void SelectionMustReferToVisibleSlot()
        {
            _slots.Add("alpha", true);
            _panel.Open(SaveLoadPanelMode.Load);
            Assert.That(_panel.SelectSlot("missing"), Is.False);
            Assert.That(_panel.SelectSlot("alpha"), Is.True);
            Assert.That(_panel.SelectedSlotId, Is.EqualTo("alpha"));
        }

        [Test]
        public void NewSaveUsesGeneratedSafeIdAndRefreshesCatalog()
        {
            _panel.Open(SaveLoadPanelMode.Save);
            _panel.RequestSave();
            Assert.That(_slots.SaveCalls, Is.EqualTo(1));
            Assert.That(_slots.LastSlotId, Is.EqualTo("manual01"));
            Assert.That(_panel.SelectedSlotId, Is.EqualTo("manual01"));
            Assert.That(_panel.StatusMessage, Is.EqualTo("Saved 'manual01'."));
            Assert.That(_panel.Slots.Select(slot => slot.SlotId), Contains.Item("manual01"));
        }

        [Test]
        public void SourceSaveNameIsPassedAsPresentationMetadata()
        {
            _panel.Open(SaveLoadPanelMode.Save);
            _panel.SetDraftSaveName("The Crash Site");
            _panel.RequestSave();
            Assert.That(_slots.LastDisplayName, Is.EqualTo("The Crash Site"));
            Assert.That(_panel.Slots.Single().DisplayName, Is.EqualTo("The Crash Site"));
        }

        [Test]
        public void GeneratedIdSkipsExistingNumberedSlots()
        {
            _slots.Add("manual01", true);
            _slots.Add("manual02", true);
            _panel.Open(SaveLoadPanelMode.Save);
            Assert.That(_panel.SuggestedSlotId, Is.EqualTo("manual03"));
        }

        [Test]
        public void ExistingSaveRequiresConfirmationBeforeServiceCall()
        {
            _slots.Add("alpha", true);
            _panel.Open(SaveLoadPanelMode.Save);
            _panel.SelectSlot("alpha");
            _panel.RequestSave();
            Assert.That(_panel.Confirmation, Is.EqualTo(SaveLoadConfirmation.Overwrite));
            Assert.That(_panel.ConfirmationMessage, Is.EqualTo("Overwrite 'alpha'?"));
            Assert.That(_slots.SaveCalls, Is.Zero);
        }

        [Test]
        public void CancelledOverwritePerformsNoOperation()
        {
            _slots.Add("alpha", true);
            _panel.Open(SaveLoadPanelMode.Save);
            _panel.SelectSlot("alpha");
            _panel.RequestSave();
            _panel.CancelConfirmation();
            Assert.That(_slots.SaveCalls, Is.Zero);
            Assert.That(_panel.Confirmation, Is.EqualTo(SaveLoadConfirmation.None));
            Assert.That(_panel.StatusMessage, Does.Contain("No save data was changed"));
        }

        [Test]
        public void ConfirmedOverwriteInvokesSameSaveOperationAndRefreshes()
        {
            _slots.Add("alpha", true);
            _panel.Open(SaveLoadPanelMode.Save);
            _panel.SelectSlot("alpha");
            _panel.RequestSave();
            _panel.Confirm();
            Assert.That(_slots.SaveCalls, Is.EqualTo(1));
            Assert.That(_slots.ListCalls, Is.EqualTo(2));
            Assert.That(_panel.StatusMessage, Is.EqualTo("Saved 'alpha'."));
        }

        [Test]
        public void ValidLoadInvokesServiceOnceAndDismissesPanel()
        {
            _slots.Add("alpha", true);
            _panel.Open(SaveLoadPanelMode.Load);
            _panel.RequestLoad();
            Assert.That(_slots.LoadCalls, Is.EqualTo(1));
            Assert.That(_slots.LastSlotId, Is.EqualTo("alpha"));
            Assert.That(_panel.IsOpen, Is.False);
        }

        [Test]
        public void FailedLoadLeavesPanelAndExternalActiveSessionTokenUntouched()
        {
            _slots.Add("alpha", true);
            object activeSession = _slots.ActiveSessionToken;
            _slots.LoadResult = Result(SessionSaveSlotFailure.ReadFailed);
            _panel.Open(SaveLoadPanelMode.Load);
            _panel.RequestLoad();
            Assert.That(_panel.IsOpen, Is.True);
            Assert.That(_slots.ActiveSessionToken, Is.SameAs(activeSession));
            Assert.That(_panel.ErrorMessage, Is.EqualTo("The selected save slot could not be read."));
        }

        [Test]
        public void CorruptSlotRemainsVisibleAndShowsConciseError()
        {
            _slots.Add("broken", false, error: "parse detail");
            _slots.LoadResult = Result(SessionSaveSlotFailure.MalformedSlot);
            _panel.Open(SaveLoadPanelMode.Load);
            Assert.That(_panel.Slots.Single().IsValid, Is.False);
            _panel.RequestLoad();
            Assert.That(_panel.ErrorMessage, Is.EqualTo("The selected save slot is damaged or malformed."));
            Assert.That(_panel.Slots.Single().SlotId, Is.EqualTo("broken"));
        }

        [TestCase(SessionSaveSlotFailure.UnsupportedVersion, SessionLoadFailure.None,
            "The selected save slot is from an unsupported version.")]
        [TestCase(SessionSaveSlotFailure.MissingSlot, SessionLoadFailure.None,
            "The selected save slot no longer exists.")]
        [TestCase(SessionSaveSlotFailure.LoadFailed, SessionLoadFailure.MalformedJson,
            "The embedded save data is malformed.")]
        [TestCase(SessionSaveSlotFailure.LoadFailed, SessionLoadFailure.UnsupportedVersion,
            "The embedded save is from an unsupported version.")]
        [TestCase(SessionSaveSlotFailure.LoadFailed, SessionLoadFailure.InvalidIdentity,
            "The save contains an invalid or duplicate object identity.")]
        [TestCase(SessionSaveSlotFailure.LoadFailed, SessionLoadFailure.InvalidReference,
            "The save contains an invalid object reference.")]
        public void TypedFailuresHavePlayerVisibleMessages(SessionSaveSlotFailure failure,
            SessionLoadFailure sessionFailure, string expected)
        {
            _slots.Add("alpha", true);
            _slots.LoadResult = Result(failure, sessionFailure);
            _panel.Open(SaveLoadPanelMode.Load);
            _panel.RequestLoad();
            Assert.That(_panel.ErrorMessage, Is.EqualTo(expected));
        }

        [Test]
        public void DeleteRequiresConfirmation()
        {
            _slots.Add("alpha", true);
            _panel.Open(SaveLoadPanelMode.Load);
            _panel.RequestDelete();
            Assert.That(_panel.Confirmation, Is.EqualTo(SaveLoadConfirmation.Delete));
            Assert.That(_slots.DeleteCalls, Is.Zero);
        }

        [Test]
        public void CancelledDeletePerformsNoOperation()
        {
            _slots.Add("alpha", true);
            _panel.Open(SaveLoadPanelMode.Load);
            _panel.RequestDelete();
            _panel.CancelConfirmation();
            Assert.That(_slots.DeleteCalls, Is.Zero);
            Assert.That(_panel.Slots.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmedDeleteUsesSelectedSafeSlotAndRefreshes()
        {
            _slots.Add("alpha", true);
            _panel.Open(SaveLoadPanelMode.Load);
            _panel.RequestDelete();
            _panel.Confirm();
            Assert.That(_slots.DeleteCalls, Is.EqualTo(1));
            Assert.That(_slots.LastSlotId, Is.EqualTo("alpha"));
            Assert.That(_panel.Slots, Is.Empty);
            Assert.That(_panel.StatusMessage, Is.EqualTo("Deleted 'alpha'."));
        }

        [Test]
        public void FailedDeleteLeavesSlotVisible()
        {
            _slots.Add("alpha", true);
            _slots.DeleteResult = Result(SessionSaveSlotFailure.InvalidSlotId);
            _panel.Open(SaveLoadPanelMode.Load);
            _panel.RequestDelete();
            _panel.Confirm();
            Assert.That(_panel.Slots.Count, Is.EqualTo(1));
            Assert.That(_panel.ErrorMessage, Is.EqualTo("The save-slot name is not valid."));
        }

        [Test]
        public void CatalogFailureHasExplicitEmptyStateError()
        {
            _slots.ListFailure = SessionSaveSlotFailure.EnumerateFailed;
            _panel.Open(SaveLoadPanelMode.Load);
            Assert.That(_panel.Slots, Is.Empty);
            Assert.That(_panel.ErrorMessage, Is.EqualTo("The save-slot list could not be read."));
        }

        [Test]
        public void PresenterOpenBlocksInputAndCloseRestoresIt()
        {
            var root = new GameObject("M6C presenter");
            try
            {
                root.AddComponent<WorldMapSessionCoordinator>();
                PlayerInputGate gate = root.AddComponent<PlayerInputGate>();
                ProductionSaveLoadPresenter presenter = root.AddComponent<ProductionSaveLoadPresenter>();
                presenter.BindSlotOperations(_slots);
                presenter.Open(SaveLoadPanelMode.Save);
                Assert.That(gate.IsBlocked, Is.True);
                presenter.Close();
                Assert.That(gate.IsBlocked, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SuccessfulPresenterLoadReleasesInputAndReopenRefreshesWithoutStaleSelection()
        {
            _slots.Add("alpha", true);
            var root = new GameObject("M6C load lifecycle");
            try
            {
                root.AddComponent<WorldMapSessionCoordinator>();
                PlayerInputGate gate = root.AddComponent<PlayerInputGate>();
                ProductionSaveLoadPresenter presenter = root.AddComponent<ProductionSaveLoadPresenter>();
                presenter.BindSlotOperations(_slots);
                presenter.Open(SaveLoadPanelMode.Load);
                presenter.LoadSelectedForTests();
                Assert.That(presenter.IsOpen, Is.False);
                Assert.That(gate.IsBlocked, Is.False);
                _slots.Remove("alpha");
                _slots.Add("beta", true);
                presenter.Open(SaveLoadPanelMode.Load);
                Assert.That(presenter.Controller.SelectedSlotId, Is.EqualTo("beta"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void LoaderCompositionCreatesSinglePresentationAndInputOwners()
        {
            var root = new GameObject("M6C composition");
            try
            {
                WorldObjectSectorLoader loader = root.AddComponent<WorldObjectSectorLoader>();
                loader.EnsureProductionPresentationComponents();
                loader.EnsureProductionPresentationComponents();
                Assert.That(root.GetComponents<ProductionSaveLoadPresenter>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponents<PlayerInputGate>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponents<ProductionDialoguePresenter>().Length, Is.EqualTo(1));
                Assert.That(root.GetComponents<ProductionJournalPresenter>().Length, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static SessionSaveSlotResult Result(SessionSaveSlotFailure failure,
            SessionLoadFailure sessionFailure = SessionLoadFailure.None)
            => new(failure, "detail", sessionFailure);

        private sealed class FakeSlots : ISessionSaveSlotOperations, ISessionSaveSlotPresentationOperations
        {
            private readonly List<SessionSaveSlotInfo> _entries = new();
            public int SaveCalls { get; private set; }
            public int LoadCalls { get; private set; }
            public int DeleteCalls { get; private set; }
            public int ListCalls { get; private set; }
            public string LastSlotId { get; private set; }
            public string LastDisplayName { get; private set; }
            public object ActiveSessionToken { get; } = new();
            public SessionSaveSlotResult SaveResult { get; set; } = Result(SessionSaveSlotFailure.None);
            public SessionSaveSlotResult LoadResult { get; set; } = Result(SessionSaveSlotFailure.None);
            public SessionSaveSlotResult DeleteResult { get; set; } = Result(SessionSaveSlotFailure.None);
            public SessionSaveSlotFailure ListFailure { get; set; }

            public void Add(string id, bool valid, string timestamp = "2026-09-13T20:15:30.0000000Z",
                string sector = "maps/test/1.sec", int level = 1, int version = 1, string error = null,
                string displayName = null, string pcName = null, string areaName = null,
                long gameTime = 0, string thumbnail = null)
            {
                _entries.Add(new SessionSaveSlotInfo
                {
                    SlotId = id,
                    IsValid = valid,
                    Error = valid ? null : error ?? "damaged",
                    Metadata = valid ? new SessionSaveSlotMetadata
                    {
                        SlotId = id,
                        SavedAtUtc = timestamp,
                        SessionFormat = SessionSaveService.FormatIdentifier,
                        SessionVersion = version,
                        CurrentMap = "maps/test",
                        SelectedSector = sector,
                        PcIdentity = "G_00000000_0000_0000_0000_000000000001",
                        PcLevel = level,
                        DisplayName = displayName,
                        PcName = pcName,
                        AreaName = areaName,
                        GameTimeMilliseconds = gameTime,
                        ThumbnailPngBase64 = thumbnail,
                    } : null,
                });
                _entries.Sort((left, right) => string.CompareOrdinal(left.SlotId, right.SlotId));
            }

            public void Remove(string id) => _entries.RemoveAll(entry => entry.SlotId == id);

            public SessionSaveSlotResult SaveSlot(string slotId)
                => SaveSlot(slotId, slotId);

            public SessionSaveSlotResult SaveSlot(string slotId, string displayName)
            {
                SaveCalls++;
                LastSlotId = slotId;
                LastDisplayName = displayName;
                if (!SaveResult.Succeeded) return SaveResult;
                Remove(slotId);
                Add(slotId, true, displayName: displayName);
                return SaveResult;
            }

            public SessionSaveSlotResult LoadSlot(string slotId)
            {
                LoadCalls++;
                LastSlotId = slotId;
                return LoadResult;
            }

            public SessionSaveSlotListResult ListSlots()
            {
                ListCalls++;
                return ListFailure == SessionSaveSlotFailure.None
                    ? new SessionSaveSlotListResult(SessionSaveSlotFailure.None, _entries.ToArray())
                    : new SessionSaveSlotListResult(ListFailure, Array.Empty<SessionSaveSlotInfo>(), "detail");
            }

            public SessionSaveSlotResult DeleteSlot(string slotId)
            {
                DeleteCalls++;
                LastSlotId = slotId;
                if (DeleteResult.Succeeded) Remove(slotId);
                return DeleteResult;
            }
        }
    }
}
