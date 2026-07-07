using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WinForge.Services;

/// <summary>
/// 超級電腦（HPC）· Supercomputer / HPC center — a heavy, reactor-powered compute load.
/// Compute nodes online scale with the reactor's available electric output (1 node per ~2 MW,
/// capped at 5000). A job queue is drained while the reactor is generating: work done per tick
/// equals onlineNodes * dt (node-seconds). When the reactor is not generating, nodes go offline
/// and jobs park. Everything is computed here and never throws. UI-agnostic, pure managed C#.
/// </summary>
public sealed class HpcService
{
    // Tunables.
    public const double MwPerNode = 2.0;      // 1 compute node per ~2 MW of available output.
    public const int MaxNodes = 5000;         // hard cap on nodes online.
    public const double PflopsPerNode = 0.002; // ~2 TFLOPS per node → nodes*this = PFLOPS.

    /// <summary>One queued/active compute job. Public so it can back a ListView ({Binding}).</summary>
    public sealed class HpcJob
    {
        public string Name { get; set; } = "";
        public double TotalNodeHours { get; set; }      // requested size.
        public double RemainingNodeHours { get; set; }  // shrinks as work is applied.

        // Derived, for binding in the ListView.
        public double PercentDone
        {
            get
            {
                if (TotalNodeHours <= 0) return 100.0;
                double done = TotalNodeHours - RemainingNodeHours;
                double p = done / TotalNodeHours * 100.0;
                if (double.IsNaN(p) || double.IsInfinity(p)) return 0.0;
                return Math.Clamp(p, 0.0, 100.0);
            }
        }

        public string SizeText => $"{TotalNodeHours:0} node-h";
        public string StatusText => $"{Math.Max(0, RemainingNodeHours):0} node-h left · {PercentDone:0}%";
    }

    private int _jobSeq;

    /// <summary>Live, bindable job queue (front = running).</summary>
    public ObservableCollection<HpcJob> Jobs { get; } = new();

    // Live metrics (read by the UI each tick).
    public int NodesOnline { get; private set; }
    public double Pflops { get; private set; }
    public long JobsCompleted { get; private set; }
    public double AvailableMW { get; private set; }
    public bool Generating { get; private set; }

    /// <summary>Total node-hours still queued across all jobs.</summary>
    public double QueueDepthNodeHours
    {
        get
        {
            double sum = 0;
            foreach (var j in Jobs) sum += Math.Max(0, j.RemainingNodeHours);
            return sum;
        }
    }

    public int QueuedJobCount => Jobs.Count;

    /// <summary>Add a single job of the given size (node-hours). Never throws.</summary>
    public void SubmitJob(string name, double nodeHours)
    {
        try
        {
            double size = double.IsNaN(nodeHours) || nodeHours <= 0 ? 1.0 : nodeHours;
            _jobSeq++;
            string safeName = string.IsNullOrWhiteSpace(name) ? $"job-{_jobSeq:0000}" : name;
            Jobs.Add(new HpcJob
            {
                Name = safeName,
                TotalNodeHours = size,
                RemainingNodeHours = size,
            });
        }
        catch { }
    }

    /// <summary>Seed a handful of realistic sample jobs of varied sizes.</summary>
    public void AddSampleJobs(Func<int, string> namer)
    {
        try
        {
            double[] sizes = { 250, 1200, 60, 4800, 900 };
            foreach (var s in sizes)
            {
                _jobSeq++;
                string nm = namer == null ? $"job-{_jobSeq:0000}" : namer(_jobSeq);
                Jobs.Add(new HpcJob { Name = nm, TotalNodeHours = s, RemainingNodeHours = s });
            }
        }
        catch { }
    }

    /// <summary>Clear the queue and all counters back to zero.</summary>
    public void Reset()
    {
        try
        {
            Jobs.Clear();
            _jobSeq = 0;
            NodesOnline = 0;
            Pflops = 0;
            JobsCompleted = 0;
            AvailableMW = 0;
            Generating = false;
        }
        catch { }
    }

    /// <summary>
    /// Advance the sim by <paramref name="dt"/> seconds. Nodes online track available MW while
    /// generating; while generating, drain the queue by onlineNodes*dt node-seconds (converted to
    /// node-hours). When not generating, nodes drop to zero and jobs park. Never throws.
    /// </summary>
    public void Tick(double dt, double availableMW, bool generating)
    {
        try
        {
            double d = double.IsNaN(dt) || dt < 0 ? 0 : Math.Min(dt, 2.0);
            AvailableMW = double.IsNaN(availableMW) || availableMW < 0 ? 0 : availableMW;
            Generating = generating;

            if (!generating)
            {
                NodesOnline = 0;
                Pflops = 0;
                return;
            }

            int nodes = (int)Math.Floor(AvailableMW / MwPerNode);
            if (nodes < 0) nodes = 0;
            if (nodes > MaxNodes) nodes = MaxNodes;
            NodesOnline = nodes;
            Pflops = nodes * PflopsPerNode;

            if (nodes <= 0) return;

            // Work available this tick, in node-hours (node-seconds / 3600).
            double budget = nodes * d / 3600.0;
            if (budget <= 0) return;

            // Drain from the front of the queue.
            while (budget > 0 && Jobs.Count > 0)
            {
                var job = Jobs[0];
                double need = Math.Max(0, job.RemainingNodeHours);
                if (need <= budget)
                {
                    budget -= need;
                    Jobs.RemoveAt(0);
                    JobsCompleted++;
                }
                else
                {
                    job.RemainingNodeHours = need - budget;
                    budget = 0;
                }
            }
        }
        catch { }
    }
}
