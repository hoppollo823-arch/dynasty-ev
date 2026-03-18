// GeoEngine Build.cs
// 地球引擎模块 — Cesium集成, 地形生成, 大气系统

using UnrealBuildTool;

public class GeoEngine : ModuleRules
{
    public GeoEngine(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new string[] {
            "Core",
            "CoreUObject",
            "Engine",
            "CesiumRuntime",
            "Cesium3DTiles",
            "Landscape",
            "ProceduralMeshComponent",
            "RHI",
            "RenderCore"
        });
    }
}
