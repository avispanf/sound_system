using System.Collections.Generic;
using NUnit.Framework;

namespace AudioMW.Tests
{
    public sealed class LoudnessAuditReportTests
    {
        [Test]
        public void OutliersRespectTolerance()
        {
            List<LoudnessAuditEntry> entries = new List<LoudnessAuditEntry>
            {
                MakeEntry("onTarget", -23.0),
                MakeEntry("slightlyOff", -25.0),
                MakeEntry("wayOff", -12.0)
            };

            List<LoudnessAuditEntry> outliers = LoudnessAuditReport.FindOutliers(entries, -23.0, 3.0);

            Assert.AreEqual(1, outliers.Count);
            Assert.AreEqual("wayOff", outliers[0].ClipName);
        }

        [Test]
        public void UnreadableEntriesAreNeverOutliers()
        {
            List<LoudnessAuditEntry> entries = new List<LoudnessAuditEntry>
            {
                new LoudnessAuditEntry { ClipName = "compressed", Readable = false }
            };

            Assert.AreEqual(0, LoudnessAuditReport.FindOutliers(entries, -23.0, 1.0).Count);
        }

        [Test]
        public void NegativeToleranceIsTreatedAsMagnitude()
        {
            List<LoudnessAuditEntry> entries = new List<LoudnessAuditEntry> { MakeEntry("off", -30.0) };

            Assert.AreEqual(1, LoudnessAuditReport.FindOutliers(entries, -23.0, -3.0).Count);
        }

        [Test]
        public void AverageIgnoresUnreadableEntries()
        {
            List<LoudnessAuditEntry> entries = new List<LoudnessAuditEntry>
            {
                MakeEntry("a", -20.0),
                MakeEntry("b", -30.0),
                new LoudnessAuditEntry { ClipName = "skip", Readable = false }
            };

            Assert.AreEqual(-25.0, LoudnessAuditReport.AverageLoudness(entries), 1e-9);
        }

        [Test]
        public void AverageOfEmptySetIsNegativeInfinity()
        {
            Assert.IsTrue(double.IsNegativeInfinity(LoudnessAuditReport.AverageLoudness(new List<LoudnessAuditEntry>())));
            Assert.IsTrue(double.IsNegativeInfinity(LoudnessAuditReport.AverageLoudness(null)));
        }

        [Test]
        public void ClippingCountUsesTruePeak()
        {
            List<LoudnessAuditEntry> entries = new List<LoudnessAuditEntry>
            {
                MakeEntry("hot", -18.0, -0.2),
                MakeEntry("safe", -18.0, -6.0)
            };

            Assert.AreEqual(1, LoudnessAuditReport.CountClipping(entries, -1.0));
        }

        [Test]
        public void SuggestedOffsetMovesTowardTarget()
        {
            LoudnessAuditEntry entry = MakeEntry("quiet", -30.0);

            Assert.AreEqual(7.0, entry.SuggestedOffsetDb(-23.0), 1e-9);
        }

        [Test]
        public void CsvContainsHeaderAndRows()
        {
            List<LoudnessAuditEntry> entries = new List<LoudnessAuditEntry> { MakeEntry("clip", -22.0) };

            string csv = LoudnessAuditReport.ToCsv(entries, -23.0);

            StringAssert.Contains("integrated_lufs", csv);
            StringAssert.Contains("clip", csv);
        }

        [Test]
        public void CsvHandlesNullInput()
        {
            string csv = LoudnessAuditReport.ToCsv(null, -23.0);

            StringAssert.Contains("path,clip", csv);
        }

        private static LoudnessAuditEntry MakeEntry(string name, double lufs, double truePeak = -6.0)
        {
            LoudnessResult result = new LoudnessResult();
            result.HasSignal = true;
            result.IntegratedLufs = lufs;
            result.TruePeakDb = truePeak;
            result.SamplePeakDb = truePeak;
            result.DurationSeconds = 1.0;

            return new LoudnessAuditEntry
            {
                ClipName = name,
                AssetPath = "Assets/" + name + ".wav",
                Readable = true,
                Loudness = result
            };
        }
    }
}
