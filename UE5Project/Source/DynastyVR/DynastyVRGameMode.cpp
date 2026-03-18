// DynastyVRGameMode.cpp — 东方皇朝 DynastyVR GameMode
#include "DynastyVRGameMode.h"
#include "DynastyVRPlayerController.h"

ADynastyVRGameMode::ADynastyVRGameMode()
{
    PlayerControllerClass = ADynastyVRPlayerController::StaticClass();
    DefaultPawnClass = nullptr; // Set in Blueprint (VR Pawn)
}
