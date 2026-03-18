// BuildSystem Build.cs
// 建造系统模块 — UGC工具, 地块系统, 模块化建筑

using UnrealBuildTool;

public class BuildSystem : ModuleRules
{
    public BuildSystem(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new string[] {
            "Core",
            "CoreUObject",
            "Engine",
            "UMG",
            "Slate",
            "SlateCore",
            "ProceduralMeshComponent",
            "PCG"
        });
    }
}
