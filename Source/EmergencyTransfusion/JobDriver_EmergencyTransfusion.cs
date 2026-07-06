using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI;

namespace EmergencyTransfusion;

[UsedImplicitly]
public class JobDriver_EmergencyTransfusion : JobDriver
{
    private const TargetIndex PatientIndex = TargetIndex.A;
    private const TargetIndex BloodIndex = TargetIndex.B;
    private const TargetIndex BloodHolderIndex = TargetIndex.C;

    private float transfusionWorkLeft;
    private int ticksSpentTransfusing;
    
    private PathEndMode pathEndMode;
    
    protected Pawn Patient => job.targetA.Pawn;


    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref pathEndMode, "pathEndMode");
    }

    public override void Notify_Starting()
    {
        base.Notify_Starting();
        if (Patient == pawn)
            pathEndMode = PathEndMode.OnCell;
        else if (Patient.InBed())
            pathEndMode = PathEndMode.InteractionCell;
        else
            pathEndMode = PathEndMode.ClosestTouch;

        ticksSpentTransfusing = 0;
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if (!pawn.Reserve(Patient, job, errorOnFailed: errorOnFailed))
            return false;

        for (var i = 0; i < job.targetQueueB.Count; i++)
        {
            if (!pawn.Reserve(job.targetQueueB[i], job, 10, job.countQueue[i]))
                return false;
        }

        return true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(PatientIndex);
        this.FailOnAggroMentalState(PatientIndex);

        foreach (var toil in CollectBloodToils()) yield return toil;

        yield return Toils_Goto.GotoThing(PatientIndex, pathEndMode);
        yield return PerformTransfusion();
        yield return ApplyTransfusionEffects();
    }

    /// <summary>
    /// Makes a loop of toils for collecting all the blood packs
    /// </summary>
    /// <returns>The collection toils</returns>
     protected static IEnumerable<Toil> CollectBloodToils()
    {
        var getNextPack = Toils_JobTransforms.ExtractNextTargetFromQueue(BloodIndex);
        yield return getNextPack;
        
        var gotoBloodHolder = Toils_Goto.GotoThing(BloodIndex, PathEndMode.Touch, true);
        
        var pickUpBlood = Toils_Haul.StartCarryThing(BloodIndex, failIfStackCountLessThanJobCount: true,
            reserve: false,
            canTakeFromInventory: true);
        
        yield return Toils_ET.JumpIfHoldingTarget(BloodIndex, pickUpBlood);
        yield return Toils_ET.JumpIfSomeoneElseHoldingTarget(BloodIndex, gotoBloodHolder, BloodHolderIndex);
        
        // Blood is on the ground
        yield return Toils_Goto.GotoThing(BloodIndex, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(BloodIndex)
            .FailOnBurningImmobile(BloodIndex);
        yield return Toils_Jump.Jump(pickUpBlood);
        
        // Blood is being carried by someone else
        yield return gotoBloodHolder;
        yield return Toils_General.Wait(25).WithProgressBarToilDelay(BloodHolderIndex);
        
        // We are at the blood (or it's in our inventory)
        yield return pickUpBlood;
        yield return Toils_Jump.JumpIfHaveTargetInQueue(BloodIndex, getNextPack);
    }
    
    /// <summary>
    /// A toil representing doing the transfusion work.
    /// Handles the delay, tracking work speed, and freezing the target pawn, but does not apply any actual effects. 
    /// </summary>
    /// <returns></returns>
    private Toil PerformTransfusion()
    {
        var toil = ToilMaker.MakeToil();
        toil.initAction = (Action) (() =>
        {
            var doctor = toil.actor;
            var curJob = doctor.jobs.curJob;
            var curDriver = (JobDriver_EmergencyTransfusion) doctor.jobs.curDriver;
            var patient = curJob.GetTarget(PatientIndex).Pawn;
            
            doctor.pather.StopDead();
            Toils_ET.ForceWaitIndefinite(patient, maintainPosture: true, maintainSleep: true,
                reportStringOverride:"ET_ReceivingTransfusion".Translate());
            
            curDriver.transfusionWorkLeft = ET_DefOf.BloodTransfusion.workAmount;
            curDriver.ticksSpentTransfusing = 0;
        });
        toil.tickIntervalAction = delta =>
        {
            var doctor = toil.actor;
            var curDriver = (JobDriver_EmergencyTransfusion) doctor.jobs.curDriver;

            doctor.rotationTracker.FaceTarget(doctor.CurJob.GetTarget(PatientIndex));
            
            curDriver.ticksSpentTransfusing += delta;
            curDriver.transfusionWorkLeft -= delta * doctor.GetStatValue(ET_DefOf.MedicalOperationSpeed);
            
            if (curDriver.transfusionWorkLeft <= 0.0)
                curDriver.ReadyForNextToil();
        };
        toil.handlingFacing = true;
        toil.activeSkill = () => SkillDefOf.Medicine;
        toil.defaultCompleteMode = ToilCompleteMode.Never;
        toil.WithProgressBar(TargetIndex.A, (Func<float>)(() =>
        {
            var doctor = toil.actor;
            var curDriver = (JobDriver_EmergencyTransfusion) doctor.jobs.curDriver;
            return (float)(1.0 - curDriver.transfusionWorkLeft / ET_DefOf.BloodTransfusion.workAmount);
        }));
        toil.WithEffect(() => EffecterDefOf.Surgery, PatientIndex);
        toil.PlaySustainerOrSound(SoundDefOf.Recipe_Surgery);
        toil.FailOnDespawnedOrNull(PatientIndex);
        toil.FailOnCannotTouch(PatientIndex, pathEndMode);
        toil.AddFinishAction(() =>
        {
            var patient = toil.actor.CurJob.GetTarget(PatientIndex).Pawn;
            patient.jobs.EndCurrentJob(JobCondition.Succeeded);
        });
        return toil;
    }
    
    /// <summary>
    /// A toil that applies the effects of a successful transfusion.
    /// This includes restoring blood, adding hemogen, and training doctor XP
    /// </summary>
    /// <returns></returns>
    private Toil ApplyTransfusionEffects()
    {
        var toil = ToilMaker.MakeToil();
        toil.initAction = (Action) (() =>
        {
            var doctor = toil.actor;
            var patient = doctor.CurJob.targetA.Pawn;
            var bloodpack = doctor.CurJob.targetB.Thing;
            var jobDriver = (JobDriver_EmergencyTransfusion) doctor.jobs.curDriver;
            if (doctor.skills != null)
            {
                var xp = jobDriver.ticksSpentTransfusing * 0.1f * ET_DefOf.BloodTransfusion.workSkillLearnFactor;
                doctor.skills.Learn(SkillDefOf.Medicine, xp);
            }

            // Reduce blood loss
            var bloodLoss = patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss != null)
                bloodLoss.Severity -= bloodpack.stackCount * Recipe_BloodTransfusion.BloodlossHealedPerPack;
        
            // Add hemogen if the pawn is hemogenic
            if (patient.genes?.GetFirstGeneOfType<Gene_Hemogen>() != null)
                GeneUtility.OffsetHemogen(patient, bloodpack.stackCount * JobGiver_GetHemogen.HemogenPackHemogenGain);
        
            bloodpack.Destroy();
            doctor.jobs.EndCurrentJob(JobCondition.Succeeded);
        });
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        toil.atomicWithPrevious = true;
        return toil;
    }
}