using Newtonsoft.Json;
using Pico.Avatar.Sample;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Pico
{
    namespace Avatar
    {
        public class AvatarBunchSample : PicoAppLaunchBase
        {
            #region Public Fields

            //
            [Tooltip("list of avatar specification text")]
            public string[] _avatarSpecTextList;

            [Tooltip("from which lod level to use avatar bunch.Currently should be same to maxLodLevel set if want to enable AvatarBunch, until skeleton retarget is supported.")]
            [HideInInspector()]
            public AvatarLodLevel avatarBunchLodLevel = AvatarLodLevel.Lod0;

            // how many target avatars to create for a bunch.
            public int avatarCountPerBunch = 15;

            [Tooltip("radius to spawn avatar in meters.")]
            public float avatarSpawnRange = 3.0f;

            [Tooltip("center position to spawn avatar.")]
            public Vector3 avatarSpawnCenter = new Vector3(0.0f, 0.0f, 1.5f);

            [Tooltip("center positions used to spawn avatars.")]
            public Transform[] avatarBunchPositions;

            //public Lab
            public Text avatarBunchDescLabel;

            [Tooltip("toggle to switch enableAnimationSync.")]
            public Toggle enableAnimationFollowSyncToggle;

            //public Lab
            public bool needUpdateAvatarBunchMaterialProperties = false;

            [Tooltip("whether enable animation playback sync")]
            public bool enableAnimationSync = true;

            [Tooltip("whether enable update avatar bunch target skeleton")]
            public bool forceUpdateSkeleton = false;

            [Tooltip("whether enable random color")]
            public bool enableRandomColor = false;

            [Tooltip("animation asset bundle path.. can be empty")]
            public string animationAssetBundlePathName = "";

            [Tooltip("animation asset bundle bytes. require animationAssetBundlePathName MUST be empty")]
            public TextAsset animationAssetBundleTextAsset;

            [Tooltip("animation asset bundle path. can be empty")]
            public string animationListString = "";
            #endregion


            #region UI Interaction

            [Serializable]
            public class AnimationNameList
            {
                public string[] animNames;
            }

            /**
             * @brief Adds an batch of avatar. Invoked from GUI.
             */
            public void OnBtnAddTargetAvatarForAvatarBunch(int bounchIndex = -1)
            {
                if(_bunchList.Count == 0 || _avatarSpecTextList == null || _avatarSpecTextList.Length == 0)
                {
                    return;
                }

                if(bounchIndex < 0)
                {
                    if(_nextBunchIndex >= _bunchList.Count)
                    {
                        _nextBunchIndex = 0;
                    }
                    bounchIndex = _nextBunchIndex++;
                }

                for (int i = 0; i < avatarCountPerBunch; ++i)
                {
                    var avatarId = "__target_avata_" + _nextTargetAvatarId++.ToString();
                    var capability = new AvatarCapabilities();
                    capability.manifestationType = AvatarManifestationType.Full;
                    capability.controlSourceType = ControlSourceType.OtherPlayer;
                    capability.bodyCulling = false;
                    capability.recordBodyAnimLevel = RecordBodyAnimLevel.BasicBone;
                    capability.enablePlaceHolder = false;
                    capability.autoStopIK = true; //set automatically stop ik when controller is far,idle,etc.
                    capability.ikMode = AvatarIKMode.FullBody;
                    capability.flags = 0;
                    capability.avatarBunchLodLevel = avatarBunchLodLevel;

                    var loadCtx = new AvatarLoadContext(avatarId, avatarId, _avatarSpecTextList[bounchIndex], capability);
                    var avatar = PicoAvatarManager.instance.LoadAvatar(loadCtx, (avatar, avatarEntity) =>
                    {
                        if(avatarEntity == null || _bunchList == null || _bunchList.Count <= bounchIndex)
                        {
                            return;
                        }
                        avatar.PlayAnimation("idle");
                        //
                        avatarEntity.SetAvatarBunch(_bunchList[bounchIndex]);

                        // load animation.
                        if (!enableAnimationSync)
                        {
                            avatar.OnLoadAnimationsExternComplete += (name, result) => {
                                if(!string.IsNullOrEmpty(result))
                                {
                                    try
                                    {
                                        var text = "{ \"animNames\": " + result + "}";
                                        var rootObject = JsonUtility.FromJson<AnimationNameList>(text);
                                        if(rootObject != null && rootObject.animNames != null)
                                        {
                                            foreach (var animName in rootObject.animNames)
                                            {
                                                {
                                                    _animationNameSets.Add(animName);
                                                }
                                            }
                                        }
                                    }
                                    catch(System.Exception ex)
                                    {
                                        UnityEngine.Debug.LogException(ex);
                                    }
                                }
                            };

                            if (!string.IsNullOrEmpty(animationAssetBundlePathName) && System.IO.File.Exists(animationAssetBundlePathName))
                            {
                                avatar.LoadAnimationsExtern(animationAssetBundlePathName, animationListString);
                            }
                            else if (animationAssetBundleTextAsset != null && animationAssetBundleTextAsset.bytes != null)
                            {
                                if(_avatarAssetBundle == null)
                                {
                                    _avatarAssetBundle = new AvatarAssetBundle();
                                    _avatarAssetBundle.Retain();

                                    var mv = new MemoryView(animationAssetBundleTextAsset.bytes, false);
                                    _avatarAssetBundle.LoadFromZipMemoryView(mv);
                                    mv.CheckDelete();
                                }
                                avatar.LoadAnimationsFromAssetBundle(_avatarAssetBundle, animationListString);
                            }
                        }
                        else
                        {
                            // disable animator if follow and sync animation with main avatar.
                            avatarEntity.bodyAnimController.SetEnableAnimator(false);
                        }
                        
                       
                        // add to follow sync
                        if (enableAnimationSync)
                        {
                            _animationFollowSync.AddFollowAvatar(avatarEntity.owner);
                        }
                    });
                    if (avatar == null)
                    {
                        return;
                    }
                    // sets whether force update skeleton.
                    avatar.forceUpdateSkeleton = forceUpdateSkeleton;
                    //
                    _targetAvatarList.Add(avatar);
                   

                    var basePosition = avatarSpawnCenter;
                    if (avatarBunchPositions != null && avatarBunchPositions.Length > 0)
                    {
                        var bunchPosIndex = _nextTargetBunchPositionIndex++;
                        if (bunchPosIndex >= avatarBunchPositions.Length)
                        {
                            bunchPosIndex = 0;
                            _nextTargetBunchPositionIndex = 0;
                        }
                        basePosition = avatarBunchPositions[bunchPosIndex] == null? avatarSpawnCenter : avatarBunchPositions[bunchPosIndex].transform.position;
                    }
                    float avatarDistX = UnityEngine.Random.Range(0.0f, avatarSpawnRange);
                    float avatarDistY = UnityEngine.Random.Range(0.0f, avatarSpawnRange * 0.5f);
                    float avatarDistZ = UnityEngine.Random.Range(0.0f, avatarSpawnRange);
                    //
                    avatar.transform.localPosition = basePosition + new Vector3(avatarDistX, avatarDistY, avatarDistZ);

                    // update static count description ui text.
                    if(avatarBunchDescLabel != null)
                    {
                        int bounchAvatarCount = 0;
                        if (_bunchAvatarCounts.TryGetValue(bounchIndex, out bounchAvatarCount))
                        {
                            _bunchAvatarCounts[bounchIndex]++;
                        }
                        else
                        {
                            _bunchAvatarCounts[bounchIndex] = 1;
                        }
                        bounchAvatarCount++;

                        int totalAvatarCount = 0;
                        var sb = new StringBuilder();
                        foreach (var x in _bunchAvatarCounts)
                        {
                            sb.AppendLine(string.Format("bounchIndex: {0} avatarCount: {1}", x.Key, x.Value));
                            totalAvatarCount += x.Value;
                        }
                        sb.AppendLine("--------------------");
                        sb.AppendLine(string.Format("total count: {0}", totalAvatarCount));

                        avatarBunchDescLabel.text = sb.ToString();
                    }
                }
            }

            /**
             * @brief Unload all avatar. Invoked from GUI.
             */
            public void OnBtnUnloadAllAvatar()
            {
                // reset follw synchronizer.
                _animationFollowSync.Reset();

                if(_mainActionAvatar != null)
                {
                    _animationFollowSync.SetLeadAvatar(_mainActionAvatar.Avatar);
                }
                // unload all bunch avatars.
                foreach (var avatar in this._targetAvatarList)
                {
                    PicoAvatarManager.instance.UnloadAvatar(avatar);
                }
                this._targetAvatarList.Clear();
            }

            public void OnToggleEnableAnimationFollowSync()
            {
                enableAnimationSync = enableAnimationFollowSyncToggle.isOn;
            }

            /**
             * @brief Play next animation. Invoked from GUI.
             */
            public void OnBtnPlayNextAnimation()
            {
                if(_animationNameSets != null && _animationNameSets.Count > 0)
                {
                    this._nextAnimationNameIndex = ++this._nextAnimationNameIndex % _animationNameSets.Count;

                    int index = 0;
                    foreach(var animName in _animationNameSets)
                    {
                        if(index++ == this._nextAnimationNameIndex)
                        {
                            foreach(var avatar in _targetAvatarList)
                            {
                                if(avatar.entity != null &&
                                    avatar.entity.bodyAnimController != null &&
                                    avatar.entity.bodyAnimController.isAnimatorEnabled)
                                {
                                    avatar.PlayAnimation(animName);
                                }
                            }
                            break;
                        }
                    }
                }
            }

            #endregion

            #region Private Fields

            private bool m_loginPlatformSDK = false;

            private AvatarBunchGroup _bunchGroup;
            private List<AvatarBunch> _bunchList = new List<AvatarBunch>();
            private List<PicoAvatar> _targetAvatarList = new List<PicoAvatar>();
            private static int _nextAvatarBounchId = 0;
            private static int _nextTargetAvatarId = 0;
            private int _nextBunchIndex = 0;
            private int _nextTargetBunchPositionIndex = 0;
            private Dictionary<int, int> _bunchAvatarCounts = new Dictionary<int, int>();

            private ActionAvatar _mainActionAvatar = null;

            private LocalAnimationFollowSync _animationFollowSync = new LocalAnimationFollowSync();

            Pico.Avatar.AvatarAssetBundle _avatarAssetBundle = null;

            private HashSet<string> _animationNameSets = new HashSet<string>();
            private int _nextAnimationNameIndex = 0;
            #endregion


            #region Private Methods

            private void DoInitialize()
            {
                // sync ui.
                if(enableAnimationFollowSyncToggle != null)
                {
                    enableAnimationFollowSyncToggle.isOn = enableAnimationSync;
                }

                if (_bunchGroup == null)
                {
                    _bunchGroup = PicoAvatarManager.instance.AddAvatarBunchGroup();
                }
                
                if(_avatarSpecTextList != null && _avatarSpecTextList.Length > 0)
                {
                    foreach(var specText in _avatarSpecTextList)
                    {
                        var avatarId = "__bunch_avatar_" + _nextAvatarBounchId++.ToString();
                        var bunch =  _bunchGroup.GetAvatarBunch(avatarId);
                        _bunchList.Add(bunch);
                        //
                        {
                            var capability = new AvatarCapabilities();
                            capability.manifestationType = AvatarManifestationType.Full;
                            capability.controlSourceType = ControlSourceType.OtherPlayer;
                            capability.bodyCulling = false;
                            capability.recordBodyAnimLevel = RecordBodyAnimLevel.BasicBone;
                            capability.enablePlaceHolder = false;
                            capability.autoStopIK = true; //set automatically stop ik when controller is far,idle,etc.
                            capability.ikMode = AvatarIKMode.FullBody;
                            capability.flags = (int)AvatarCapabilities.Flags.AvatarBunchSource;
                            //
                            if(enableRandomColor)
                            {
                                bunch.materialProvider = new MyMaterialProvider();
                            }
                            //
                            var loadCtx = new AvatarLoadContext(avatarId, avatarId, specText, capability);
                            bunch.LoadAvatar(loadCtx);
                        }
                    }

                    //
                    if (_mainActionAvatar == null)
                    {
                        var go = GameObject.Find("MainActionAvatar");
                        if (go != null)
                        {
                            _mainActionAvatar = go.GetComponent<ActionAvatar>();
                            _mainActionAvatar.SetAsMainAvatar();

                            _mainActionAvatar.StartJsonAvatar("_PackageAvatarId_Main", _avatarSpecTextList[0], null);
                            //
                            InitEditorInputDevice("_PackageAvatarId_Main");

                            //
                            _animationFollowSync.SetLeadAvatar(_mainActionAvatar.Avatar);
                        }
                        return;
                    }
                }
            }

            private void Update()
            {
                if(needUpdateAvatarBunchMaterialProperties)
                {
                    needUpdateAvatarBunchMaterialProperties = false;
                    foreach(var x in _bunchList)
                    {
                        x.SetNeedUpdateMaterialProperties();
                    }
                }

                _animationFollowSync.Update();
            }

            void OnDestroy()
            {
                // release avatar asset bundle.
                if(_avatarAssetBundle != null)
                {
                    _avatarAssetBundle.Release();
                    _avatarAssetBundle = null;
                }

                if (_bunchGroup != null && PicoAvatarManager.instance != null)
                {
                    PicoAvatarManager.instance.RemoveAvatarBunchGroup(_bunchGroup);
                    _bunchGroup = null;
                }
            }
            #endregion


            #region Material Provider

            /**
             * @brief The class shows how to takeover avatar materials when rendered use AvatarBunch with gpu instancing.
             */
            class MyMaterialProvider : AvatarBunch.MaterialProvider
            {
                // get material for bunch primitive. the returned material MUST has perperty of "_PAV_PerInstanceBufferSize"
                public Material GetBunchPrimitiveMaterial(AvatarBunch bunch, AvatarPrimitive avatarPrimitive, Material srcMaterial)
                {
                    return null;
                }

                // fill per instance material data.
                public void UpdateMaterialProperties(AvatarPrimitive avatarPrimitive, List<AvatarBunchItem> renderableBunchItems,
                    MaterialPropertyBlock materialPropertyBlock, Material material)
                {
                    var additiveGIs = new List<float>();
                    var baseColors = new List<Vector4>();
                    additiveGIs.Capacity = renderableBunchItems.Count;
                    baseColors.Capacity = renderableBunchItems.Count;
                    foreach (var x in renderableBunchItems)
                    {
                        additiveGIs.Add(UnityEngine.Random.Range(0.1f, 1.0f));
                        baseColors.Add(new Vector4(UnityEngine.Random.Range(0.1f, 1.0f),
                            UnityEngine.Random.Range(0.1f, 1.0f),
                            UnityEngine.Random.Range(0.1f, 1.0f), 
                            1.0f));
                    }
                    materialPropertyBlock.SetFloatArray("_AdditiveGI", additiveGIs);
                    materialPropertyBlock.SetVectorArray("_BaseColor", baseColors);
                }
            }
            #endregion


            #region Framework

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
                PXRCheck();

                while (!m_loginPlatformSDK)
                    yield return null;

                //waiting PicoAvatarApp finished
                while (!PicoAvatarApp.isWorking)
                    yield return null;

                this.PicoAvatarAppStart();

                //waiting Manager finished
                while (!PicoAvatarManager.canLoadAvatar)
                    yield return null;

                DoInitialize();
            }

            void InitEditorInputDevice(string userID)
            {
                var item = GameObject.Find("EditorInputDeviceSample");
                if (item)
                {
                    var input = item.GetComponent<EditorInputDevice>();
                    input.DeInit();
                    input.UserId = userID;
                    input.useEditorTarget = true;
                    input.Init();
                }
            }
            #endregion
        }
    }
}