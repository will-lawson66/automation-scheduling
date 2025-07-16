using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluidHandling.Core.Models;
using FluidHandling.Core.Interfaces;

namespace FluidHandling.OptimizationScheduling
{
    /// <summary>
    /// Genetic Algorithm Scheduler for IVD Fluid Handling Systems
    /// Based on research showing genetic algorithms can achieve substantial improvements
    /// when combined with specialized operators for scheduling problems
    /// </summary>
    public class GeneticAlgorithmScheduler : IScheduler
    {
        public string Name => "Genetic Algorithm Scheduler";
        public string Description => "Evolutionary algorithm for multi-objective scheduling optimization";
        public SchedulerType Type => SchedulerType.Optimization;

        private readonly List<TimedFluidOperation> _operations;
        private readonly List<FluidInstrument> _instruments;
        private readonly GeneticAlgorithmConfig _config;
        private readonly Random _random;
        private PerformanceMetrics _performanceMetrics;

        public GeneticAlgorithmScheduler(List<TimedFluidOperation> operations, 
            List<FluidInstrument> instruments, 
            GeneticAlgorithmConfig config = null)
        {
            _operations = operations;
            _instruments = instruments;
            _config = config ?? new GeneticAlgorithmConfig();
            _random = new Random(_config.RandomSeed);
            _performanceMetrics = new PerformanceMetrics();
        }

        public void AddOperation(FluidOperation operation)
        {
            if (operation is TimedFluidOperation timedOp)
            {
                _operations.Add(timedOp);
            }
            else
            {
                // Convert to TimedFluidOperation with default values
                var timedOperation = new TimedFluidOperation
                {
                    Id = operation.Id,
                    SampleId = operation.SampleId,
                    OperationType = operation.OperationType,
                    VolumeInMicroliters = operation.VolumeInMicroliters,
                    EstimatedDurationMs = operation.EstimatedDurationMs,
                    Priority = operation.Priority,
                    SubmissionTime = operation.SubmissionTime,
                    Deadline = operation.Deadline,
                    SourceLocation = operation.SourceLocation,
                    DestinationLocation = operation.DestinationLocation,
                    EarliestStartTime = operation.SubmissionTime,
                    LatestStartTime = operation.Deadline.AddMilliseconds(-operation.EstimatedDurationMs),
                    SampleType = "Unknown",
                    StabilityTimeMs = 3600000, // 1 hour default
                    CriticalityScore = 0.5
                };
                _operations.Add(timedOperation);
            }
        }

        public void AddOperations(IEnumerable<FluidOperation> operations)
        {
            foreach (var operation in operations)
            {
                AddOperation(operation);
            }
        }

        public async Task<SchedulingSolution> GenerateScheduleAsync()
        {
            _performanceMetrics.MeasurementStart = DateTime.Now;
            
            Console.WriteLine($"[GA] Starting genetic algorithm with population size: {_config.PopulationSize}");
            
            // Initialize population
            var population = InitializePopulation();
            
            // Evolution loop
            for (int generation = 0; generation < _config.MaxGenerations; generation++)
            {
                // Evaluate fitness
                EvaluatePopulation(population);
                
                // Log progress
                var bestFitness = population.Max(c => c.Fitness);
                if (generation % 10 == 0)
                {
                    Console.WriteLine($"[GA] Generation {generation}: Best fitness = {bestFitness:F4}");
                }
                
                // Check termination criteria
                if (HasConverged(population) || bestFitness >= _config.TargetFitness)
                {
                    Console.WriteLine($"[GA] Converged at generation {generation}");
                    break;
                }
                
                // Create next generation
                var newPopulation = new List<Chromosome>();
                
                // Elitism: Keep best solutions
                var eliteCount = (int)(_config.PopulationSize * _config.ElitismRate);
                var elites = population.OrderByDescending(c => c.Fitness).Take(eliteCount).ToList();
                newPopulation.AddRange(elites.Select(e => e.Clone()));
                
                // Generate offspring
                while (newPopulation.Count < _config.PopulationSize)
                {
                    var parent1 = SelectParent(population);
                    var parent2 = SelectParent(population);
                    
                    var (child1, child2) = Crossover(parent1, parent2);
                    
                    if (_random.NextDouble() < _config.MutationRate)
                    {
                        Mutate(child1);
                    }
                    
                    if (_random.NextDouble() < _config.MutationRate)
                    {
                        Mutate(child2);
                    }
                    
                    newPopulation.Add(child1);
                    if (newPopulation.Count < _config.PopulationSize)
                    {
                        newPopulation.Add(child2);
                    }
                }
                
                population = newPopulation;
                
                // Apply local search to best solutions
                if (_config.UseLocalSearch && generation % _config.LocalSearchFrequency == 0)
                {
                    ApplyLocalSearch(population);
                }
            }
            
            // Return best solution
            var bestChromosome = population.OrderByDescending(c => c.Fitness).First();
            var solution = ChromosomeToSolution(bestChromosome);
            
            _performanceMetrics.FinalizeMeasurement();
            Console.WriteLine($"[GA] Completed in {_performanceMetrics.MeasurementDuration.TotalSeconds:F2} seconds");
            
            return solution;
        }

        public async Task<FluidOperation> ScheduleNextAsync()
        {
            var solution = await GenerateScheduleAsync();
            return solution.Assignments.FirstOrDefault()?.ToFluidOperation(_operations);
        }

        public void UpdateConfiguration(SchedulerConfiguration config)
        {
            // Update GA-specific configuration
            if (config.Parameters.ContainsKey("PopulationSize"))
            {
                _config.PopulationSize = (int)config.Parameters["PopulationSize"];
            }
            if (config.Parameters.ContainsKey("MutationRate"))
            {
                _config.MutationRate = (double)config.Parameters["MutationRate"];
            }
            if (config.Parameters.ContainsKey("CrossoverRate"))
            {
                _config.CrossoverRate = (double)config.Parameters["CrossoverRate"];
            }
        }

        public PerformanceMetrics GetPerformanceMetrics()
        {
            return _performanceMetrics;
        }

        private List<Chromosome> InitializePopulation()
        {
            var population = new List<Chromosome>();
            
            for (int i = 0; i < _config.PopulationSize; i++)
            {
                var chromosome = new Chromosome();
                
                // Initialize genes (operation assignments)
                foreach (var operation in _operations)
                {
                    var compatibleInstruments = _instruments
                        .Where(inst => operation.CanExecuteOn(inst))
                        .ToList();
                    
                    if (compatibleInstruments.Count > 0)
                    {
                        var selectedInstrument = compatibleInstruments[_random.Next(compatibleInstruments.Count)];
                        var startTime = GenerateRandomStartTime(operation);
                        
                        var gene = new Gene
                        {
                            OperationId = operation.Id,
                            InstrumentId = selectedInstrument.Id,
                            StartTime = startTime
                        };
                        
                        chromosome.Genes.Add(gene);
                    }
                }
                
                population.Add(chromosome);
            }
            
            return population;
        }

        private DateTime GenerateRandomStartTime(TimedFluidOperation operation)
        {
            var earliestMs = (operation.EarliestStartTime - DateTime.Now).TotalMilliseconds;
            var latestMs = (operation.LatestStartTime - DateTime.Now).TotalMilliseconds;
            
            if (latestMs <= earliestMs)
            {
                return operation.EarliestStartTime;
            }
            
            var randomMs = earliestMs + _random.NextDouble() * (latestMs - earliestMs);
            return DateTime.Now.AddMilliseconds(randomMs);
        }

        private void EvaluatePopulation(List<Chromosome> population)
        {
            Parallel.ForEach(population, chromosome =>
            {
                chromosome.Fitness = EvaluateFitness(chromosome);
            });
        }

        private double EvaluateFitness(Chromosome chromosome)
        {
            var solution = ChromosomeToSolution(chromosome);
            
            // Multi-objective fitness function
            double makespan = solution.TotalExecutionTime;
            double deadlineMisses = CalculateDeadlineMisses(solution);
            double stabilityViolations = CalculateStabilityViolations(solution);
            double resourceUtilization = CalculateResourceUtilization(solution);
            double constraintViolations = CalculateConstraintViolations(solution);
            
            // Normalize objectives (0 to 1, higher is better)
            double makespanScore = 1.0 / (1.0 + makespan / 3600000.0); // Normalize to 1 hour
            double deadlineScore = 1.0 - deadlineMisses;
            double stabilityScore = 1.0 - stabilityViolations;
            double utilizationScore = resourceUtilization;
            double constraintScore = 1.0 - constraintViolations;
            
            // Weighted combination
            double fitness = makespanScore * 0.3 +
                           deadlineScore * 0.25 +
                           stabilityScore * 0.2 +
                           utilizationScore * 0.15 +
                           constraintScore * 0.1;
            
            return Math.Max(0, Math.Min(1.0, fitness));
        }

        private Chromosome SelectParent(List<Chromosome> population)
        {
            // Tournament selection
            var tournamentSize = Math.Min(_config.TournamentSize, population.Count);
            var tournament = new List<Chromosome>();
            
            for (int i = 0; i < tournamentSize; i++)
            {
                var randomIndex = _random.Next(population.Count);
                tournament.Add(population[randomIndex]);
            }
            
            return tournament.OrderByDescending(c => c.Fitness).First();
        }

        private (Chromosome, Chromosome) Crossover(Chromosome parent1, Chromosome parent2)
        {
            var child1 = new Chromosome();
            var child2 = new Chromosome();
            
            // Order crossover (OX) - preserves relative order
            if (_random.NextDouble() < _config.CrossoverRate)
            {
                var crossoverPoint1 = _random.Next(parent1.Genes.Count);
                var crossoverPoint2 = _random.Next(crossoverPoint1, parent1.Genes.Count);
                
                // Copy segment from parent1 to child1
                for (int i = crossoverPoint1; i <= crossoverPoint2; i++)
                {
                    child1.Genes.Add(parent1.Genes[i].Clone());
                }
                
                // Fill remaining positions from parent2
                foreach (var gene in parent2.Genes)
                {
                    if (!child1.Genes.Any(g => g.OperationId == gene.OperationId))
                    {
                        child1.Genes.Add(gene.Clone());
                    }
                }
                
                // Similar for child2
                for (int i = crossoverPoint1; i <= crossoverPoint2; i++)
                {
                    child2.Genes.Add(parent2.Genes[i].Clone());
                }
                
                foreach (var gene in parent1.Genes)
                {
                    if (!child2.Genes.Any(g => g.OperationId == gene.OperationId))
                    {
                        child2.Genes.Add(gene.Clone());
                    }
                }
            }
            else
            {
                // No crossover - copy parents
                child1.Genes.AddRange(parent1.Genes.Select(g => g.Clone()));
                child2.Genes.AddRange(parent2.Genes.Select(g => g.Clone()));
            }
            
            return (child1, child2);
        }

        private void Mutate(Chromosome chromosome)
        {
            // Multiple mutation operators
            var mutationType = _random.Next(4);
            
            switch (mutationType)
            {
                case 0:
                    InstrumentMutation(chromosome);
                    break;
                case 1:
                    TimeMutation(chromosome);
                    break;
                case 2:
                    SwapMutation(chromosome);
                    break;
                case 3:
                    InsertionMutation(chromosome);
                    break;
            }
        }

        private void InstrumentMutation(Chromosome chromosome)
        {
            if (chromosome.Genes.Count == 0) return;
            
            var geneIndex = _random.Next(chromosome.Genes.Count);
            var gene = chromosome.Genes[geneIndex];
            var operation = _operations.FirstOrDefault(op => op.Id == gene.OperationId);
            
            if (operation != null)
            {
                var compatibleInstruments = _instruments
                    .Where(inst => operation.CanExecuteOn(inst) && inst.Id != gene.InstrumentId)
                    .ToList();
                
                if (compatibleInstruments.Count > 0)
                {
                    var newInstrument = compatibleInstruments[_random.Next(compatibleInstruments.Count)];
                    gene.InstrumentId = newInstrument.Id;
                }
            }
        }

        private void TimeMutation(Chromosome chromosome)
        {
            if (chromosome.Genes.Count == 0) return;
            
            var geneIndex = _random.Next(chromosome.Genes.Count);
            var gene = chromosome.Genes[geneIndex];
            var operation = _operations.FirstOrDefault(op => op.Id == gene.OperationId);
            
            if (operation != null)
            {
                var newStartTime = GenerateRandomStartTime(operation);
                gene.StartTime = newStartTime;
            }
        }

        private void SwapMutation(Chromosome chromosome)
        {
            if (chromosome.Genes.Count < 2) return;
            
            var index1 = _random.Next(chromosome.Genes.Count);
            var index2 = _random.Next(chromosome.Genes.Count);
            
            if (index1 != index2)
            {
                var temp = chromosome.Genes[index1].InstrumentId;
                chromosome.Genes[index1].InstrumentId = chromosome.Genes[index2].InstrumentId;
                chromosome.Genes[index2].InstrumentId = temp;
            }
        }

        private void InsertionMutation(Chromosome chromosome)
        {
            if (chromosome.Genes.Count < 2) return;
            
            var fromIndex = _random.Next(chromosome.Genes.Count);
            var toIndex = _random.Next(chromosome.Genes.Count);
            
            if (fromIndex != toIndex)
            {
                var gene = chromosome.Genes[fromIndex];
                chromosome.Genes.RemoveAt(fromIndex);
                chromosome.Genes.Insert(toIndex, gene);
            }
        }

        private void ApplyLocalSearch(List<Chromosome> population)
        {
            // Apply local search to top chromosomes
            var topChromosomes = population.OrderByDescending(c => c.Fitness)
                .Take(_config.LocalSearchCount)
                .ToList();
            
            foreach (var chromosome in topChromosomes)
            {
                LocalSearch(chromosome);
            }
        }

        private void LocalSearch(Chromosome chromosome)
        {
            // Simple hill climbing local search
            var currentFitness = EvaluateFitness(chromosome);
            bool improved = true;
            
            while (improved)
            {
                improved = false;
                var bestNeighbor = chromosome.Clone();
                var bestFitness = currentFitness;
                
                // Try swapping instruments for each operation
                foreach (var gene in chromosome.Genes)
                {
                    var originalInstrument = gene.InstrumentId;
                    var operation = _operations.FirstOrDefault(op => op.Id == gene.OperationId);
                    
                    if (operation != null)
                    {
                        var compatibleInstruments = _instruments
                            .Where(inst => operation.CanExecuteOn(inst) && inst.Id != originalInstrument)
                            .ToList();
                        
                        foreach (var instrument in compatibleInstruments)
                        {
                            gene.InstrumentId = instrument.Id;
                            var neighborFitness = EvaluateFitness(chromosome);
                            
                            if (neighborFitness > bestFitness)
                            {
                                bestFitness = neighborFitness;
                                bestNeighbor = chromosome.Clone();
                                improved = true;
                            }
                        }
                        
                        gene.InstrumentId = originalInstrument; // Restore
                    }
                }
                
                if (improved)
                {
                    chromosome.Genes = bestNeighbor.Genes;
                    currentFitness = bestFitness;
                }
            }
        }

        private bool HasConverged(List<Chromosome> population)
        {
            var fitnessValues = population.Select(c => c.Fitness).ToList();
            var mean = fitnessValues.Average();
            var variance = fitnessValues.Select(f => Math.Pow(f - mean, 2)).Average();
            
            return variance < _config.ConvergenceThreshold;
        }

        private SchedulingSolution ChromosomeToSolution(Chromosome chromosome)
        {
            var solution = new SchedulingSolution();
            
            foreach (var gene in chromosome.Genes)
            {
                var operation = _operations.FirstOrDefault(op => op.Id == gene.OperationId);
                if (operation != null)
                {
                    var assignment = new OperationAssignment
                    {
                        OperationId = gene.OperationId,
                        InstrumentId = gene.InstrumentId,
                        StartTime = gene.StartTime,
                        EndTime = gene.StartTime.AddMilliseconds(operation.EstimatedDurationMs),
                        Cost = operation.EstimatedDurationMs
                    };
                    
                    solution.Assignments.Add(assignment);
                }
            }
            
            solution.TotalExecutionTime = CalculateTotalExecutionTime(solution);
            solution.Cost = CalculateTotalCost(solution);
            solution.Fitness = EvaluateFitness(chromosome);
            solution.IsValid = ValidateSolution(solution);
            
            return solution;
        }

        private double CalculateDeadlineMisses(SchedulingSolution solution)
        {
            int totalOperations = solution.Assignments.Count;
            if (totalOperations == 0) return 0;
            
            int missedDeadlines = 0;
            foreach (var assignment in solution.Assignments)
            {
                var operation = _operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation != null && assignment.EndTime > operation.Deadline)
                {
                    missedDeadlines++;
                }
            }
            
            return (double)missedDeadlines / totalOperations;
        }

        private double CalculateStabilityViolations(SchedulingSolution solution)
        {
            int totalOperations = solution.Assignments.Count;
            if (totalOperations == 0) return 0;
            
            int stabilityViolations = 0;
            foreach (var assignment in solution.Assignments)
            {
                var operation = _operations.FirstOrDefault(op => op.Id == assignment.OperationId);
                if (operation != null)
                {
                    var sampleAge = (assignment.StartTime - operation.SubmissionTime).TotalMilliseconds;
                    if (sampleAge > operation.StabilityTimeMs)
                    {
                        stabilityViolations++;
                    }
                }
            }
            
            return (double)stabilityViolations / totalOperations;
        }

        private double CalculateResourceUtilization(SchedulingSolution solution)
        {
            if (solution.Assignments.Count == 0) return 0;
            
            var instrumentUtilization = new Dictionary<int, double>();
            var totalTime = solution.TotalExecutionTime;
            
            foreach (var assignment in solution.Assignments)
            {
                var operationTime = (assignment.EndTime - assignment.StartTime).TotalMilliseconds;
                if (instrumentUtilization.ContainsKey(assignment.InstrumentId))
                {
                    instrumentUtilization[assignment.InstrumentId] += operationTime;
                }
                else
                {
                    instrumentUtilization[assignment.InstrumentId] = operationTime;
                }
            }
            
            return instrumentUtilization.Values.Average() / totalTime;
        }

        private double CalculateConstraintViolations(SchedulingSolution solution)
        {
            // Check for instrument conflicts and other constraint violations
            var violations = 0;
            var totalConstraints = 0;
            
            // Check for overlapping operations on same instrument
            var instrumentGroups = solution.Assignments.GroupBy(a => a.InstrumentId);
            foreach (var group in instrumentGroups)
            {
                var assignments = group.OrderBy(a => a.StartTime).ToList();
                for (int i = 0; i < assignments.Count - 1; i++)
                {
                    totalConstraints++;
                    if (assignments[i].EndTime > assignments[i + 1].StartTime)
                    {
                        violations++;
                    }
                }
            }
            
            return totalConstraints > 0 ? (double)violations / totalConstraints : 0;
        }

        private double CalculateTotalExecutionTime(SchedulingSolution solution)
        {
            if (solution.Assignments.Count == 0) return 0;
            var minStart = solution.Assignments.Min(a => a.StartTime);
            var maxEnd = solution.Assignments.Max(a => a.EndTime);
            return (maxEnd - minStart).TotalMilliseconds;
        }

        private double CalculateTotalCost(SchedulingSolution solution)
        {
            return solution.Assignments.Sum(a => a.Cost);
        }

        private bool ValidateSolution(SchedulingSolution solution)
        {
            // Basic validation
            return solution.Assignments.All(a => a.EndTime > a.StartTime);
        }
    }

    /// <summary>
    /// Chromosome representation for genetic algorithm
    /// </summary>
    public class Chromosome
    {
        public List<Gene> Genes { get; set; }
        public double Fitness { get; set; }

        public Chromosome()
        {
            Genes = new List<Gene>();
            Fitness = 0;
        }

        public Chromosome Clone()
        {
            return new Chromosome
            {
                Genes = new List<Gene>(Genes.Select(g => g.Clone())),
                Fitness = Fitness
            };
        }
    }

    /// <summary>
    /// Gene representation for operation assignment
    /// </summary>
    public class Gene
    {
        public int OperationId { get; set; }
        public int InstrumentId { get; set; }
        public DateTime StartTime { get; set; }

        public Gene Clone()
        {
            return new Gene
            {
                OperationId = OperationId,
                InstrumentId = InstrumentId,
                StartTime = StartTime
            };
        }
    }

    /// <summary>
    /// Configuration for genetic algorithm
    /// </summary>
    public class GeneticAlgorithmConfig
    {
        public int PopulationSize { get; set; } = 100;
        public int MaxGenerations { get; set; } = 500;
        public double CrossoverRate { get; set; } = 0.8;
        public double MutationRate { get; set; } = 0.1;
        public double ElitismRate { get; set; } = 0.1;
        public int TournamentSize { get; set; } = 5;
        public double TargetFitness { get; set; } = 0.95;
        public double ConvergenceThreshold { get; set; } = 0.001;
        public bool UseLocalSearch { get; set; } = true;
        public int LocalSearchFrequency { get; set; } = 10;
        public int LocalSearchCount { get; set; } = 5;
        public int RandomSeed { get; set; } = 42;
    }

    /// <summary>
    /// Extension methods for operation assignment
    /// </summary>
    public static class OperationAssignmentExtensions
    {
        public static FluidOperation ToFluidOperation(this OperationAssignment assignment, List<TimedFluidOperation> operations)
        {
            return operations.FirstOrDefault(op => op.Id == assignment.OperationId);
        }
    }
}