using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UI;

namespace Pico
{
    namespace Avatar
    {
        namespace Sample
        {
            public class AvatarMirrorDemoStart : PicoAppLaunchBase
            {
                public Text openFTLipsync;
                private bool m_loginPlatformSDK = false;
                private ActionAvatar _actAvatar;
                private bool _openFTAndLipsync = false;
                private bool _faceTrackingRequested;
                private Coroutine _startingFaceTracking;
                private void Awake()
                {
                    System.Action<bool> loginCall = (state) =>
                    {
                        m_loginPlatformSDK = state;
                    };
                    this.SvrPlatformLogin(loginCall);
                }

                IEnumerator Start()
                {
                    this.PXRCheck();

                    while (!m_loginPlatformSDK)
                        yield return null;

                    //waiting PicoAvatarApp finished
                    while (!PicoAvatarApp.isWorking)
                        yield return null;

                    this.PicoAvatarAppStart();

                    //waiting Manager finished
                    while (!PicoAvatarManager.canLoadAvatar)
                        yield return null;


                    //start AvatarLoading
                    var item = GameObject.Find("LocalActionAvatar");
                    if (item)
                    {
                        _actAvatar = item.GetComponent<ActionAvatar>();
                        _actAvatar.StartAvatar(this.UserServiceUserID);
                    }
                    //create mirror
                    var itemMirror = GameObject.Find("LocalActionAvatarMirror");
                    if (itemMirror)
                    {
                        itemMirror.GetComponent<ActionAvatarMirror>().StartAvatar(_actAvatar);
                    }

                }

                public void OpenFTAndLipsync()
                {
#if !UNITY_EDITOR && UNITY_ANDROID
                    if (_actAvatar != null && _actAvatar.Avatar != null 
                                           && _actAvatar.Avatar.entity != null)
                    {
                        if (_startingFaceTracking != null)
                            return;
                        _faceTrackingRequested = !_faceTrackingRequested;
                        if (_faceTrackingRequested)
                            _startingFaceTracking = StartCoroutine(StartFaceTrackingAfterPermission());
                        else
                            SetFaceTracking(false);
                    }
#endif
                }

                private IEnumerator StartFaceTrackingAfterPermission()
                {
                    const string facePermission = "com.picovr.permission.FACE_TRACKING";
                    var missing = new List<string>();
                    if (!Permission.HasUserAuthorizedPermission(facePermission))
                        missing.Add(facePermission);
                    if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                        missing.Add(Permission.Microphone);
                    if (missing.Count != 0)
                    {
                        var remaining = new HashSet<string>(missing);
                        var callbacks = new PermissionCallbacks();
                        callbacks.PermissionGranted += permission => remaining.Remove(permission);
                        callbacks.PermissionDenied += permission => remaining.Remove(permission);
                        callbacks.PermissionDeniedAndDontAskAgain += permission => remaining.Remove(permission);
                        Permission.RequestUserPermissions(missing.ToArray(), callbacks);
                        while (remaining.Count != 0)
                            yield return null;
                    }
                    // Permission dialogs pause XR. Start only after the session resumes.
                    while (!Application.isFocused)
                        yield return null;
                    yield return new WaitForSecondsRealtime(1);
                    _startingFaceTracking = null;
                    if (!Permission.HasUserAuthorizedPermission(facePermission) ||
                        !Permission.HasUserAuthorizedPermission(Permission.Microphone))
                    {
                        _faceTrackingRequested = false;
                        Debug.LogWarning("[AvatarMirror] FT & Lipsync requires face tracking and microphone permissions.");
                        yield break;
                    }
                    SetFaceTracking(_faceTrackingRequested);
                }

                private void SetFaceTracking(bool enabled)
                {
                    var controller = _actAvatar?.Avatar?.entity?.bodyAnimController;
                    if (enabled && controller == null)
                    {
                        _faceTrackingRequested = false;
                        return;
                    }
                    if (enabled)
                        controller.StartFaceTrack(true, true, true);
                    else if (_openFTAndLipsync)
                        controller?.StopFaceTracker();
                    _openFTAndLipsync = enabled;
                    if (openFTLipsync != null)
                        openFTLipsync.text = enabled ? "Close\nFT&Lipsync" : "Open\nFT&Lipsync";
                    Debug.Log($"[AvatarMirror] Avatar native face tracking requested={enabled}");
                }

                private void OnApplicationPause(bool paused)
                {
                    if (paused && _openFTAndLipsync)
                        SetFaceTracking(false);
                    else if (!paused && _faceTrackingRequested && _startingFaceTracking == null)
                        _startingFaceTracking = StartCoroutine(StartFaceTrackingAfterPermission());
                }

                private void OnDisable()
                {
                    _faceTrackingRequested = false;
                    if (_startingFaceTracking != null)
                    {
                        StopCoroutine(_startingFaceTracking);
                        _startingFaceTracking = null;
                    }
                    SetFaceTracking(false);
                }
            }
        }
    }
}


