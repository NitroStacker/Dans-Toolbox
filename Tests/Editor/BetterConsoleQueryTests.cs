using DansToolbox.EditorTools.BetterConsole;
using NUnit.Framework;
using UnityEngine;

namespace DansToolbox.Editor.Tests
{
    internal sealed class BetterConsoleQueryTests
    {
        [Test]
        public void Query_MatchesFieldsPhrasesAndExclusions()
        {
            BetterConsoleEntry entry = Entry();
            Assert.That(BetterConsoleQuery.Compile("sev:error channel:NET \"connection lost\" -source:Remote").Matches(entry), Is.True);
            Assert.That(BetterConsoleQuery.Compile("has:stack is:structured file:Client.cs").Matches(entry), Is.True);
            Assert.That(BetterConsoleQuery.Compile("sev:warning").Matches(entry), Is.False);
        }

        [Test]
        public void Query_RegexIsOptionalAndMalformedRegexIsSafe()
        {
            BetterConsoleEntry entry = Entry();
            Assert.That(BetterConsoleQuery.Compile("/connection\\s+lost/").Matches(entry), Is.True);
            BetterConsoleQuery invalid = BetterConsoleQuery.Compile("/[unterminated/");
            Assert.That(invalid.IsValid, Is.False);
            Assert.That(invalid.Matches(entry), Is.False);
        }

        [Test]
        public void Signature_NormalizesChangingIdsNumbersAndAddresses()
        {
            BetterConsoleEntry first = Entry();
            first.message = "Enemy 42 failed at 0xABCDEF12 with 5.7";
            BetterConsoleEntry second = Entry();
            second.message = "Enemy 99 failed at 0xDEADBEEF with 8.1";
            Assert.That(BetterConsoleClassification.Signature(first), Is.EqualTo(BetterConsoleClassification.Signature(second)));
        }

        [TestCase("Shader error in Assets/Fx/Test.shader", BetterConsoleCategory.Shader)]
        [TestCase("Failed to import texture", BetterConsoleCategory.Import)]
        [TestCase("Socket connection closed", BetterConsoleCategory.Network)]
        [TestCase("Test failed in NUnit", BetterConsoleCategory.Test)]
        public void Category_RecognizesCommonUnityWork(string message, BetterConsoleCategory expected)
        {
            Assert.That(
                BetterConsoleClassification.Categorize(message, string.Empty, string.Empty, BetterConsoleSessionKind.Editor),
                Is.EqualTo(expected));
        }

        [Test]
        public void DetailPlacement_AdaptsWithoutCrushingNarrowPanes()
        {
            Assert.That(
                BetterConsoleWindow.CalculateDetailPlacement(new Vector2(1200f, 500f), true, true),
                Is.EqualTo(BetterConsoleDetailPlacement.Right));
            Assert.That(
                BetterConsoleWindow.CalculateDetailPlacement(new Vector2(600f, 500f), true, true),
                Is.EqualTo(BetterConsoleDetailPlacement.Bottom));
            Assert.That(
                BetterConsoleWindow.CalculateDetailPlacement(new Vector2(420f, 300f), true, true),
                Is.EqualTo(BetterConsoleDetailPlacement.Hidden));
            Assert.That(
                BetterConsoleWindow.CalculateDetailPlacement(new Vector2(1200f, 500f), true, false),
                Is.EqualTo(BetterConsoleDetailPlacement.Hidden));
        }

        [Test]
        public void SearchFocus_ReleasesOnlyForPointerDownOutsideTheField()
        {
            Rect searchRect = new Rect(100f, 10f, 200f, 24f);

            Assert.That(
                BetterConsoleWindow.ShouldReleaseSearchFocus(searchRect, new Vector2(50f, 50f), true, EventType.MouseDown),
                Is.True);
            Assert.That(
                BetterConsoleWindow.ShouldReleaseSearchFocus(searchRect, new Vector2(150f, 20f), true, EventType.MouseDown),
                Is.False);
            Assert.That(
                BetterConsoleWindow.ShouldReleaseSearchFocus(searchRect, new Vector2(50f, 50f), true, EventType.MouseMove),
                Is.False);
            Assert.That(
                BetterConsoleWindow.ShouldReleaseSearchFocus(searchRect, new Vector2(50f, 50f), false, EventType.MouseDown),
                Is.False);
        }

        private static BetterConsoleEntry Entry()
        {
            return new BetterConsoleEntry
            {
                severity = BetterConsoleSeverity.Error,
                category = BetterConsoleCategory.Network,
                message = "Connection lost while loading player",
                stackTrace = "Client.Connect () (at Assets/Client.cs:42)",
                file = "Assets/Client.cs",
                source = "Editor",
                channel = "NET",
                structured = true
            };
        }
    }
}
