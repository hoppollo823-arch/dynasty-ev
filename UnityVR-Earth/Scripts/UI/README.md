# DynastyVR Earth — UI System

> 东方皇朝元宇宙部 | DynastyEV VR Earth UI System

## Architecture

```
UIBootstrap.cs          ← Scene setup helper (optional runtime bootstrapper)
├── MainUIManager.cs    ← Top search bar + left locations + bottom mode switcher
├── HUDManager.cs       ← GPS/Altitude/Speed/FPS overlay
├── SettingsPanel.cs    ← Quality / Time / Weather / Language
├── LocationInfoPanel.cs← Location detail (name + description + photo)
├── UIThemeManager.cs   ← Applies shared USS to any UIDocument
├── UIEventBus.cs       ← Static pub/sub for decoupled communication
├── LocationDatabase.cs ← Location presets with CN/EN metadata
```

## Style

All UI uses **Unity UI Toolkit** (UXML + USS) for VR-friendly scalable UI.

| Asset | Path |
|-------|------|
| USS stylesheet | `Resources/UI/MainUI.uss` |
| UXML template | `Resources/UI/MainUI.uxml` (optional — code builds UI procedurally) |
| Fonts (optional) | `Resources/UI/Fonts/NotoSansSC-Regular.ttf` |
| Location photos | `Resources/UI/LocationPhotos/*.png` |

## Theme

- **Dark translucent** — rgba(20, 20, 30, 0.80) backgrounds
- **Rounded corners** — `border-radius: 8–16px`
- **Accent gold** — `rgb(255, 220, 80)` for headings
- **VR-friendly** — min 14px font, high contrast, no tiny click targets

## Quick Start

1. Add 4 empty GameObjects to your scene: `[MainUI]`, `[HUD]`, `[Settings]`, `[LocationInfo]`
2. Add a `UIDocument` component to each
3. Attach the matching script to each
4. Or use `[UI Bootstrap]` — it creates everything at runtime

## Integrating with Cesium

`UIBootstrap.WireEvents()` auto-wires:
- Search / quick-location clicks → `CesiumEarthManager.FlyToCoordinates()`
- Arrival → `HUDManager.ShowArrivalNotification()`
- Settings → Quality / Time / Weather callbacks
