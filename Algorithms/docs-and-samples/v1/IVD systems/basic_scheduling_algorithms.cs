using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FluidHandling.BasicScheduling
{
    /// <summary>
    /// Represents a fluid handling operation in an IVD system
    /// </summary>
    public class FluidOperation
    {
        public int Id { get; set; }
        public string SampleId { get; set; }
        public string OperationType { get; set; }
        public double VolumeInMicroliters { get; set; }
        public int EstimatedDurationMs { get; set; }
        public int Priority { get; set; }
        public DateTime SubmissionTime { get; set; }
        public DateTime Deadline { get; set; }
        public string SourceLocation { get; set; }
        public string DestinationLocation { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletionTime { get; set; }
    }

    /// <summary>
    /// Represents a fluid handling instrument (pipette, dispenser, etc.)
    /// </summary>
    public class FluidInstrument
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public double MinVolumeCapacity { get; set; }
        public double MaxVolumeCapacity { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime? LastUsed { get; set; }
        public FluidOperation CurrentOperation { get; set; }
    }

    /// <summary>
    /// Basic First-Come-First-Served (FCFS) Scheduler
    /// Simplest algorithm: processes operations in order of arrival
    /// </summary>
    public class FCFSScheduler
    {
        private readonly Queue<FluidOperation> _operationQueue;
        private readonly List<FluidInstrument> _instruments;

        public FCFSScheduler(List<FluidInstrument> instruments)
        {
            _operationQueue = new Queue<FluidOperation>();
            _instruments = instruments;
        }

        public void AddOperation(FluidOperation operation)
        {
            _operationQueue.Enqueue(operation);
            Console.WriteLine($"[FCFS] Added operation {operation.Id} for sample {operation.SampleId}");
        }

        public async Task<FluidOperation> ScheduleNext()
        {
            if (_operationQueue.Count == 0)
                return null;

            var nextOperation = _operationQueue.Dequeue();
            var availableInstrument = _instruments.FirstOrDefault(i => 
                i.IsAvailable && 
                i.MaxVolumeCapacity >= nextOperation.VolumeInMicroliters);

            if (availableInstrument == null)
            {
                // No instrument available, requeue the operation
                _operationQueue.Enqueue(nextOperation);
                return null;
            }

            // Assign operation to instrument
            availableInstrument.IsAvailable = false;
            availableInstrument.CurrentOperation = nextOperation;
            
            Console.WriteLine($"[FCFS] Assigned operation {nextOperation.Id} to instrument {availableInstrument.Name}");
            
            // Simulate operation execution
            await Task.Delay(nextOperation.EstimatedDurationMs);
            
            // Complete operation
            nextOperation.IsCompleted = true;
            nextOperation.CompletionTime = DateTime.Now;
            availableInstrument.IsAvailable = true;
            availableInstrument.CurrentOperation = null;
            availableInstrument.LastUsed = DateTime.Now;

            return nextOperation;
        }
    }

    /// <summary>
    /// Shortest Job First (SJF) Scheduler
    /// Processes operations with shortest estimated duration first
    /// </summary>
    public class SJFScheduler
    {
        private readonly List<FluidOperation> _operationQueue;
        private readonly List<FluidInstrument> _instruments;

        public SJFScheduler(List<FluidInstrument> instruments)
        {
            _operationQueue = new List<FluidOperation>();
            _instruments = instruments;
        }

        public void AddOperation(FluidOperation operation)
        {
            _operationQueue.Add(operation);
            Console.WriteLine($"[SJF] Added operation {operation.Id} (duration: {operation.EstimatedDurationMs}ms)");
        }

        public async Task<FluidOperation> ScheduleNext()
        {
            if (_operationQueue.Count == 0)
                return null;

            // Find shortest job that can be executed
            var shortestJob = _operationQueue
                .Where(op => !op.IsCompleted)
                .OrderBy(op => op.EstimatedDurationMs)
                .FirstOrDefault();

            if (shortestJob == null)
                return null;

            var availableInstrument = _instruments.FirstOrDefault(i => 
                i.IsAvailable && 
                i.MaxVolumeCapacity >= shortestJob.VolumeInMicroliters);

            if (availableInstrument == null)
                return null;

            // Remove from queue and assign to instrument
            _operationQueue.Remove(shortestJob);
            availableInstrument.IsAvailable = false;
            availableInstrument.CurrentOperation = shortestJob;
            
            Console.WriteLine($"[SJF] Assigned shortest job {shortestJob.Id} ({shortestJob.EstimatedDurationMs}ms) to {availableInstrument.Name}");
            
            // Simulate operation execution
            await Task.Delay(shortestJob.EstimatedDurationMs);
            
            // Complete operation
            shortestJob.IsCompleted = true;
            shortestJob.CompletionTime = DateTime.Now;
            availableInstrument.IsAvailable = true;
            availableInstrument.CurrentOperation = null;
            availableInstrument.LastUsed = DateTime.Now;

            return shortestJob;
        }
    }

    /// <summary>
    /// Priority-based Scheduler
    /// Processes operations based on priority levels (higher number = higher priority)
    /// </summary>
    public class PriorityScheduler
    {
        private readonly List<FluidOperation> _operationQueue;
        private readonly List<FluidInstrument> _instruments;

        public PriorityScheduler(List<FluidInstrument> instruments)
        {
            _operationQueue = new List<FluidOperation>();
            _instruments = instruments;
        }

        public void AddOperation(FluidOperation operation)
        {
            _operationQueue.Add(operation);
            Console.WriteLine($"[Priority] Added operation {operation.Id} with priority {operation.Priority}");
        }

        public async Task<FluidOperation> ScheduleNext()
        {
            if (_operationQueue.Count == 0)
                return null;

            // Find highest priority job that can be executed
            var highestPriorityJob = _operationQueue
                .Where(op => !op.IsCompleted)
                .OrderByDescending(op => op.Priority)
                .ThenBy(op => op.SubmissionTime) // Break ties by submission time
                .FirstOrDefault();

            if (highestPriorityJob == null)
                return null;

            var availableInstrument = _instruments.FirstOrDefault(i => 
                i.IsAvailable && 
                i.MaxVolumeCapacity >= highestPriorityJob.VolumeInMicroliters);

            if (availableInstrument == null)
                return null;

            // Remove from queue and assign to instrument
            _operationQueue.Remove(highestPriorityJob);
            availableInstrument.IsAvailable = false;
            availableInstrument.CurrentOperation = highestPriorityJob;
            
            Console.WriteLine($"[Priority] Assigned priority {highestPriorityJob.Priority} job {highestPriorityJob.Id} to {availableInstrument.Name}");
            
            // Simulate operation execution
            await Task.Delay(highestPriorityJob.EstimatedDurationMs);
            
            // Complete operation
            highestPriorityJob.IsCompleted = true;
            highestPriorityJob.CompletionTime = DateTime.Now;
            availableInstrument.IsAvailable = true;
            availableInstrument.CurrentOperation = null;
            availableInstrument.LastUsed = DateTime.Now;

            return highestPriorityJob;
        }
    }

    /// <summary>
    /// Round Robin Scheduler
    /// Gives each operation a time slice in rotation
    /// </summary>
    public class RoundRobinScheduler
    {
        private readonly Queue<FluidOperation> _operationQueue;
        private readonly List<FluidInstrument> _instruments;
        private readonly int _timeSliceMs;

        public RoundRobinScheduler(List<FluidInstrument> instruments, int timeSliceMs = 1000)
        {
            _operationQueue = new Queue<FluidOperation>();
            _instruments = instruments;
            _timeSliceMs = timeSliceMs;
        }

        public void AddOperation(FluidOperation operation)
        {
            _operationQueue.Enqueue(operation);
            Console.WriteLine($"[RoundRobin] Added operation {operation.Id} (time slice: {_timeSliceMs}ms)");
        }

        public async Task<FluidOperation> ScheduleNext()
        {
            if (_operationQueue.Count == 0)
                return null;

            var nextOperation = _operationQueue.Dequeue();
            var availableInstrument = _instruments.FirstOrDefault(i => 
                i.IsAvailable && 
                i.MaxVolumeCapacity >= nextOperation.VolumeInMicroliters);

            if (availableInstrument == null)
            {
                // No instrument available, requeue the operation
                _operationQueue.Enqueue(nextOperation);
                return null;
            }

            // Assign operation to instrument
            availableInstrument.IsAvailable = false;
            availableInstrument.CurrentOperation = nextOperation;
            
            Console.WriteLine($"[RoundRobin] Assigned operation {nextOperation.Id} to {availableInstrument.Name} for {_timeSliceMs}ms");
            
            // Execute for time slice or until completion
            var executionTime = Math.Min(_timeSliceMs, nextOperation.EstimatedDurationMs);
            await Task.Delay(executionTime);
            
            // Update remaining time
            nextOperation.EstimatedDurationMs -= executionTime;
            
            if (nextOperation.EstimatedDurationMs <= 0)
            {
                // Operation completed
                nextOperation.IsCompleted = true;
                nextOperation.CompletionTime = DateTime.Now;
                Console.WriteLine($"[RoundRobin] Operation {nextOperation.Id} completed");
            }
            else
            {
                // Operation not completed, requeue for next time slice
                _operationQueue.Enqueue(nextOperation);
                Console.WriteLine($"[RoundRobin] Operation {nextOperation.Id} requeued ({nextOperation.EstimatedDurationMs}ms remaining)");
            }

            // Free up instrument
            availableInstrument.IsAvailable = true;
            availableInstrument.CurrentOperation = null;
            availableInstrument.LastUsed = DateTime.Now;

            return nextOperation.IsCompleted ? nextOperation : null;
        }
    }
}