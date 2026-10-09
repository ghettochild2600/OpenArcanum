using System;
using System.Collections.Generic;
using System.Linq;
using Arcanum.Runtime.UI;
using NUnit.Framework;
using UnityEngine;

namespace Arcanum.Formats.Tests
{
    [Category("UiF2LogbookPresentation")]
    public sealed class UiF2LogbookPresentationTests
    {
        [Test]
        public void SourceWindowAndPageGeometryRemainFixedAtEightHundredBySixHundred()
        {
            Assert.That(RetailLogbookLayout.ScreenRect(800, 600), Is.EqualTo(new Rect(0, 41, 800, 400)));
            Assert.That(RetailLogbookLayout.LeftContent, Is.EqualTo(new Rect(222, 72, 215, 270)));
            Assert.That(RetailLogbookLayout.RightContent, Is.EqualTo(new Rect(468, 72, 215, 270)));
            Assert.That(RetailLogbookLayout.PageLineCapacity, Is.EqualTo(18));
        }

        [TestCase(1024, 768)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(3840, 2160)]
        public void SourceWindowCentersWithoutScalingAtTargetResolutions(int width, int height)
        {
            Rect rect = RetailLogbookLayout.ScreenRect(width, height);
            Assert.That(rect.size, Is.EqualTo(new Vector2(800, 400)));
            Assert.That(rect.x, Is.EqualTo((width - 800) * .5f));
            Assert.That(rect.y, Is.EqualTo(41 + (height - 600) * .5f));
        }

        [Test]
        public void SevenTabsUseExactSourceOrderIdsAndPositions()
        {
            Assert.That(RetailLogbookLayout.Tabs.Select(value => value.Tab), Is.EqualTo(new[]
            {
                RetailLogbookTab.RumorsAndNotes, RetailLogbookTab.Quests, RetailLogbookTab.Reputations,
                RetailLogbookTab.BlessingsAndCurses, RetailLogbookTab.KillsAndInjuries,
                RetailLogbookTab.Background, RetailLogbookTab.Keys,
            }));
            Assert.That(RetailLogbookLayout.Tabs.Select(value => value.SourceId),
                Is.EqualTo(new[] { 265, 267, 271, 266, 270, 268, 269 }));
            Assert.That(RetailLogbookLayout.Tabs.Select(value => value.Position.y),
                Is.EqualTo(new[] { 83f, 129f, 175f, 221f, 267f, 313f, 359f }));
        }

        [Test]
        public void ButtonStatesSelectFramesWithoutChangingPaletteOrRotation()
        {
            Assert.That(RetailLogbookLayout.ButtonFrame(267, 1).ToString(), Is.EqualTo("ui:0267:p00:r00:f001"));
            Assert.That(RetailLogbookLayout.ButtonFrame(273, 2).ToString(), Is.EqualTo("ui:0273:p00:r00:f002"));
        }

        [Test]
        public void WordWrappingUsesMeasuredWidthAndPreservesParagraphBreaks()
        {
            IReadOnlyList<string> lines = RetailLogbookPaginator.Wrap("one two three\nfour", 7, _ => 1);
            Assert.That(lines, Is.EqualTo(new[] { "one two", "three", "four" }));
        }

        [Test]
        public void SpreadPackerFillsLeftThenRightAndReturnsLastCompleteEntry()
        {
            var blocks = new List<RetailLogbookQuestBlock>();
            for (int index = 0; index < 6; index++)
                blocks.Add(new RetailLogbookQuestBlock(default, new[] { "a", "b", "c" }));
            Assert.That(RetailLogbookPaginator.LastVisibleEntry(blocks, 0, 6), Is.EqualTo(1));
            Assert.That(RetailLogbookPaginator.LastVisibleEntry(blocks, 4, 6), Is.EqualTo(5));
        }

        [Test]
        public void InvalidPaginatorRequestsFailClosed()
        {
            var blocks = new List<RetailLogbookQuestBlock>
                { new(default, new[] { "entry" }) };
            Assert.Throws<ArgumentOutOfRangeException>(() => RetailLogbookPaginator.LastVisibleEntry(blocks, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => RetailLogbookPaginator.LastVisibleEntry(blocks, 0, 0));
        }
    }
}
