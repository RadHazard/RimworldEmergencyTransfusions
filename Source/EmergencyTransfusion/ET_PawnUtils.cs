using RimWorld;
using Verse;
using Verse.AI;

namespace EmergencyTransfusion;

public class ET_PawnUtils
{
    
    /// <summary>
    /// Forces a pawn to wait for an indefinite amount of time rather than a fixed amount.
    /// Note that you must manually interrupt this job, as it will never end on its own.
    /// </summary>
    /// <param name="pawn">The pawn to force to wait</param>
    /// <param name="faceTarget">Optional, a thing to face while waiting</param>
    /// <param name="maintainPosture">Whether the pawn should maintain their current posture</param>
    /// <param name="maintainSleep">Whether the pawn should continue sleeping</param>
    /// <param name="reportStringOverride">An optional translated string for the waiting job</param>
    public static void ForceWaitIndefinite(
        Pawn pawn,
        Thing? faceTarget = null,
        bool maintainPosture = false,
        bool maintainSleep = false,
        string? reportStringOverride = null)
    {
        var def = maintainPosture ? JobDefOf.Wait_MaintainPosture : JobDefOf.Wait;
        if (pawn.IsDeactivated())
            def = JobDefOf.Deactivated;
        if (pawn.IsSelfShutdown())
            def = JobDefOf.SelfShutdown;
        else if (pawn.InBed())
            def = pawn.Awake() ? JobDefOf.LayDownAwake : JobDefOf.LayDown;
        else if (!pawn.health.capacities.CanBeAwake)
            def = JobDefOf.Wait_Downed;
        else if (maintainSleep && !pawn.Awake())
            def = JobDefOf.Wait_Asleep;
        
        var newJob = JobMaker.MakeJob(def,faceTarget);
        if (maintainSleep && !pawn.Awake())
        {
            newJob.forceSleep = true;
            newJob.targetA = pawn.Position;
        }
        if (pawn.InBed())
            newJob.targetA = pawn.CurrentBed();
        newJob.reportStringOverride = reportStringOverride;
        pawn.jobs.StartJob(newJob, JobCondition.InterruptForced, resumeCurJobAfterwards: true);
    }
    
}