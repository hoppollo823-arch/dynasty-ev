// RoadEngine Build.cs
// 路网引擎模块 — OSM路网, 车辆物理(Chaos), 路线规划

using UnrealBuildTool;

public class RoadEngine : ModuleRules
{
    public RoadEngine(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new string[] {
            "Core",
            "CoreUObject",
            "Engine",
            "ChaosVehicles",
            "ChaosSolverEngine",
            "Landscape",
            "ProceduralMeshComponent",
            "NavigationSystem"
        });
    }
}
