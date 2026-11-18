This repository contains samples demonstrating the usage of the PICO Avatar SDK.

# Requirement

| Environment                 | Version            |
|-----------------------------|--------------------|
| Unity Editor                | &ge;2022.3, &lt; Unity 6 |
| PICO Avatar SDK             | &ge;6.0.0          |
| PICO Unity SDK (XR)         | &ge;6.0.0          |
| PICO Device's System Version| &ge;6.0.0          |

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
