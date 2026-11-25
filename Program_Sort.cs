using System.Diagnostics;
using System.Globalization;

int[] sizes = { 100_000, 1_000_000, 5_000_000 };
int[] coreCounts = { 1, 2, 4, 8, 16 };
const int Runs = 10;
var inv = CultureInfo.InvariantCulture;

string dir = AppContext.BaseDirectory;
using var raw = new StreamWriter(Path.Combine(dir, "sort_raw.csv")) { AutoFlush = true };
using var sum = new StreamWriter(Path.Combine(dir, "sort_summary.csv")) { AutoFlush = true };
raw.WriteLine("array_size,cores,run,time_ms");
sum.WriteLine("array_size,cores,avg_ms,sd_ms,min_ms,max_ms,speedup,efficiency");

Console.WriteLine($"Files will be saved in: {dir}");
ThreadPool.SetMinThreads(16, 16);

foreach (int n in sizes)
{
    var rng = new Random(42);
    int[] orig = new int[n];
    for (int i = 0; i < n; i++) orig[i] = rng.Next();
    int[] data = new int[n];
    int[] buffer = new int[n];

    Console.WriteLine();
    Console.WriteLine($"=== Array of {n} integers ===");

    // Sequential baseline: plain Array.Sort on one logical processor
    Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)1;
    Array.Copy(orig, data, n);
    Array.Sort(data); // warm-up
    var seqTimes = new List<double>();
    for (int run = 1; run <= Runs; run++)
    {
        Array.Copy(orig, data, n);
        var sw = Stopwatch.StartNew();
        Array.Sort(data);
        sw.Stop();
        seqTimes.Add(sw.Elapsed.TotalMilliseconds);
        raw.WriteLine($"{n},sequential,{run},{sw.Elapsed.TotalMilliseconds.ToString("F2", inv)}");
    }
    var (seqAvg, seqSd) = Stats(seqTimes);
    sum.WriteLine($"{n},sequential,{seqAvg.ToString("F2", inv)},{seqSd.ToString("F2", inv)},{seqTimes.Min().ToString("F2", inv)},{seqTimes.Max().ToString("F2", inv)},,");
    Console.WriteLine($"sequential: avg {seqAvg:F1} ms | sd {seqSd:F1}");

    // Parallel versions
    double t1 = 0;
    foreach (int cores in coreCounts)
    {
        // Logical CPUs 0,1 = core 1; 2,3 = core 2; ... take every second bit
        long mask = 0;
        if (cores <= 8)
            for (int i = 0; i < cores; i++) mask |= 1L << (2 * i);
        else
            mask = 0xFFFF;
        Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)mask;

        Array.Copy(orig, data, n);
        int[] check = SortParallel(data, buffer, cores); // warm-up
        if (!IsSorted(check)) Console.WriteLine("WARNING: result is NOT sorted!");

        var times = new List<double>();
        for (int run = 1; run <= Runs; run++)
        {
            Array.Copy(orig, data, n);
            var sw = Stopwatch.StartNew();
            SortParallel(data, buffer, cores);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
            raw.WriteLine($"{n},{cores},{run},{sw.Elapsed.TotalMilliseconds.ToString("F2", inv)}");
        }

        var (avg, sd) = Stats(times);
        if (cores == 1) t1 = avg;
        double speedup = t1 / avg;
        double efficiency = speedup / cores;
        sum.WriteLine($"{n},{cores},{avg.ToString("F2", inv)},{sd.ToString("F2", inv)},{times.Min().ToString("F2", inv)},{times.Max().ToString("F2", inv)},{speedup.ToString("F3", inv)},{efficiency.ToString("F3", inv)}");
        Console.WriteLine($"{cores} cores: avg {avg:F1} ms | sd {sd:F1} | speedup {speedup:F2} | efficiency {efficiency:F2}");
    }
}

Console.WriteLine();
Console.WriteLine("Done. You can close this window.");

static (double avg, double sd) Stats(List<double> t)
{
    double avg = t.Average();
    double sd = Math.Sqrt(t.Sum(x => (x - avg) * (x - avg)) / (t.Count - 1));
    return (avg, sd);
}

static bool IsSorted(int[] a)
{
    for (int i = 1; i < a.Length; i++)
        if (a[i - 1] > a[i]) return false;
    return true;
}

static void Merge(int[] src, int[] dst, int lo, int mid, int hi)
{
    int i = lo, j = mid, k = lo;
    while (i < mid && j < hi) dst[k++] = src[i] <= src[j] ? src[i++] : src[j++];
    while (i < mid) dst[k++] = src[i++];
    while (j < hi) dst[k++] = src[j++];
}

// Split into 'parts' chunks, sort each chunk in parallel, then merge pairs in rounds.
static int[] SortParallel(int[] data, int[] buffer, int parts)
{
    int n = data.Length;
    if (parts == 1) { Array.Sort(data); return data; }

    int[] bounds = new int[parts + 1];
    for (int i = 0; i <= parts; i++) bounds[i] = (int)((long)n * i / parts);

    var options = new ParallelOptions { MaxDegreeOfParallelism = parts };
    Parallel.For(0, parts, options, part =>
        Array.Sort(data, bounds[part], bounds[part + 1] - bounds[part]));

    int[] src = data, dst = buffer;
    int chunks = parts;
    while (chunks > 1)
    {
        int pairs = chunks / 2;
        int[] s = src, d = dst, bs = bounds;
        Parallel.For(0, pairs, options, q =>
            Merge(s, d, bs[2 * q], bs[2 * q + 1], bs[2 * q + 2]));

        int[] nb = new int[pairs + 1];
        for (int x = 0; x <= pairs; x++) nb[x] = bs[2 * x];
        bounds = nb;
        (src, dst) = (dst, src);
        chunks = pairs;
    }
    return src;
}