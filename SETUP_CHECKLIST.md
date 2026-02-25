# New Team Member Setup Checklist

## Prerequisites
- [ ] Install Unity Hub and Unity 6.3 LTS (6000.3.x)
- [ ] During Unity install, add Android Build Support (with Android SDK & NDK, OpenJDK)
- [ ] Install Git (https://git-scm.com/)
- [ ] Install Git LFS (`git lfs install` -- run once globally)
- [ ] Install Blender (if doing 3D modeling) (https://www.blender.org/)

## Repository Setup
- [ ] Clone the repo: `git clone https://github.com/MissionToPsyche-Platinum/platinum_20d_vr_game-se.git`
- [ ] Verify LFS: `git lfs pull`
- [ ] Open project in Unity Hub (Add > navigate to cloned folder)
- [ ] Wait for initial asset import to complete (may take 5-10 minutes)
- [ ] Verify no console errors in Unity

## Unity Project Verification
- [ ] Confirm build target is Android (File > Build Settings)
- [ ] Confirm XR Interaction Toolkit is in Package Manager
- [ ] Confirm OpenXR plugin is in Package Manager
- [ ] Confirm Asset Serialization is set to Force Text
- [ ] Confirm Version Control Mode is set to Visible Meta Files

## Quest Development (if testing on device)
- [ ] Enable Developer Mode on Quest (Settings > System > Developer)
- [ ] Install ADB (included with Android SDK from Unity install)
- [ ] Connect Quest via USB-C, authorize the computer on the headset
- [ ] Verify connection: `adb devices` shows your Quest

## Taiga
- [ ] Read CONTRIBUTING.md to understand how Taiga refs connect to Git branches and commits
