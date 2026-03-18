// DynastyVR主模块 Build.cs
// 东方皇朝 EV 元宇宙部

using UnrealBuildTool;

public class DynastyVR : ModuleRules
{
    public DynastyVR(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new string[] {
            "Core",
            "CoreUObject",
            "Engine",
            "InputCore",
            "EnhancedInput",
            "UMG",
            "Slate",
            "SlateCore",
            "OpenXR",
            "HeadMountedDisplay",
            "Niagara",
            "Landscape",
            "ProceduralMeshComponent"
        });

        PrivateDependencyModuleNames.AddRange(new string[] {
            "GeoEngine",
            "RoadEngine",
            "CosmosEngine",
            "BuildSystem",
            "Social"
        });

        // Cesium for Unreal
        PrivateDependencyModuleNames.AddRange(new string[] {
            "CesiumRuntime"
        });
    }
}
