using System.Collections.Generic;

namespace AudioMW
{
    public struct LoudnessAuditEntry
    {
        public string AssetPath;
        public string ClipName;
        public bool Readable;
        public LoudnessResult Loudness;

        public double SuggestedOffsetDb(double targetLufs)
        {
            return Readable ? Loudness.OffsetToTarget(targetLufs) : 0.0;
        }
    }

    public static class LoudnessAuditReport
    {
        public const double DefaultToleranceDb = 3.0;

        public static List<LoudnessAuditEntry> FindOutliers(IReadOnlyList<LoudnessAuditEntry> entries, double targetLufs, double toleranceDb)
        {
            List<LoudnessAuditEntry> outliers = new List<LoudnessAuditEntry>();

            if (entries == null)
            {
                return outliers;
            }

            double tolerance = System.Math.Abs(toleranceDb);

            for (int i = 0; i < entries.Count; i++)
            {
                LoudnessAuditEntry entry = entries[i];

                if (!entry.Readable || !entry.Loudness.HasSignal)
                {
                    continue;
                }

                if (System.Math.Abs(entry.SuggestedOffsetDb(targetLufs)) > tolerance)
                {
                    outliers.Add(entry);
                }
            }

            return outliers;
        }

        public static double AverageLoudness(IReadOnlyList<LoudnessAuditEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return double.NegativeInfinity;
            }

            double sum = 0.0;
            int count = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                LoudnessAuditEntry entry = entries[i];

                if (entry.Readable && entry.Loudness.HasSignal)
                {
                    sum += entry.Loudness.IntegratedLufs;
                    count++;
                }
            }

            return count == 0 ? double.NegativeInfinity : sum / count;
        }

        public static int CountClipping(IReadOnlyList<LoudnessAuditEntry> entries, double ceilingDb)
        {
            if (entries == null)
            {
                return 0;
            }

            int count = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                LoudnessAuditEntry entry = entries[i];

                if (entry.Readable && entry.Loudness.TruePeakDb > ceilingDb)
                {
                    count++;
                }
            }

            return count;
        }

        public static string ToCsv(IReadOnlyList<LoudnessAuditEntry> entries, double targetLufs)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine("path,clip,readable,integrated_lufs,short_term_max,sample_peak_db,true_peak_db,duration_s,suggested_offset_db");

            if (entries == null)
            {
                return builder.ToString();
            }

            for (int i = 0; i < entries.Count; i++)
            {
                LoudnessAuditEntry entry = entries[i];

                builder.AppendLine(string.Join(",", new[]
                {
                    Escape(entry.AssetPath),
                    Escape(entry.ClipName),
                    entry.Readable ? "yes" : "no",
                    Format(entry.Loudness.IntegratedLufs),
                    Format(entry.Loudness.ShortTermMaxLufs),
                    Format(entry.Loudness.SamplePeakDb),
                    Format(entry.Loudness.TruePeakDb),
                    entry.Loudness.DurationSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    Format(entry.SuggestedOffsetDb(targetLufs))
                }));
            }

            return builder.ToString();
        }

        private static string Format(double value)
        {
            if (double.IsNegativeInfinity(value) || double.IsNaN(value))
            {
                return "-inf";
            }

            return value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Contains(",") ? "\"" + value + "\"" : value;
        }
    }
}
