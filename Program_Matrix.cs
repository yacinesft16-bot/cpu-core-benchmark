using System.Diagnostics;
using System.Globalization;

int[] sizes = { 200, 500, 1000 };
int[] coreCounts = { 1, 2, 4, 8, 16 };
const int Runs = 10;
var inv = CultureInfo.InvariantCulture;

string dir = AppContext.BaseDirectory;
string rawPath = Path.Combine(dir, "results_raw.csv");
string sumPath = Path.Combine(dir, "results_summary.csv");

using var raw = new StreamWriter(rawPath) { AutoFlush = true };
using var sum = new StreamWriter(sumPath) { AutoFlush = true };
raw.WriteLine("matrix_size,cores,run,time_ms");
sum.WriteLine("matrix_size,cores,avg_ms,sd_ms,min_ms,max_ms,speedup,efficiency");

Console.WriteLine($"Files will be saved in: {dir}");
ThreadPool.SetMinThreads(16, 16);

foreach (int size in sizes)
{
    var rng = new Random(42);
    double[,] a = new double[size, size];
    double[,] b = new double[size, size];
    double[,] c = new double[size, size];
    for (int i = 0; i < size; i++)
        for (int j = 0; j < size; j++)
        {
            a[i, j] = rng.NextDouble();
            b[i, j] = rng.NextDouble();
        }

    Console.WriteLine($"\n=== Matrix {size}x{size} ===");

    // Sequential baseline (plain loops, one logical processor)
    Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)1;
    Array.Clear(c);
    MultiplySequential(a, b, c, size); // warm-up
    var seqTimes = new List<double>();
    for (int run = 1; run <= Runs; run++)
    {
        Array.Clear(c);
        var sw = Stopwatch.StartNew();
        MultiplySequential(a, b, c, size);
        sw.Stop();
        seqTimes.Add(sw.Elapsed.TotalMilliseconds);
        raw.WriteLine($"{size},sequential,{run},{sw.Elapsed.TotalMilliseconds.ToString("F2", inv)}");
    }
    double seqAvg = seqTimes.Average();
    double seqSd = Math.Sqrt(seqTimes.Sum(t => (t - seqAvg) * (t - seqAvg)) / (Runs - 1));
    sum.WriteLine($"{size},sequential,{seqAvg.ToString("F2", inv)},{seqSd.ToString("F2", inv)},{seqTimes.Min().ToString("F2", inv)},{seqTimes.Max().ToString("F2", inv)},,");
    Console.WriteLine($"sequential: avg {seqAvg:F1} ms | sd {seqSd:F1}");

    // Parallel versions
    double t1 = 0;
    foreach (int cores in coreCounts)
    {
        // Logical CPUs 0,1 = core 1; 2,3 = core 2; ... take every second bit (one thread per physical core)
        long mask = 0;
        if (cores <= 8)
            for (int i = 0; i < cores; i++) mask |= 1L << (2 * i);
        else
            mask = 0xFFFF; // all 16 logical processors
        Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)mask;

        Array.Clear(c);
        MultiplyParallel(a, b, c, size, cores); // warm-up

        var times = new List<double>();
        for (int run = 1; run <= Runs; run++)
        {
            Array.Clear(c);
            var sw = Stopwatch.StartNew();
            MultiplyParallel(a, b, c, size, cores);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
            raw.WriteLine($"{size},{cores},{run},{sw.Elapsed.TotalMilliseconds.ToString("F2", inv)}");
        }

        double avg = times.Average();
        double sd = Math.Sqrt(times.Sum(t => (t - avg) * (t - avg)) / (Runs - 1));
        if (cores == 1) t1 = avg;
        double speedup = t1 / avg;
        double efficiency = speedup / cores;

        sum.WriteLine($"{size},{cores},{avg.ToString("F2", inv)},{sd.ToString("F2", inv)},{times.Min().ToString("F2", inv)},{times.Max().ToString("F2", inv)},{speedup.ToString("F3", inv)},{efficiency.ToString("F3", inv)}");
        Console.WriteLine($"{cores} cores: avg {avg:F1} ms | sd {sd:F1} | speedup {speedup:F2} | efficiency {efficiency:F2}");
    }
}

Console.WriteLine("\nDone. You can close this window.");

static void MultiplySequential(double[,] a, double[,] b, double[,] c, int n)
{
    for (int i = 0; i < n; i++)
        for (int k = 0; k < n; k++)
            for (int j = 0; j < n; j++)
                c[i, j] += a[i, k] * b[k, j];
}

static void MultiplyParallel(double[,] a, double[,] b, double[,] c, int n, int threads)
{
    var options = new ParallelOptions { MaxDegreeOfParallelism = threads };
    Parallel.For(0, n, options, i =>
    {
        for (int k = 0; k < n; k++)
            for (int j = 0; j < n; j++)
                c[i, j] += a[i, k] * b[k, j];
    });
}