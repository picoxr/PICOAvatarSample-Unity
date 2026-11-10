using Pico.Avatar.Sample;
using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;

namespace Pico
{
    namespace Avatar
    {
        public class PreloadAssetPackageSample : PicoAppLaunchBase
        {
            #region Public Fields

            [Tooltip("base path name of preload package.")]
            public string packagePathName = "PicoAvatar/PreloadPackages/PackageA";

            // preload package resource location.
            [Tooltip("resource root type as ResourceRootType.")]
            public ResourceRootType resRootType = ResourceRootType.Bin;

            [Tooltip("avatar specification text to load.")]
            public string avatarSpecText;

            [Tooltip("provide avatar specifications.")]
            public PicoAvatarPreloadAssetPackageImportAsset importAsset;

            [Tooltip("click button to load avatar.")]
            public UnityEngine.UI.Button btnLoadAvatar;

            [Tooltip("radius to spawn avatar in meters.")]
            public float avatarSpawnRange = 3.0f;

            [Tooltip("center position to spawn avatar.")]
            public Vector3 avatarSpawnCenter = new Vector3(0.0f, 0.0f, 1.5f);
            #endregion


            #region Framework Methods

            private void Awake()
            {
                System.Action<bool> loginCall = (state) =>
                {
                    m_loginPlatformSDK = state;
                };
                this.SvrPlatformLogin(loginCall);
            }

            // Start is called before the first frame update
            void Start()
            {
                // disable load avatar button first.
                if(btnLoadAvatar != null)
                {
                    btnLoadAvatar.enabled = false;
                    var uiText = btnLoadAvatar.GetComponentInChildren<UnityEngine.UI.Text>();
                    if (uiText != null)
                    {
                        uiText.text = "Preload package is loading...";
                    }
                }
                this.StartCoroutine(StartLoad());
            }

            // Update is called once per frame
            void Update()
            {
                _animationFollowSync.Update();
            }

            IEnumerator StartLoad()
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

               yield return Coroutine_DoInitialize();
            }

            private void OnDestroy()
            {
                if(_preloadPackage != null)
                {
                    AvatarPreloadAssetPackageSet.intance.RemovePreloadPackage(_preloadPackage);
                    _preloadPackage = null;
                }
            }
            #endregion


            #region Private Fields

            private bool m_loginPlatformSDK = false;

            private int _lastUserIndex = 0;

            private ActionAvatar _mainActionAvatar = null;

            private AvatarPreloadAssetPackage _preloadPackage = null;

            private LocalAnimationFollowSync _animationFollowSync = new LocalAnimationFollowSync();
            #endregion


            #region Private Methods

            private IEnumerator Coroutine_DoInitialize()
            {
                string packageFullPathName = packagePathName;

#if UNITY_EDITOR
                if(resRootType == ResourceRootType.Bin)
                {
                    //packageFullPathName = "PicoAvatarSamples/SampleAndroidXRModel/PreloadAssetPackage/Resources/" + packagePathName;
                    var manifestAsset = Resources.Load<TextAsset>(packagePathName + "/PackageManifest.json");
                    if(manifestAsset == null || string.IsNullOrEmpty(manifestAsset.text))
                    {
                        AvatarEnv.Log(DebugLogMask.GeneralError, packagePathName + " MUST be located in Resources/ ");
                        UnityEditor.EditorUtility.DisplayDialog("Error", packagePathName + " MUST be located in Resources/ ", "OK");
                        yield break;
                    }
                }
                else if(resRootType == ResourceRootType.ReadOnlyAssets)
                {
                    if (!System.IO.Directory.Exists(Application.streamingAssetsPath + "/" + packagePathName))
                    {
                        AvatarEnv.Log(DebugLogMask.GeneralError, packagePathName + " MUST be located in Assets/StreamingAssets/ ");
                        UnityEditor.EditorUtility.DisplayDialog("Error", packagePathName + " MUST be located in Assets/StreamingAssets/ ", "OK");
                        yield break;
                    }
                }
                else if (resRootType == ResourceRootType.LocalFileSystem)
                {
                    if (!System.IO.Directory.Exists(packagePathName))
                    {
                        AvatarEnv.Log(DebugLogMask.GeneralError, packagePathName + " MUST be an existing absolute path name!");
                        UnityEditor.EditorUtility.DisplayDialog("Error", packagePathName + " MUST be an existing absolute path name!", "OK");
                        yield break;
                    }
                }
#else
                packageFullPathName = packagePathName;
#endif
                while (!PicoAvatarManager.canLoadAvatar)
                {
                    yield return null;
                }
                //
                _preloadPackage = new AvatarPreloadAssetPackage("PackageA", packageFullPathName, resRootType);
                AvatarPreloadAssetPackageSet.intance.AddPreloadPackage(_preloadPackage, (success) => { 
                
                    if(PicoAvatarManager.canLoadAvatar && _preloadPackage != null)
                    {
                        if(success)
                        {
                            // disable load avatar button first.
                            if (btnLoadAvatar != null)
                            {
                                btnLoadAvatar.enabled = true;
                                var uiText = btnLoadAvatar.GetComponentInChildren<UnityEngine.UI.Text>();
                                if(uiText != null)
                                {
                                    uiText.text = "Click To Add Avatar With preload package.";
                                }
                                //btnLoadAvatar.
                            }
                        }
                        else
                        {
                            AvatarEnv.Log(DebugLogMask.GeneralError, "Failed to add preload package!");
                        }
                    }
                });
            }

            /**
             * @brief load avatar. Invoked from UI.
             */
            [Preserve()]
            public void LoadAvatar()
            {
                if (_preloadPackage != null && _preloadPackage.isAddedToPackageSet && 
                    PicoAvatarManager.canLoadAvatar && 
                    (!string.IsNullOrEmpty(avatarSpecText) || importAsset != null))
                {
                    if(_mainActionAvatar == null)
                    {
                        var go = GameObject.Find("MainActionAvatar");
                        if(go != null)
                        {
                            _mainActionAvatar = go.GetComponent<ActionAvatar>();
                            _mainActionAvatar.SetAsMainAvatar();

                            _mainActionAvatar.StartJsonAvatar("_PackageAvatarId_Main", avatarSpecText, null);
                            //
                            InitEditorInputDevice("_PackageAvatarId_Main");

                            //
                            _animationFollowSync.SetLeadAvatar(_mainActionAvatar.Avatar);
                        }
                        return;
                    }

                    // load avatar.
                    var capability = new AvatarCapabilities();
                    capability.manifestationType = AvatarManifestationType.Full;
                    capability.controlSourceType = ControlSourceType.OtherPlayer;
                    capability.bodyCulling = false;
                    capability.recordBodyAnimLevel = RecordBodyAnimLevel.FullBlendShape;
                    capability.enablePlaceHolder = false; //set Enable PlaceHolder.
                    capability.autoStopIK = true; //set automatically stop ik when controller is far,idle,etc.
                    capability.ikMode = AvatarIKMode.FullBody;

                    var avatarSpecJsonText = avatarSpecText;
                    if (importAsset != null && importAsset.avatarSpecTexts != null && importAsset.avatarSpecTexts.Length > 0)
                    {
                        var specIndex = _lastUserIndex % importAsset.avatarSpecTexts.Length;
                        avatarSpecJsonText = importAsset.avatarSpecTexts[specIndex];
                    }

                    //if (isMainAvatar)
                    //{
                    //    capability.flags |= (uint)AvatarCapabilities.Flags.MainAvatar;
                    //}
                    //if (allowEdit)
                    //{
                    //    capability.flags |= (uint)AvatarCapabilities.Flags.AllowEdit;
                    //}
                    //if (enableFacialExpressionTransfer)
                    //{
                    //    capability.flags |= (uint)AvatarCapabilities.Flags.EnableFaceExpressionTransfer;
                    //}
                    
                    {
                        var avatar = PicoAvatarManager.instance.LoadAvatar(new AvatarLoadContext("_PackageAvatarId_" + _lastUserIndex++.ToString(), "", avatarSpecJsonText, capability),
                            (avatar, avatarEntity) => {

                                float avatarDistX = UnityEngine.Random.Range(0.0f, avatarSpawnRange);
                                float avatarDistY = UnityEngine.Random.Range(0.0f, avatarSpawnRange * 0.5f);
                                float avatarDistZ = UnityEngine.Random.Range(0.0f, avatarSpawnRange);
                                //
                                avatar.transform.localPosition = avatarSpawnCenter + new Vector3(avatarDistX, avatarDistY, avatarDistZ);

                                //
                                avatar.PlayAnimation("idle", 0, "BottomLayer");

                                //
                                _animationFollowSync.AddFollowAvatar(avatar);
                            });
                    }
                }
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
