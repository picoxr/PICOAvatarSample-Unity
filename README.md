This repository contains samples demonstrating the usage of the PICO Avatar SDK.
The Avatar SDK and PICO XR SDK are imported as UPM Git URL dependencies declared in `Packages/manifest.json`.

# Requirement

| Environment                 | Version        |
|-----------------------------|----------------|
| Unity Editor                | 2022.3.62f3    |
| PICO Avatar SDK             | 6.0.0          |
| PICO Unity SDK (XR)         | 6.1.1          |
| Universal Render Pipeline   | 14.0.12        |
| PICO Device's System Version| 6.0.0          |

# Dependencies

The following packages are resolved from Git URLs through the Unity Package Manager:

| Package                | Source |
|------------------------|--------|
| org.byted.avatar.sdk   | https://github.com/Pico-Developer/PICO-Avatar-SDK-Unity.git#V6.0.0 |
| com.bytedance.pico.xr  | https://github.com/Pico-Developer/PICO-Unity-SDK.git?path=/XR#6.1.0 |

# SampleList

The `MenuEntry` scene is the entry point that links to every sample scene below.

| Sample                     | Description                                                                   |
|----------------------------|-------------------------------------------------------------------------------|
| LoadAvatarSample           | Load and render a PICO Avatar.                                                |
| AvatarIKSample             | Avatar inverse kinematics (IK) usage.                                         |
| AvatarMirrorSample         | Mirror and IK synchronization for the avatar.                                 |
| BodyTrackingSample         | Full-body tracking driven by the PICO Body Tracker.                          |
| HandPoseAPISample          | Custom hand pose and gesture API usage.                                       |
| HeightAutoFitAPISample     | Height auto-fit API usage.                                                    |
| DynamicAnimationClipSample | Play and control dynamic animation clips.                                     |
| PacketSyncAPISample        | Network packet data sync reference; the transport layer is not included.      |
| AvatarBunchSample          | Load and manage a bunch of avatars simultaneously.                            |
| CustomPlaceholderAvatar    | Customize the placeholder avatar shown before the real avatar is ready.       |
| PreloadAssetPackage        | Preload avatar asset packages to speed up loading.                            |
| WeakNetworkMode            | Avatar loading behavior under weak network conditions.                        |
