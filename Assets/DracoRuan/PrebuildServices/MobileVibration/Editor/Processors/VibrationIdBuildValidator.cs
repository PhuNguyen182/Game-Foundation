using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace DracoRuan.PrebuildServices.MobileVibration.Editor.Processors
{
    /// <summary>
    /// Fails the build when the generated <c>VibrationId</c> file no longer matches the project.
    /// </summary>
    /// <remarks>
    /// <para>Validation rather than generation, deliberately, for the same reason
    /// <c>AudioIdBuildValidator</c> only validates: generating during a build would compile C# in the
    /// middle of one, which is exactly the domain-reload-inside-a-pipeline problem the postprocessor
    /// avoids. Rendering in memory and comparing gives the same protection with none of the risk.
    /// </para>
    ///
    /// <para>What it catches is "someone forgot to press Generate Ids" — which otherwise ships a
    /// build whose constants disagree with its assets.</para>
    /// </remarks>
    public sealed class VibrationIdBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            VibrationIdIndex.Invalidate();
            VibrationIdGenerationPlan plan = VibrationIdGenerationService.BuildPlan();

            if (!plan.CanApply)
                throw new BuildFailedException(
                    "The vibration identifiers cannot be generated, so this build would ship stale ones: "
                    + plan.BlockingError);

            if (plan.IsNoOp)
                return;

            throw new BuildFailedException(
                $"{plan.TargetPath} does not match the VibrationEntry assets in the project "
                + $"({plan.AddedIds.Count} to add, {plan.RemovedIds.Count} to remove). "
                + "Run Tools > Foundations > Vibration Editor > Regenerate Vibration Ids and build again.");
        }
    }
}
