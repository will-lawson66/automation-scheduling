namespace Instrument.Scheduler.Cmr.Execution;

using Instrument.Scheduler.Cmr.Model;
using Instrument.Scheduler.Abstraction;

/// <summary>
///  Class that encapsulates the alogrithm that will generate an execution plan.
///  The algorithm , just groups the list of tests , into groups of technologies and sample types
///  It will always prefer to run a maximum number
/// </summary>
public class CmrExecutionPlanner : IExecutionPlanner<CmrExecutionPlan>
{
    private readonly CmrFile _cmrFile;
    private readonly int _maxTestsScheduled = 4;

    public CmrExecutionPlanner(CmrFile cmrFile)
    {
        _cmrFile = cmrFile;
    }

    public CmrExecutionPlan GetExecutionPlan()
    {
        var groupedTestOrders = GroupAndSplit(_cmrFile.Orders, _maxTestsScheduled, cmrTestOrder => cmrTestOrder.TestMethod);
        var cmrPlanSteps = new List<CmrExecutionStep>();

        foreach (var order in groupedTestOrders)
        {
            cmrPlanSteps.Add(new CmrExecutionStep() { TestOrders = order.AsEnumerable() });
        }

        return new CmrExecutionPlan(cmrPlanSteps);
    }

    public static List<List<CmrTestOrder>> GroupAndSplit(IReadOnlyCollection<CmrTestOrder> source,
                                                int maxSize,
                                                Func<CmrTestOrder, object> groupBySelector)
    {
        return [.. source
            .GroupBy(groupBySelector)
            .Select(group =>
            {
                var groupList = group.ToList();
                var result = new List<List<CmrTestOrder>>();
                for (var i = 0; i < groupList.Count; i += maxSize)
                {
                    result.Add(groupList.GetRange(i, Math.Min(maxSize, groupList.Count - i)));
                }
                return result;
            })
            .SelectMany(x => x)];
    }
}
