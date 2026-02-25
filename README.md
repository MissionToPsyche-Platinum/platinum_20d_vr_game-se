# Psyche Mission: VR Experience

**A virtual reality educational game for NASA's Psyche Mission**




## About

Psyche Mission: VR Experience is a standalone virtual reality educational game that immerses players in NASA's first mission to explore a metal-rich asteroid. Players step into the role of a young space enthusiast who discovers the Psyche mission, visits a Mission Control room, and helps assemble and launch the spacecraft. The game features an Event Mode designed for short, outreach demos (3-5 minutes) and a Normal Mode for free exploration at your own pace.

Learn more about the real mission: [NASA Psyche Mission](https://science.nasa.gov/mission/psyche/)

## Features

- **Event Mode** -- 3-5 minute guided experience ideal for outreach events and museum demos
- **Normal Mode** -- Free exploration with no time constraints
- **Interactive Mission Control props** -- Telescope, computer terminal, display boards, and more
- **Spacecraft assembly puzzle** -- Hands-on puzzle to build the Psyche orbiter from components
- **Real NASA launch footage integration** -- Authentic mission video content

## Getting Started

### Prerequisites

- **Unity 6.3 LTS** (6000.3.x) -- download from [Unity Hub](https://unity.com/download)
- **Git LFS** installed (`git lfs install`)
- **Meta Quest 2 or Quest 3** with Developer Mode enabled
- **Android Build Support** module for Unity (install via Unity Hub)
- **USB-C cable** for ADB deployment

### Setup Instructions

1. Clone the repo:
   
   ```bash
   git clone https://github.com/MissionToPsyche-Platinum/platinum_20d_vr_game-se.git
   ```
2. Ensure Git LFS pulls assets:
   
   ```bash
   git lfs pull
   ```
3. Open Unity Hub, click **Add** and navigate to the cloned project folder.
4. Unity will import assets and regenerate the Library folder (this takes a while on first open).
5. Verify **XR Interaction Toolkit** and **OpenXR** packages are installed via Window > Package Manager.
6. Switch build target to Android: File > Build Settings > Android > Switch Platform.

### Building and Deploying to Quest

1. Open File > Build Settings and ensure Android is the active platform.
2. Connect your Quest headset via USB-C and authorize the computer when prompted on the headset.
3. Click **Build and Run** to build the APK and deploy directly to your Quest.
4. Alternatively, build the APK and install manually via ADB:
   
   ```bash
   adb install build.apk
   ```

For detailed Quest deployment guidance, see the [Unity Meta Quest documentation](https://docs.unity3d.com/Manual/xr-meta-quest.html).

## Project Structure

```
Assets/
├── Art/
│   ├── Animations/
│   ├── Materials/
│   ├── Models/
│   │   ├── Characters/
│   │   ├── Environment/
│   │   │   ├── MissionControl/
│   │   │   └── Bedroom/
│   │   └── Props/
│   │       ├── Telescope/
│   │       ├── Computer/
│   │       ├── DisplayBoard/
│   │       ├── InstructionManual/
│   │       └── SpacecraftPieces/
│   ├── Textures/
│   ├── Shaders/
│   └── UI/
│       ├── Fonts/
│       ├── Icons/
│       └── Sprites/
├── Audio/
│   ├── Music/
│   ├── SFX/
│   └── Voiceover/
├── Prefabs/
│   ├── Environment/
│   ├── Props/
│   ├── UI/
│   └── VR/
├── Scenes/
├── Scripts/
│   ├── Core/
│   │   ├── GameManager/
│   │   └── StateMachine/
│   ├── Gameplay/
│   │   ├── PuzzleEngine/
│   │   └── InteractionHandler/
│   ├── UI/
│   ├── VR/
│   ├── Audio/
│   ├── Data/
│   └── Utils/
├── ScriptableObjects/
│   ├── GameData/
│   └── MissionContent/
├── ThirdParty/
├── Video/
├── Resources/
└── Settings/
    ├── URP/
    └── XR/
```

## Workflow

All work is tracked in Taiga. See [CONTRIBUTING.md](CONTRIBUTING.md) for our branching strategy, commit conventions, and pull request process.

## Team

| Name             | Role                      |
| ---------------- | ------------------------- |
| Zachariah Hintze | Scrum Master              |
| Richard Jaworski | Git Master / Project Lead |
| Nawang Gurung    | Lead Game Designer        |
| Brandon Chong    | Lead VR Coordinator       |
| Ethan Tomasi     | Testing Lead              |

## Acknowledgments

- **Dr. Cassie Bowman** -- Project Sponsor, NASA Psyche Mission, ASU
- **NASA Psyche Mission** -- [science.nasa.gov/mission/psyche](https://science.nasa.gov/mission/psyche/)
- **Arizona State University** -- School of Computing and Augmented Intelligence
