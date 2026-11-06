using System;
using System.Collections.Generic;

namespace VolumeProfileX
{
    public readonly struct LevelIn
    {
        public readonly long Tick;
        public readonly double Volume;
        public readonly double Bid;
        public readonly double Ask;
        public readonly int Trades;

        public LevelIn(long tick, double volume, double bid, double ask, int trades)
        {
            Tick = tick; Volume = volume; Bid = bid; Ask = ask; Trades = trades;
        }
    }

    public sealed class ProfileWindow
    {
        private sealed class BarEntry
        {
            public int Bar;
            public DateTime Time;
            public LevelIn[] Levels = new LevelIn[64];
            public int Count;
        }

        public readonly object SyncRoot = new object();

        private readonly List<BarEntry> _entries = new List<BarEntry>();
        private int _head;
        private readonly Stack<BarEntry> _pool = new Stack<BarEntry>();

        private double[] _vol = new double[0], _bid = new double[0], _ask = new double[0];
        private int[] _trd = new int[0];
        private long _base;

        private double _hours = 1;
        private DateTime? _anchor;

        public long Version { get; private set; }
        public bool AnchoredMode => _anchor.HasValue;
        public double Hours => _hours;
        public DateTime? Anchor => _anchor;

        internal double[] VolArr => _vol;
        internal double[] BidArr => _bid;
        internal double[] AskArr => _ask;
        internal int[] TrdArr => _trd;
        internal long BaseTick => _base;
        internal int ArrLen => _vol.Length;

        public bool IsEmpty => _entries.Count - _head == 0;
        public int FirstBar => IsEmpty ? -1 : _entries[_head].Bar;
        public int LastBar => IsEmpty ? -1 : _entries[_entries.Count - 1].Bar;
        public DateTime FirstTime => IsEmpty ? DateTime.MinValue : _entries[_head].Time;
        public DateTime LastTime => IsEmpty ? DateTime.MinValue : _entries[_entries.Count - 1].Time;
        public int BarCount => _entries.Count - _head;

        public void Configure(double hours, DateTime? anchor)
        {
            if (!(hours > 0)) hours = 1.0 / 3600.0;
            if (hours == _hours && anchor == _anchor) return;
            _hours = hours; _anchor = anchor;
            Reset();
        }

        public void Reset()
        {
            for (int i = _head; i < _entries.Count; i++) _pool.Push(_entries[i]);
            _entries.Clear(); _head = 0; _anchorFirstBar = -1; _anchorFirstTime = DateTime.MinValue;
            Array.Clear(_vol, 0, _vol.Length); Array.Clear(_bid, 0, _bid.Length);
            Array.Clear(_ask, 0, _ask.Length); Array.Clear(_trd, 0, _trd.Length);
            Version++;
        }

        public bool Accepts(DateTime barTime, DateTime latestBarTime)
        {
            if (_anchor.HasValue) return barTime >= _anchor.Value;
            return barTime > latestBarTime - TimeSpan.FromHours(_hours);
        }

        public void Feed(int bar, DateTime time, List<LevelIn> levels)
        {
            if (_anchor.HasValue && time < _anchor.Value) return;
            int n = _entries.Count;
            if (n == _head || bar > _entries[n - 1].Bar)
            {
                var e = Rent(bar, time, levels);
                Apply(e, +1);
                if (n == _head) { _anchorFirstBar = bar; _anchorFirstTime = time; }
                if (_anchor.HasValue && n - _head >= 1)
                {
                    Return(_entries[n - 1]); _entries.RemoveAt(n - 1);
                }
                _entries.Add(e);
                Trim();
            }
            else
            {
                int idx = -1;
                for (int i = n - 1; i >= _head; i--) { if (_entries[i].Bar == bar) { idx = i; break; } }
                if (idx < 0) return;
                var old = _entries[idx];
                Apply(old, -1);
                Fill(old, bar, time, levels);
                Apply(old, +1);
            }
            Version++;
        }

        private int _anchorFirstBar = -1;
        private DateTime _anchorFirstTime = DateTime.MinValue;

        public int ProfileFirstBar => IsEmpty ? -1 : (_anchor.HasValue && _anchorFirstBar >= 0 ? _anchorFirstBar : FirstBar);
        public DateTime ProfileFirstTime => IsEmpty ? DateTime.MinValue : (_anchor.HasValue && _anchorFirstBar >= 0 ? _anchorFirstTime : FirstTime);

        private void Trim()
        {
            if (_anchor.HasValue) return;
            var newest = _entries[_entries.Count - 1].Time;
            var cut = newest - TimeSpan.FromHours(_hours);
            while (_entries.Count - _head > 1 && _entries[_head].Time <= cut)
            {
                var e = _entries[_head];
                Apply(e, -1);
                Return(e);
                _entries[_head] = null;
                _head++;
            }
            if (_head > 256 && _head * 2 > _entries.Count)
            {
                _entries.RemoveRange(0, _head); _head = 0;
            }
        }

        private BarEntry Rent(int bar, DateTime time, List<LevelIn> levels)
        {
            var e = _pool.Count > 0 ? _pool.Pop() : new BarEntry();
            Fill(e, bar, time, levels);
            return e;
        }

        private void Return(BarEntry e) { _pool.Push(e); }

        private static void Fill(BarEntry e, int bar, DateTime time, List<LevelIn> levels)
        {
            e.Bar = bar; e.Time = time; e.Count = levels.Count;
            if (e.Levels.Length < levels.Count) e.Levels = new LevelIn[Math.Max(levels.Count, e.Levels.Length * 2)];
            levels.CopyTo(e.Levels, 0);
        }

        private void Apply(BarEntry e, int sign)
        {
            for (int i = 0; i < e.Count; i++)
            {
                ref readonly var l = ref e.Levels[i];
                if (sign > 0) Ensure(l.Tick);
                else if (l.Tick < _base || l.Tick >= _base + _vol.Length) continue;
                int k = (int)(l.Tick - _base);
                _vol[k] += sign * l.Volume; _bid[k] += sign * l.Bid; _ask[k] += sign * l.Ask; _trd[k] += sign * l.Trades;
                if (sign < 0)
                {
                    if (_vol[k] < 1e-9) _vol[k] = 0;
                    if (_bid[k] < 1e-9) _bid[k] = 0;
                    if (_ask[k] < 1e-9) _ask[k] = 0;
                    if (_trd[k] < 0) _trd[k] = 0;
                }
            }
        }

        private void Ensure(long tick)
        {
            if (_vol.Length == 0)
            {
                _base = tick - 128; Alloc(512); return;
            }
            if (tick >= _base && tick < _base + _vol.Length) return;
            long lo = Math.Min(tick, _base) - 128;
            long hi = Math.Max(tick, _base + _vol.Length - 1) + 128;
            int len = (int)(hi - lo + 1);
            long oldBase = _base; var ov = _vol; var ob = _bid; var oa = _ask; var ot = _trd;
            _base = lo; Alloc(len);
            int off = (int)(oldBase - lo);
            Array.Copy(ov, 0, _vol, off, ov.Length); Array.Copy(ob, 0, _bid, off, ob.Length);
            Array.Copy(oa, 0, _ask, off, oa.Length); Array.Copy(ot, 0, _trd, off, ot.Length);
        }

        private void Alloc(int len)
        {
            _vol = new double[len]; _bid = new double[len]; _ask = new double[len]; _trd = new int[len];
        }
    }

    [Flags]
    public enum RowFlags : byte { None = 0, InValueArea = 1, Poc = 2, Hvn = 4, Lvn = 8 }

    public sealed class ProfileSnapshot
    {
        public int RowCount;
        public long FirstBucket;
        public int Rpb = 1;
        public double[] Vol = new double[0], Bid = new double[0], Ask = new double[0];
        public int[] Trades = new int[0];
        public RowFlags[] Flags = new RowFlags[0];
        public int PocRow = -1, VahRow = -1, ValRow = -1, MedianRow = -1;
        public double Total, MaxVol, MaxBid, MaxAsk, MaxAbsDelta, MaxTrades, Vwap, TotalAsk, TotalBid;
        public long MinTick, MaxTick;
        public int FirstBar = -1, LastBar = -1;
        public DateTime StartTime, EndTime;
        public double TickSize = 1;
        public bool IsEmpty => RowCount == 0;
        public long Version => _ver;
        public int BuildId;

        private long _ver = -1; private int _rpb; private double _va, _hvn, _lvn;

        public long RowFirstTick(int row) => (FirstBucket + row) * Rpb;
        public long RowLastTick(int row) => (FirstBucket + row + 1) * Rpb - 1;
        public double RowDelta(int row) => Ask[row] - Bid[row];

        public static long FloorDiv(long a, long b)
        {
            long q = a / b; if ((a % b != 0) && ((a < 0) != (b < 0))) q--; return q;
        }

        public bool Update(ProfileWindow w, int rowsPerBucket, double vaPct, double hvnPct, double lvnPct, double tickSize)
        {
            if (rowsPerBucket < 1) rowsPerBucket = 1;
            if (!(tickSize > 0)) tickSize = 1;
            if (_ver == w.Version && _rpb == rowsPerBucket && _va == vaPct && _hvn == hvnPct && _lvn == lvnPct && TickSize == tickSize) return false;
            _ver = w.Version; _rpb = rowsPerBucket; _va = vaPct; _hvn = hvnPct; _lvn = lvnPct; TickSize = tickSize;
            Rebuild(w);
            return true;
        }

        private void Rebuild(ProfileWindow w)
        {
            Rpb = _rpb; BuildId++;
            RowCount = 0; PocRow = VahRow = ValRow = MedianRow = -1;
            Total = MaxVol = MaxBid = MaxAsk = MaxAbsDelta = MaxTrades = Vwap = TotalAsk = TotalBid = 0;
            FirstBar = w.ProfileFirstBar; LastBar = w.LastBar; StartTime = w.ProfileFirstTime; EndTime = w.LastTime;
            var v = w.VolArr; var bi = w.BidArr; var ak = w.AskArr; var tr = w.TrdArr; long b0 = w.BaseTick; int len = w.ArrLen;
            int lo = -1, hi = -1;
            for (int i = 0; i < len; i++) if (v[i] > 0 || tr[i] > 0) { lo = i; break; }
            if (lo < 0) return;
            for (int i = len - 1; i >= lo; i--) if (v[i] > 0 || tr[i] > 0) { hi = i; break; }
            MinTick = b0 + lo; MaxTick = b0 + hi;
            long bLo = FloorDiv(MinTick, Rpb), bHi = FloorDiv(MaxTick, Rpb);
            int n = (int)(bHi - bLo + 1);
            if (Vol.Length < n)
            {
                int cap = Math.Max(n, Vol.Length * 2);
                Vol = new double[cap]; Bid = new double[cap]; Ask = new double[cap]; Trades = new int[cap]; Flags = new RowFlags[cap];
            }
            else
            {
                Array.Clear(Vol, 0, n); Array.Clear(Bid, 0, n); Array.Clear(Ask, 0, n); Array.Clear(Trades, 0, n); Array.Clear(Flags, 0, n);
            }
            FirstBucket = bLo; RowCount = n;
            double pv = 0;
            for (int i = lo; i <= hi; i++)
            {
                if (v[i] == 0 && tr[i] == 0) continue;
                long t = b0 + i;
                int r = (int)(FloorDiv(t, Rpb) - bLo);
                Vol[r] += v[i]; Bid[r] += bi[i]; Ask[r] += ak[i]; Trades[r] += tr[i];
                pv += t * TickSize * v[i];
            }
            for (int r = 0; r < n; r++)
            {
                Total += Vol[r]; TotalAsk += Ask[r]; TotalBid += Bid[r];
                if (Vol[r] > MaxVol) MaxVol = Vol[r];
                if (Bid[r] > MaxBid) MaxBid = Bid[r];
                if (Ask[r] > MaxAsk) MaxAsk = Ask[r];
                double ad = Math.Abs(Ask[r] - Bid[r]); if (ad > MaxAbsDelta) MaxAbsDelta = ad;
                if (Trades[r] > MaxTrades) MaxTrades = Trades[r];
            }
            Vwap = Total > 0 ? pv / Total : 0;
            if (Total <= 0) return;

            double mid = (n - 1) / 2.0; int poc = 0; double best = -1;
            for (int r = 0; r < n; r++)
            {
                if (Vol[r] > best || (Vol[r] == best && Math.Abs(r - mid) < Math.Abs(poc - mid))) { best = Vol[r]; poc = r; }
            }
            PocRow = poc;

            double target = Total * _va / 100.0;
            double acc = Vol[poc]; int a = poc, b = poc;
            while (acc < target - 1e-9 && (a > 0 || b < n - 1))
            {
                double up = 0, dn = 0; int upN = 0, dnN = 0;
                if (b + 1 < n) { up += Vol[b + 1]; upN++; if (b + 2 < n) { up += Vol[b + 2]; upN++; } }
                if (a - 1 >= 0) { dn += Vol[a - 1]; dnN++; if (a - 2 >= 0) { dn += Vol[a - 2]; dnN++; } }
                if (upN > 0 && (dnN == 0 || up >= dn)) { b += upN; acc += up; }
                else { a -= dnN; acc += dn; }
            }
            ValRow = a; VahRow = b;
            for (int r = a; r <= b; r++) Flags[r] |= RowFlags.InValueArea;
            Flags[poc] |= RowFlags.Poc;

            double half = Total / 2, cum = 0; MedianRow = n - 1;
            for (int r = 0; r < n; r++) { cum += Vol[r]; if (cum >= half) { MedianRow = r; break; } }

            double hv = MaxVol * _hvn / 100.0, lv = MaxVol * _lvn / 100.0;
            for (int r = 0; r < n; r++)
            {
                double c = Vol[r];
                double pl = r > 0 ? Vol[r - 1] : double.NegativeInfinity, pr = r < n - 1 ? Vol[r + 1] : double.NegativeInfinity;
                if (c > 0 && c >= hv && c >= pl && c >= pr) Flags[r] |= RowFlags.Hvn;
                if (r > 0 && r < n - 1 && c <= lv && c <= Vol[r - 1] && c <= Vol[r + 1]) Flags[r] |= RowFlags.Lvn;
            }
        }
    }
}
