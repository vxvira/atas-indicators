using System;
using System.Collections.Generic;
using System.Linq;
using VolumeProfileX;
using Xunit;

namespace VolumeProfileX.Tests
{
    public class CalcTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);

        private static List<LevelIn> L(params (long tick, double vol)[] a)
            => a.Select(x => new LevelIn(x.tick, x.vol, x.vol / 2, x.vol / 2, (int)x.vol)).ToList();

        private static ProfileSnapshot Snap(ProfileWindow w, int rpb = 1, double va = 70, double hvn = 85, double lvn = 15)
        {
            var s = new ProfileSnapshot(); s.Update(w, rpb, va, hvn, lvn, 0.25); return s;
        }

        private static ProfileWindow SingleBar(params double[] vols)
        {
            var w = new ProfileWindow(); w.Configure(24, null);
            w.Feed(0, T0, vols.Select((v, i) => new LevelIn(100 + i, v, v / 2, v / 2, 1)).ToList());
            return w;
        }

        [Fact]
        public void Poc_is_max_volume_row()
        {
            var s = Snap(SingleBar(1, 2, 5, 8, 10, 7, 4, 2, 1));
            Assert.Equal(4, s.PocRow);
            Assert.Equal(40, s.Total);
        }

        [Fact]
        public void Poc_tie_prefers_row_nearest_middle()
        {
            var s = Snap(SingleBar(10, 1, 1, 1, 1, 1, 1, 1, 10, 1, 10));
            Assert.Equal(8, s.PocRow);
        }

        [Fact]
        public void ValueArea_two_row_lookahead_hand_computed()
        {
            var s = Snap(SingleBar(1, 2, 5, 8, 10, 7, 4, 2, 1), va: 70);
            Assert.Equal(2, s.ValRow);
            Assert.Equal(6, s.VahRow);
            for (int r = 0; r < s.RowCount; r++)
                Assert.Equal(r >= 2 && r <= 6, (s.Flags[r] & RowFlags.InValueArea) != 0);
        }

        [Fact]
        public void ValueArea_100_percent_covers_everything_and_tiny_is_poc_only()
        {
            var w = SingleBar(1, 2, 5, 8, 10, 7, 4, 2, 1);
            var s = Snap(w, va: 100);
            Assert.Equal(0, s.ValRow); Assert.Equal(8, s.VahRow);
            s = Snap(w, va: 1);
            Assert.Equal(4, s.ValRow); Assert.Equal(4, s.VahRow);
        }

        [Fact]
        public void ValueArea_at_edge_takes_single_remaining_row()
        {
            var s = Snap(SingleBar(10, 3, 1), va: 90);
            Assert.Equal(0, s.ValRow); Assert.Equal(2, s.VahRow);
        }

        [Fact]
        public void Row_bucketing_aligns_to_absolute_tick_multiples_including_negatives()
        {
            var w = new ProfileWindow(); w.Configure(24, null);
            var lv = new List<LevelIn>();
            for (long t = -6; t <= 5; t++) lv.Add(new LevelIn(t, 1, 0, 1, 1));
            w.Feed(0, T0, lv);
            var s = Snap(w, rpb: 4);
            Assert.Equal(4, s.RowCount);
            Assert.Equal(new double[] { 2, 4, 4, 2 }, s.Vol.Take(4).ToArray());
            Assert.Equal(-8, s.RowFirstTick(0)); Assert.Equal(-5, s.RowLastTick(0));
            Assert.Equal(4, s.RowFirstTick(3));
            Assert.Equal(12, s.Total);
        }

        [Fact]
        public void Rows_are_contiguous_with_zero_gaps_filled()
        {
            var w = new ProfileWindow(); w.Configure(24, null);
            w.Feed(0, T0, L((100, 5), (110, 7)));
            var s = Snap(w);
            Assert.Equal(11, s.RowCount);
            Assert.Equal(0, s.Vol[5]);
        }

        [Fact]
        public void Floor_div()
        {
            Assert.Equal(-2, ProfileSnapshot.FloorDiv(-5, 4));
            Assert.Equal(-1, ProfileSnapshot.FloorDiv(-1, 4));
            Assert.Equal(1, ProfileSnapshot.FloorDiv(4, 4));
        }

        [Theory]
        [InlineData(0.5, 30)]
        [InlineData(1.0, 60)]
        [InlineData(1.25, 75)]
        [InlineData(6.0, 360)]
        [InlineData(0.01, 1)]
        public void Fractional_hour_window_counts_bars(double hours, int expectedBars)
        {
            var w = new ProfileWindow(); w.Configure(hours, null);
            for (int i = 0; i < 600; i++) w.Feed(i, T0.AddMinutes(i), L((100 + i % 7, 1)));
            Assert.Equal(expectedBars, w.BarCount);
            Assert.Equal(599, w.LastBar);
            Assert.Equal(600 - expectedBars, w.FirstBar);
            Assert.Equal(expectedBars, Snap(w).Total);
        }

        [Fact]
        public void Window_boundary_is_strict_on_old_side()
        {
            var w = new ProfileWindow(); w.Configure(1, null);
            Assert.False(w.Accepts(T0.AddMinutes(1), T0.AddMinutes(61)));
            Assert.True(w.Accepts(T0.AddMinutes(2), T0.AddMinutes(61)));
            Assert.False(w.Accepts(T0, T0.AddMinutes(60)));
            Assert.True(w.Accepts(T0.AddMinutes(1), T0.AddMinutes(60)));
        }

        [Fact]
        public void Incremental_equals_brute_force_recompute()
        {
            var rnd = new Random(7);
            var w = new ProfileWindow(); w.Configure(2.5, null);
            var all = new List<(int bar, DateTime t, List<LevelIn> lv)>();
            for (int i = 0; i < 900; i++)
            {
                var lv = new List<LevelIn>();
                long c = 1000 + (long)(Math.Sin(i / 40.0) * 80);
                for (int k = 0; k < 12; k++)
                { double v = rnd.Next(1, 30); lv.Add(new LevelIn(c + rnd.Next(-15, 16), v, v * 0.4, v * 0.6, rnd.Next(1, 9))); }
                var t = T0.AddSeconds(i * 37);
                w.Feed(i, t, lv.Take(5).ToList());
                w.Feed(i, t, lv);
                all.Add((i, t, lv));
            }
            var newest = all[^1].t; var cut = newest - TimeSpan.FromHours(2.5);
            var fresh = new ProfileWindow(); fresh.Configure(2.5, null);
            foreach (var b in all.Where(b => b.t > cut)) fresh.Feed(b.bar, b.t, b.lv);
            var a = Snap(w); var b2 = Snap(fresh);
            Assert.Equal(b2.RowCount, a.RowCount);
            Assert.Equal(b2.Total, a.Total, 6);
            for (int r = 0; r < a.RowCount; r++)
            {
                Assert.Equal(b2.Vol[r], a.Vol[r], 6); Assert.Equal(b2.Ask[r], a.Ask[r], 6);
                Assert.Equal(b2.Bid[r], a.Bid[r], 6); Assert.Equal(b2.Trades[r], a.Trades[r]);
            }
            Assert.Equal(b2.PocRow, a.PocRow); Assert.Equal(b2.VahRow, a.VahRow); Assert.Equal(b2.ValRow, a.ValRow);
            Assert.Equal(b2.FirstBar, a.FirstBar);
        }

        [Fact]
        public void Live_bar_replace_does_not_double_count()
        {
            var w = new ProfileWindow(); w.Configure(24, null);
            w.Feed(0, T0, L((100, 5)));
            w.Feed(1, T0.AddMinutes(1), L((100, 2)));
            w.Feed(1, T0.AddMinutes(1), L((100, 3), (101, 4)));
            var s = Snap(w);
            Assert.Equal(12, s.Total);
            Assert.Equal(8, s.Vol[0]);
            Assert.Equal(2, w.BarCount);
        }

        [Fact]
        public void Snapshot_is_cached_until_something_changes()
        {
            var w = new ProfileWindow(); w.Configure(24, null);
            w.Feed(0, T0, L((100, 5)));
            var s = new ProfileSnapshot();
            Assert.True(s.Update(w, 1, 70, 85, 15, 0.25));
            Assert.False(s.Update(w, 1, 70, 85, 15, 0.25));
            Assert.True(s.Update(w, 2, 70, 85, 15, 0.25));
            w.Feed(1, T0.AddMinutes(1), L((100, 5)));
            Assert.True(s.Update(w, 2, 70, 85, 15, 0.25));
        }

        [Fact]
        public void Anchored_mode_ignores_earlier_bars_and_keeps_everything_after()
        {
            var w = new ProfileWindow(); w.Configure(1, T0.AddMinutes(10));
            for (int i = 0; i < 100; i++)
                if (w.Accepts(T0.AddMinutes(i), T0.AddMinutes(99))) w.Feed(i, T0.AddMinutes(i), L((100, 1)));
            Assert.Equal(90, Snap(w).Total);
            Assert.Equal(10, w.ProfileFirstBar);
            Assert.Equal(99, w.LastBar);
            Assert.True(w.BarCount <= 2);
            w.Feed(99, T0.AddMinutes(99), L((100, 4)));
            Assert.Equal(93, Snap(w).Total);
            Assert.Equal(10, Snap(w).FirstBar);
        }

        [Fact]
        public void Shrinking_window_reconfigure_resets()
        {
            var w = new ProfileWindow(); w.Configure(24, null);
            w.Feed(0, T0, L((100, 5)));
            w.Configure(12, null);
            Assert.True(w.IsEmpty);
            Assert.True(Snap(w).IsEmpty);
        }

        [Fact]
        public void Histogram_grows_in_both_directions()
        {
            var w = new ProfileWindow(); w.Configure(24, null);
            w.Feed(0, T0, L((100, 1)));
            w.Feed(1, T0.AddMinutes(1), L((5000, 1), (-4000, 1)));
            var s = Snap(w);
            Assert.Equal(3, s.Total);
            Assert.Equal(-4000, s.MinTick); Assert.Equal(5000, s.MaxTick);
        }

        [Fact]
        public void Hvn_peak_detection_second_peak_included()
        {
            var s = Snap(SingleBar(1, 10, 4, 1, 3, 9, 2), hvn: 85, lvn: 15);
            Assert.True((s.Flags[5] & RowFlags.Hvn) != 0);
            Assert.True((s.Flags[3] & RowFlags.Lvn) != 0);
            Assert.False((s.Flags[0] & RowFlags.Lvn) != 0);
        }

        [Fact]
        public void Vwap_median_delta_trades_maxima()
        {
            var w = new ProfileWindow(); w.Configure(24, null);
            w.Feed(0, T0, new List<LevelIn> { new LevelIn(100, 10, 2, 8, 3), new LevelIn(102, 30, 25, 5, 7) });
            var s = Snap(w);
            Assert.Equal((100 * 0.25 * 10 + 102 * 0.25 * 30) / 40, s.Vwap, 9);
            Assert.Equal(2, s.MedianRow);
            Assert.Equal(20, s.MaxAbsDelta);
            Assert.Equal(7, s.MaxTrades);
            Assert.Equal(25, s.MaxBid); Assert.Equal(8, s.MaxAsk);
            Assert.Equal(13, s.TotalAsk); Assert.Equal(27, s.TotalBid);
        }

        [Fact]
        public void Empty_window_gives_empty_snapshot()
        {
            var s = Snap(new ProfileWindow());
            Assert.True(s.IsEmpty); Assert.Equal(-1, s.PocRow);
        }

        [Fact]
        public void Fixed_anchor_conversion_utc_and_exchange()
        {
            var dt = new DateTime(2026, 11, 6, 9, 30, 0);
            Assert.Equal(dt, AnchoredVolumeProfile.AnchorToUtc(dt, AnchorTimeBase.Utc, TimeSpan.Zero));
            Assert.Equal(dt.AddHours(-5), AnchoredVolumeProfile.AnchorToUtc(dt, AnchorTimeBase.Exchange, TimeSpan.FromHours(5)));
        }

        [Fact]
        public void Perf_many_days_of_bars_fast()
        {
            var w = new ProfileWindow(); w.Configure(6, null);
            var lv = new List<LevelIn>();
            for (int k = 0; k < 40; k++) lv.Add(new LevelIn(1000 + k, 5, 2, 3, 2));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 14400; i++) w.Feed(i, T0.AddMinutes(i), lv);
            var s = Snap(w);
            sw.Stop();
            Assert.Equal(360, w.BarCount);
            Assert.True(sw.ElapsedMilliseconds < 1500, "took " + sw.ElapsedMilliseconds + " ms");
        }

        [Fact]
        public void Period_index_hourly_boundaries()
        {
            var z = TimeSpan.Zero;
            long a = PeriodMath.Index(T0.AddMinutes(59), 1, AnchorTimeBase.Utc, z, null);
            long b = PeriodMath.Index(T0.AddMinutes(60), 1, AnchorTimeBase.Utc, z, null);
            long c = PeriodMath.Index(T0.AddMinutes(61), 1, AnchorTimeBase.Utc, z, null);
            Assert.Equal(a + 1, b);
            Assert.Equal(b, c);
        }

        [Fact]
        public void Period_index_days_and_weeks_align_to_midnight_and_monday()
        {
            var z = TimeSpan.Zero;
            long d0 = PeriodMath.Index(new DateTime(2026, 11, 6, 23, 59, 0, DateTimeKind.Utc), 24, AnchorTimeBase.Utc, z, null);
            long d1 = PeriodMath.Index(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), 24, AnchorTimeBase.Utc, z, null);
            Assert.Equal(d0 + 1, d1);
            long w0 = PeriodMath.Index(new DateTime(2026, 11, 8, 23, 0, 0, DateTimeKind.Utc), 168, AnchorTimeBase.Utc, z, null);
            long w1 = PeriodMath.Index(new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc), 168, AnchorTimeBase.Utc, z, null);
            Assert.Equal(w0 + 1, w1);
        }

        [Fact]
        public void Period_index_with_anchor_starts_at_anchor_and_ignores_earlier_bars()
        {
            var anchor = T0.AddHours(5);
            Assert.Equal(long.MinValue, PeriodMath.Index(T0.AddHours(4), 2, AnchorTimeBase.Utc, TimeSpan.Zero, anchor));
            Assert.Equal(0, PeriodMath.Index(T0.AddHours(6.9), 2, AnchorTimeBase.Utc, TimeSpan.Zero, anchor));
            Assert.Equal(1, PeriodMath.Index(T0.AddHours(7), 2, AnchorTimeBase.Utc, TimeSpan.Zero, anchor));
        }

        [Fact]
        public void Period_profiles_accumulate_separately()
        {
            var p0 = new PeriodProfile(0); var p1 = new PeriodProfile(1);
            p0.Win.Feed(0, T0, L((100, 10)));
            p0.Win.Feed(1, T0.AddMinutes(1), L((100, 5)));
            p1.Win.Feed(2, T0.AddMinutes(60), L((100, 7)));
            p0.Snap.Update(p0.Win, 1, 70, 85, 15, 0.25); p1.Snap.Update(p1.Win, 1, 70, 85, 15, 0.25);
            Assert.Equal(15, p0.Snap.Total);
            Assert.Equal(7, p1.Snap.Total);
            Assert.Equal(0, p0.Snap.FirstBar);
            Assert.Equal(2, p1.Snap.FirstBar);
        }
    }
}
