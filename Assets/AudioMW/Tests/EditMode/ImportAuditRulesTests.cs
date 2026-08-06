using System.Collections.Generic;
using NUnit.Framework;

namespace AudioMW.Tests
{
    public sealed class ImportAuditRulesTests
    {
        [Test]
        public void ShortStreamedClipIsFlagged()
        {
            ClipImportInfo info = Make(1f, ImportAuditRules.StreamingLoadType);

            List<ImportIssue> issues = ImportAuditRules.Evaluate(info);

            Assert.IsTrue(Contains(issues, ImportIssueKind.LoadTypeTooHeavy));
        }

        [Test]
        public void LongDecompressedClipIsFlagged()
        {
            ClipImportInfo info = Make(60f, ImportAuditRules.DecompressLoadType);

            Assert.IsTrue(Contains(ImportAuditRules.Evaluate(info), ImportIssueKind.UncompressedLongClip));
        }

        [Test]
        public void MediumStreamedClipIsFlagged()
        {
            ClipImportInfo info = Make(6f, ImportAuditRules.StreamingLoadType);

            Assert.IsTrue(Contains(ImportAuditRules.Evaluate(info), ImportIssueKind.LoadTypeTooLight));
        }

        [Test]
        public void CorrectlyConfiguredShortClipHasNoIssues()
        {
            ClipImportInfo info = Make(1f, ImportAuditRules.DecompressLoadType);
            info.Channels = 1;
            info.UsedSpatially = false;

            Assert.AreEqual(0, ImportAuditRules.Evaluate(info).Count);
        }

        [Test]
        public void StereoSpatialClipIsFlagged()
        {
            ClipImportInfo info = Make(1f, ImportAuditRules.DecompressLoadType);
            info.Channels = 2;
            info.UsedSpatially = true;
            info.ForceToMono = false;

            Assert.IsTrue(Contains(ImportAuditRules.Evaluate(info), ImportIssueKind.StereoSpatialCandidate));
        }

        [Test]
        public void ForceToMonoClearsSpatialWarning()
        {
            ClipImportInfo info = Make(1f, ImportAuditRules.DecompressLoadType);
            info.Channels = 2;
            info.UsedSpatially = true;
            info.ForceToMono = true;

            Assert.IsFalse(Contains(ImportAuditRules.Evaluate(info), ImportIssueKind.StereoSpatialCandidate));
        }

        [Test]
        public void PreloadOnStreamingClipIsFlagged()
        {
            ClipImportInfo info = Make(60f, ImportAuditRules.StreamingLoadType);
            info.PreloadAudioData = true;

            Assert.IsTrue(Contains(ImportAuditRules.Evaluate(info), ImportIssueKind.PreloadOnStreamingClip));
        }

        [Test]
        public void BackgroundLoadOnShortClipIsFlagged()
        {
            ClipImportInfo info = Make(0.5f, ImportAuditRules.DecompressLoadType);
            info.LoadInBackground = true;

            Assert.IsTrue(Contains(ImportAuditRules.Evaluate(info), ImportIssueKind.BackgroundLoadOnShortClip));
        }

        [Test]
        public void HighSampleRateIsFlagged()
        {
            ClipImportInfo info = Make(1f, ImportAuditRules.DecompressLoadType);
            info.Frequency = 96000;

            Assert.IsTrue(Contains(ImportAuditRules.Evaluate(info), ImportIssueKind.HighSampleRate));
        }

        [Test]
        public void SuggestedLoadTypeFollowsLength()
        {
            Assert.AreEqual(ImportAuditRules.DecompressLoadType, ImportAuditRules.SuggestLoadType(1f));
            Assert.AreEqual(ImportAuditRules.CompressedInMemoryLoadType, ImportAuditRules.SuggestLoadType(8f));
            Assert.AreEqual(ImportAuditRules.StreamingLoadType, ImportAuditRules.SuggestLoadType(120f));
        }

        [Test]
        public void StreamingClipsCostNoEstimatedMemory()
        {
            ClipImportInfo info = Make(60f, ImportAuditRules.StreamingLoadType);
            info.Samples = 48000 * 60;

            Assert.AreEqual(0L, info.EstimatedMemoryBytes);
        }

        [Test]
        public void ForceToMonoHalvesEstimatedMemory()
        {
            ClipImportInfo stereo = Make(1f, ImportAuditRules.DecompressLoadType);
            stereo.Samples = 48000;
            stereo.Channels = 2;

            ClipImportInfo mono = stereo;
            mono.ForceToMono = true;

            Assert.AreEqual(stereo.EstimatedMemoryBytes / 2L, mono.EstimatedMemoryBytes);
        }

        [Test]
        public void CompressedInMemoryCostsLessThanDecompressed()
        {
            ClipImportInfo decompressed = Make(5f, ImportAuditRules.DecompressLoadType);
            decompressed.Samples = 48000 * 5;

            ClipImportInfo compressed = decompressed;
            compressed.LoadType = ImportAuditRules.CompressedInMemoryLoadType;

            Assert.Less(compressed.EstimatedMemoryBytes, decompressed.EstimatedMemoryBytes);
        }

        [Test]
        public void TotalMemorySumsClips()
        {
            ClipImportInfo a = Make(1f, ImportAuditRules.DecompressLoadType);
            a.Samples = 1000;
            a.Channels = 1;

            ClipImportInfo b = a;

            List<ClipImportInfo> clips = new List<ClipImportInfo> { a, b };

            Assert.AreEqual(a.EstimatedMemoryBytes * 2L, ImportAuditRules.TotalEstimatedMemory(clips));
            Assert.AreEqual(0L, ImportAuditRules.TotalEstimatedMemory(null));
        }

        private static bool Contains(List<ImportIssue> issues, ImportIssueKind kind)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private static ClipImportInfo Make(float seconds, string loadType)
        {
            ClipImportInfo info = new ClipImportInfo();
            info.Name = "clip";
            info.AssetPath = "Assets/clip.wav";
            info.LengthSeconds = seconds;
            info.Channels = 1;
            info.Frequency = 44100;
            info.Samples = (int)(44100 * seconds);
            info.LoadType = loadType;
            info.CompressionFormat = "Vorbis";
            return info;
        }
    }
}
