using DansToolbox.EditorTools.BetterConsole;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using PackageManagerInfo = UnityEditor.PackageManager.PackageInfo;

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

        [Test]
        public void EntrySelection_SupportsRangeAndActionKeyToggling()
        {
            long[] visible = { 10, 20, 30, 40, 50 };

            Assert.That(
                BetterConsoleWindow.CalculateEntrySelection(visible, new long[] { 20 }, 20, 40, true, false),
                Is.EqualTo(new long[] { 20, 30, 40 }));
            Assert.That(
                BetterConsoleWindow.CalculateEntrySelection(visible, new long[] { 20, 30, 40 }, 20, 30, false, true),
                Is.EqualTo(new long[] { 20, 40 }));
            Assert.That(
                BetterConsoleWindow.CalculateEntrySelection(visible, new long[] { 20, 40 }, 20, 50, true, true),
                Is.EqualTo(new long[] { 20, 30, 40, 50 }));
        }

        [Test]
        public void ClipboardFormatting_PreservesVisibleOrderAndFullEntries()
        {
            BetterConsoleEntry first = Entry();
            first.message = "First";
            first.stackTrace = "First.Frame";
            BetterConsoleEntry second = Entry();
            second.message = "Second";
            second.stackTrace = string.Empty;

            Assert.That(
                BetterConsoleWindow.FormatEntriesForClipboard(new[] { first, second }),
                Is.EqualTo("First\nFirst.Frame\n\nSecond"));
        }

        [Test]
        public void NativeSnapshot_ReconcilesAfterReloadOrNativeClear()
        {
            Assert.That(BetterConsoleNativeBridge.RequiresReconciliation(false, 0, 12), Is.True);
            Assert.That(BetterConsoleNativeBridge.RequiresReconciliation(true, 12, 0), Is.True);
            Assert.That(BetterConsoleNativeBridge.RequiresReconciliation(true, 12, 13), Is.False);
        }

        [Test]
        public void Store_AddRangePublishesOneRevisionAndIndexesEveryNativeLine()
        {
            BetterConsoleStore.Clear();
            try
            {
                int revision = BetterConsoleStore.Revision;
                BetterConsoleEntry first = Entry();
                first.message = "First batch entry";
                first.nativeLineIndex = 7001;
                BetterConsoleEntry second = Entry();
                second.message = "Second batch entry";
                second.nativeLineIndex = 7002;

                BetterConsoleStore.AddRange(new[] { first, second });

                Assert.That(BetterConsoleStore.Revision, Is.EqualTo(revision + 1));
                Assert.That(BetterConsoleStore.Entries, Has.Count.EqualTo(2));
                Assert.That(BetterConsoleStore.Entries[0], Is.SameAs(first));
                Assert.That(BetterConsoleStore.Entries[1], Is.SameAs(second));
                Assert.That(BetterConsoleStore.ContainsNativeLine(7001), Is.True);
                Assert.That(BetterConsoleStore.ContainsNativeLine(7002), Is.True);
            }
            finally
            {
                BetterConsoleStore.Clear();
            }
        }

        [Test]
        public void Store_NativeReconciliationPreservesExistingEntryIdentityAndOrder()
        {
            BetterConsoleStore.Clear();
            try
            {
                BetterConsoleEntry first = Entry();
                first.message = "Repeated native message";
                first.nativeLineIndex = 8101;
                BetterConsoleEntry second = Entry();
                second.message = "Repeated native message";
                second.nativeLineIndex = 8102;
                BetterConsoleStore.AddRange(new[] { first, second });
                long firstId = first.id;
                long secondId = second.id;

                BetterConsoleEntry nativeSecond = Entry();
                nativeSecond.message = second.message;
                nativeSecond.nativeLineIndex = 8102;
                BetterConsoleEntry nativeFirst = Entry();
                nativeFirst.message = first.message;
                nativeFirst.nativeLineIndex = 8101;
                BetterConsoleStore.ReconcileNativeSnapshot(new[] { nativeSecond, nativeFirst });

                Assert.That(BetterConsoleStore.Entries[0].id, Is.EqualTo(secondId));
                Assert.That(BetterConsoleStore.Entries[1].id, Is.EqualTo(firstId));
                Assert.That(BetterConsoleStore.Entries[0], Is.SameAs(second));
                Assert.That(BetterConsoleStore.Entries[1], Is.SameAs(first));
            }
            finally
            {
                BetterConsoleStore.Clear();
            }
        }

        [Test]
        public void TargetQuery_MatchesContextComponentsAndAssetPathsAsAlternatives()
        {
            GameObject context = new GameObject("Diagnostic Target");
            BoxCollider component = context.AddComponent<BoxCollider>();
            try
            {
                BetterConsoleEntry contextEntry = Entry();
                contextEntry.contextInstanceId = component.GetInstanceID();
                string contextQuery = BetterConsoleDiagnosticBridge.BuildTargetQuery(
                    new UnityEngine.Object[] { context },
                    null);
                Assert.That(BetterConsoleQuery.Compile(contextQuery).Matches(contextEntry), Is.True);

                BetterConsoleEntry assetEntry = Entry();
                assetEntry.file = "Assets/Client.cs";
                string assetQuery = BetterConsoleDiagnosticBridge.BuildTargetQuery(
                    null,
                    new[] { "Assets/Client.cs", "Assets/Other.asset" });
                Assert.That(BetterConsoleQuery.Compile(assetQuery).Matches(assetEntry), Is.True);
                Assert.That(BetterConsoleQuery.Compile("target:\"file=Assets/\"").Matches(assetEntry), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void DiagnosticPaths_RejectMalformedAndUnrelatedAbsolutePaths()
        {
            Assert.That(BetterConsoleDiagnosticBridge.NormalizeAssetPath("bad\0path"), Is.Empty);
            Assert.That(
                BetterConsoleDiagnosticBridge.NormalizeAssetPath(
                    Path.Combine(Path.GetTempPath(), "DansToolbox-Unrelated", "Broken.cs")),
                Is.Empty);
        }

        [Test]
        public void DiagnosticPaths_MapProjectAndPackageSourcesToAssetDatabasePaths()
        {
            Assert.That(
                BetterConsoleDiagnosticBridge.NormalizeAssetPath(
                    Path.Combine(Application.dataPath, "Client.cs")),
                Is.EqualTo("Assets/Client.cs"));

            PackageManagerInfo package = PackageManagerInfo.FindForPackageName("com.dans.toolbox");
            Assert.That(package, Is.Not.Null);
            Assert.That(
                BetterConsoleDiagnosticBridge.NormalizeAssetPath(
                    Path.Combine(package.resolvedPath, "Editor", "BetterConsole", "BetterConsoleDiagnosticBridge.cs")),
                Is.EqualTo(package.assetPath + "/Editor/BetterConsole/BetterConsoleDiagnosticBridge.cs"));
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
