using System.Collections.Generic;
using AudioMW.Editor;
using NUnit.Framework;

namespace AudioMW.Tests
{
    public sealed class LiveTweakSnapshotTests
    {
        [Test]
        public void AddStoresEntry()
        {
            LiveTweakSnapshot snapshot = new LiveTweakSnapshot();
            snapshot.Add("guid-a", "{\"volume\":1}");

            string json;
            Assert.IsTrue(snapshot.TryGet("guid-a", out json));
            Assert.AreEqual("{\"volume\":1}", json);
        }

        [Test]
        public void AddOverwritesExistingGuid()
        {
            LiveTweakSnapshot snapshot = new LiveTweakSnapshot();
            snapshot.Add("guid-a", "first");
            snapshot.Add("guid-a", "second");

            string json;
            snapshot.TryGet("guid-a", out json);

            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual("second", json);
        }

        [Test]
        public void EmptyGuidIsIgnored()
        {
            LiveTweakSnapshot snapshot = new LiveTweakSnapshot();
            snapshot.Add(null, "value");
            snapshot.Add(string.Empty, "value");

            Assert.AreEqual(0, snapshot.Count);
        }

        [Test]
        public void MissingGuidReturnsFalse()
        {
            LiveTweakSnapshot snapshot = new LiveTweakSnapshot();

            string json;
            Assert.IsFalse(snapshot.TryGet("nope", out json));
            Assert.IsNull(json);
        }

        [Test]
        public void FindChangedDetectsModifiedJson()
        {
            LiveTweakSnapshot before = new LiveTweakSnapshot();
            before.Add("a", "{\"v\":1}");
            before.Add("b", "{\"v\":2}");

            LiveTweakSnapshot after = new LiveTweakSnapshot();
            after.Add("a", "{\"v\":9}");
            after.Add("b", "{\"v\":2}");

            List<string> changed = LiveTweakSnapshot.FindChanged(before, after);

            Assert.AreEqual(1, changed.Count);
            Assert.AreEqual("a", changed[0]);
        }

        [Test]
        public void FindChangedIgnoresAssetsCreatedDuringPlay()
        {
            LiveTweakSnapshot before = new LiveTweakSnapshot();
            before.Add("a", "{\"v\":1}");

            LiveTweakSnapshot after = new LiveTweakSnapshot();
            after.Add("a", "{\"v\":1}");
            after.Add("new", "{\"v\":5}");

            List<string> changed = LiveTweakSnapshot.FindChanged(before, after);

            Assert.AreEqual(0, changed.Count);
        }

        [Test]
        public void FindChangedHandlesNullSnapshots()
        {
            Assert.AreEqual(0, LiveTweakSnapshot.FindChanged(null, new LiveTweakSnapshot()).Count);
            Assert.AreEqual(0, LiveTweakSnapshot.FindChanged(new LiveTweakSnapshot(), null).Count);
        }

        [Test]
        public void ClearRemovesAllEntries()
        {
            LiveTweakSnapshot snapshot = new LiveTweakSnapshot();
            snapshot.Add("a", "1");
            snapshot.Add("b", "2");
            snapshot.Clear();

            Assert.AreEqual(0, snapshot.Count);
        }

        [Test]
        public void CaptureReturnsSnapshotWithoutThrowing()
        {
            Assert.DoesNotThrow(() => LiveTweakTracker.Capture());
        }
    }
}
