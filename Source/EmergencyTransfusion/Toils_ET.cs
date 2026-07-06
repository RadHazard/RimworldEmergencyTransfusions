using System;
using Verse;
using Verse.AI;

namespace EmergencyTransfusion;

public class Toils_ET
{
    /// <summary>
    /// Creates a toil that jumps to the given toil if the given toil target is in the pawn's inventory
    /// </summary>
    /// <param name="targetIndex">The index to check</param>
    /// <param name="jumpToil">The toil to jump to</param>
    /// <returns>The toil</returns>
    public static Toil JumpIfHoldingTarget(TargetIndex targetIndex, Toil jumpToil)
    {
        var toil = ToilMaker.MakeToil();
        toil.initAction = (Action) (() =>
        {
            var thing = toil.actor.jobs.curJob.GetTarget(targetIndex).Thing;
            if (toil.actor.inventory.Contains(thing))
                toil.actor.jobs.curDriver.JumpToToil(jumpToil);
        });
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        toil.atomicWithPrevious = true;
        return toil;
    }
    
    /// <summary>
    /// Creates a toil that jumps to the given toil if the given toil target is a different pawn's inventory
    /// </summary>
    /// <param name="targetIndex">The index to check</param>
    /// <param name="jumpToil">The toil to jump to</param>
    /// <param name="newTargetIndex">If set, this index will be replaced with the holder of the item</param>
    /// <returns>The toil</returns>
    public static Toil JumpIfSomeoneElseHoldingTarget(TargetIndex targetIndex, Toil jumpToil,
            TargetIndex newTargetIndex = TargetIndex.None)
    {
        var toil = ToilMaker.MakeToil();
        toil.initAction = (Action) (() =>
        {
            var thing = toil.actor.jobs.curJob.GetTarget(targetIndex).Thing;
            if (thing.ParentHolder is Pawn_InventoryTracker inventory && inventory.pawn != toil.actor)
            {
                toil.actor.jobs.curJob.SetTarget(newTargetIndex, inventory.pawn);
                toil.actor.jobs.curDriver.JumpToToil(jumpToil);
            }
        });
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        toil.atomicWithPrevious = true;
        return toil;
    }
}