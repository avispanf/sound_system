using System.Collections.Generic;

namespace AudioMW
{
    public enum ImportIssueKind
    {
        LoadTypeTooHeavy = 0,
        LoadTypeTooLight = 1,
        UncompressedLongClip = 2,
        StereoSpatialCandidate = 3,
        PreloadOnStreamingClip = 4,
        BackgroundLoadOnShortClip = 5,
        HighSampleRate = 6
    }

    public struct ImportIssue
    {
        public ImportIssueKind Kind;
        public string Message;
        public string Suggestion;
    }

    public struct ClipImportInfo
    {
        public string Name;
        public string AssetPath;
        public float LengthSeconds;
        public int Channels;
        public int Frequency;
        public int Samples;
        public string LoadType;
        public string CompressionFormat;
        public bool ForceToMono;
        public bool PreloadAudioData;
        public bool LoadInBackground;
        public bool UsedSpatially;

        public long EstimatedMemoryBytes
        {
            get
            {
                if (LoadType == ImportAuditRules.StreamingLoadType)
                {
                    return 0L;
                }

                int channels = ForceToMono ? 1 : (Channels > 0 ? Channels : 1);
                long bytes = (long)Samples * channels * 4L;

                if (LoadType == ImportAuditRules.CompressedInMemoryLoadType)
                {
                    bytes /= 8L;
                }

                return bytes;
            }
        }
    }

    public static class ImportAuditRules
    {
        public const string DecompressLoadType = "DecompressOnLoad";
        public const string CompressedInMemoryLoadType = "CompressedInMemory";
        public const string StreamingLoadType = "Streaming";

        public const float ShortClipSeconds = 2f;
        public const float LongClipSeconds = 15f;
        public const int HighSampleRate = 48000;

        public static List<ImportIssue> Evaluate(ClipImportInfo info)
        {
            List<ImportIssue> issues = new List<ImportIssue>();

            if (info.LengthSeconds <= ShortClipSeconds && info.LoadType == StreamingLoadType)
            {
                issues.Add(new ImportIssue
                {
                    Kind = ImportIssueKind.LoadTypeTooHeavy,
                    Message = "short clip is streamed, which costs a disk read per play",
                    Suggestion = DecompressLoadType
                });
            }

            if (info.LengthSeconds >= LongClipSeconds && info.LoadType == DecompressLoadType)
            {
                issues.Add(new ImportIssue
                {
                    Kind = ImportIssueKind.UncompressedLongClip,
                    Message = "long clip is decompressed into memory",
                    Suggestion = StreamingLoadType
                });
            }

            if (info.LengthSeconds > ShortClipSeconds && info.LengthSeconds < LongClipSeconds && info.LoadType == StreamingLoadType)
            {
                issues.Add(new ImportIssue
                {
                    Kind = ImportIssueKind.LoadTypeTooLight,
                    Message = "medium clip is streamed where compressed in memory usually fits better",
                    Suggestion = CompressedInMemoryLoadType
                });
            }

            if (info.UsedSpatially && info.Channels > 1 && !info.ForceToMono)
            {
                issues.Add(new ImportIssue
                {
                    Kind = ImportIssueKind.StereoSpatialCandidate,
                    Message = "spatial clip is stereo, so panning is wasted and memory is doubled",
                    Suggestion = "enable Force To Mono"
                });
            }

            if (info.LoadType == StreamingLoadType && info.PreloadAudioData)
            {
                issues.Add(new ImportIssue
                {
                    Kind = ImportIssueKind.PreloadOnStreamingClip,
                    Message = "streaming clip has preload enabled, which defeats streaming",
                    Suggestion = "disable Preload Audio Data"
                });
            }

            if (info.LengthSeconds <= ShortClipSeconds && info.LoadInBackground)
            {
                issues.Add(new ImportIssue
                {
                    Kind = ImportIssueKind.BackgroundLoadOnShortClip,
                    Message = "short clip loads in background and may miss its first play",
                    Suggestion = "disable Load In Background"
                });
            }

            if (info.Frequency > HighSampleRate)
            {
                issues.Add(new ImportIssue
                {
                    Kind = ImportIssueKind.HighSampleRate,
                    Message = "sample rate above 48 kHz rarely survives the output device",
                    Suggestion = "resample to 48 kHz or lower"
                });
            }

            return issues;
        }

        public static string SuggestLoadType(float lengthSeconds)
        {
            if (lengthSeconds <= ShortClipSeconds)
            {
                return DecompressLoadType;
            }

            return lengthSeconds >= LongClipSeconds ? StreamingLoadType : CompressedInMemoryLoadType;
        }

        public static long TotalEstimatedMemory(IReadOnlyList<ClipImportInfo> clips)
        {
            if (clips == null)
            {
                return 0L;
            }

            long total = 0L;

            for (int i = 0; i < clips.Count; i++)
            {
                total += clips[i].EstimatedMemoryBytes;
            }

            return total;
        }
    }
}
