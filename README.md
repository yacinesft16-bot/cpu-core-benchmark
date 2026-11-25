# CPU Core Count and Processing Performance

This repository contains the code and raw measurements for the research project **"The Impact of CPU Core Count on Processing Performance"** (Mohammed Yassine Safer Tabi, Student ID 81697).

The project measures how execution time, speedup, and efficiency change when a CPU-intensive workload runs on 1, 2, 4, 8, and 16 logical processors, for two workloads: matrix multiplication and array sorting.

## Test system

- CPU: AMD Ryzen 7 PRO 5850U with Radeon Graphics (8 physical cores, 16 logical processors with SMT)
- RAM: 16 GB
- OS: Windows Windows 11 (version 26H2)
- Language / framework: C#, .NET 10.0 (LTS), Visual Studio
- Build: Debug

## Files

| File | Content |
|---|---|
| `Program_Matrix.cs` | Matrix multiplication benchmark (sizes 200, 500, 1000) |
| `Program_Sort.cs` | Sorting benchmark (100,000, 1,000,000, 5,000,000 integers) |
| `results_raw.csv` | Every single run of the matrix benchmark |
| `results_summary.csv` | Average, standard deviation, min, max, speedup, efficiency (matrix) |
| `sort_raw.csv` | Every single run of the sorting benchmark |
| `sort_summary.csv` | Same summary statistics for sorting |

## Method (short)

- A sequential version (plain loops or `Array.Sort`) and a parallel version (`Parallel.For`) of each workload.
- Core count is controlled with Windows processor affinity: one logical processor per physical core for 1 to 8 cores, and all 16 logical processors for the 16-core case.
- For each configuration: one warm-up run, then 10 measured runs timed with `Stopwatch`.
- Inputs are random with a fixed seed (42), so every configuration uses the same data.
- Speedup = T(1 core) / T(n cores). Efficiency = Speedup / n.
- The parallel sort splits the array into chunks, sorts each chunk in parallel, then merges pairs of chunks in parallel rounds. The result is checked to be sorted.

## How to reproduce

1. Open Visual Studio and create a new C# Console App (.NET 10.0).
2. Replace the content of `Program.cs` with the content of `Program_Matrix.cs` (or `Program_Sort.cs`).
3. Close other programs, plug in the charger, and run with Ctrl+F5.
4. The program writes its CSV files next to the executable (the path is printed on the first line).

Results depend on the hardware, so absolute times will differ on other machines.

## AI use declaration

The code and documentation of this project were written with the help of an AI assistant (Claude, by Anthropic). All measurements were run by the author on his own computer, and the results in the CSV files come directly from those runs.
