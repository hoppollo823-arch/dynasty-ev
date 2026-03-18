// CosmosEngine Build.cs
// 宇宙引擎模块 — 天体可视化, 黑洞渲染, 星空系统

using UnrealBuildTool;

public class CosmosEngine : ModuleRules
{
    public CosmosEngine(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new string[] {
            "Core",
            "CoreUObject",
            "Engine",
            "Niagara",
            "RHI",
            "RenderCore",
            "Renderer"
        });
    }
}
