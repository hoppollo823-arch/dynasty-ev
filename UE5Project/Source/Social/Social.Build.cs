// Social Build.cs
// 社交系统模块 — 多人联机, 语音通信, Avatar

using UnrealBuildTool;

public class Social : ModuleRules
{
    public Social(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new string[] {
            "Core",
            "CoreUObject",
            "Engine",
            "OnlineSubsystem",
            "OnlineSubsystemUtils",
            "UMG",
            "VoiceChat",
            "NetCore"
        });
    }
}
