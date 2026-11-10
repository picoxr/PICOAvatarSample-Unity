using Pico.Avatar.Sample;
using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;

namespace Pico
{
    namespace Avatar
    {
        public class CustomPlaceholderAvatarSample : PicoAppLaunchBase
        {
            #region Public Fields

            [Tooltip("placeholder avatar specification text.")]
            [SerializeField]
            public string placeHolderAvatarSpecText;

            [Tooltip("provide avatar specifications list.")]
            // avatar specification texts
            [SerializeField]
            public string[] otherAvatarSpecTexts;

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
                this.StartCoroutine(StartLoad());
            }

            // Update is called once per frame
            void Update()
            {
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
            }
            #endregion


            #region Private Fields

            private bool m_loginPlatformSDK = false;

            private int _lastUserIndex = 0;

            private ActionAvatar _mainActionAvatar = null;

            private PicoPlaceholderAvatar _otherAvatarPlaceHolderAvatar = null;
            #endregion


            #region Private Methods

            private IEnumerator Coroutine_DoInitialize()
            {
                while (!PicoAvatarManager.canLoadAvatar)
                {
                    yield return null;
                }

                // load main avatar.
                if (_mainActionAvatar == null)
                {
                    var go = GameObject.Find("MainActionAvatar");
                    if (go != null)
                    {
                        _mainActionAvatar = go.GetComponent<ActionAvatar>();
                        _mainActionAvatar.SetAsMainAvatar();
                        _mainActionAvatar.StartAvatar(_mainActionAvatar.userId);
                        //
                        InitEditorInputDevice("_PackageAvatarId_Main");
                    }
                }

                // load place holder
                if(!string.IsNullOrEmpty(placeHolderAvatarSpecText))
                {
                    // load avatar.
                    var capability = new AvatarCapabilities();
                    capability.manifestationType = AvatarManifestationType.Full;
                    capability.controlSourceType = ControlSourceType.OtherPlayer;
                    capability.bodyCulling = false;
                    capability.recordBodyAnimLevel = RecordBodyAnimLevel.FullBlendShape;
                    capability.enablePlaceHolder = false; //set Enable PlaceHolder.
                    capability.autoStopIK = true; //set automatically stop ik when controller is far,idle,etc.
                    capability.ikMode = AvatarIKMode.None;
                    capability.headShowType = AvatarHeadShowType.Normal;

                    // allow load from avatar meta cache when network is weak.
                    capability.usage = AvatarCapabilities.Usage.AsPlaceHolder;
                    // force use lod0
                    capability.forceLodLevel = AvatarLodLevel.Lod0;
                    {
                        _otherAvatarPlaceHolderAvatar = PicoAvatarManager.instance.LoadAvatar(new AvatarLoadContext("_OtherPlaceHolderAvatarId_" + _lastUserIndex++.ToString(),
                            "", placeHolderAvatarSpecText, capability), (avatar, avatarEntity) => {
                            _otherAvatarPlaceHolderAvatar.PlayAnimation("idle", 0, "BottomLayer");
                            _otherAvatarPlaceHolderAvatar.ForceUpdate();
                        }) as PicoPlaceholderAvatar;
             
                        _otherAvatarPlaceHolderAvatar.transform.localPosition = Vector3.zero;
                        _otherAvatarPlaceHolderAvatar.transform.localScale = Vector3.zero;
                    }
                }
            }

            /**
             * @brief load avatar. Invoked from UI.
             */
            [Preserve()]
            public void LoadAvatar()
            {
                if (PicoAvatarManager.canLoadAvatar && otherAvatarSpecTexts != null && otherAvatarSpecTexts.Length > 0)
                {
                    // load avatar.
                    var capability = new AvatarCapabilities();
                    capability.manifestationType = AvatarManifestationType.Full;
                    capability.controlSourceType = ControlSourceType.OtherPlayer;
                    capability.bodyCulling = false;
                    capability.recordBodyAnimLevel = RecordBodyAnimLevel.FullBlendShape;
                    capability.autoStopIK = true; //set automatically stop ik when controller is far,idle,etc.
                    capability.ikMode = AvatarIKMode.FullBody;

                    var specIndex = _lastUserIndex % otherAvatarSpecTexts.Length;
                    var avatarSpecJsonText = otherAvatarSpecTexts[specIndex];

                    // allow load from avatar meta cache when network is weak.
                    capability.enablePlaceHolder = true;

                    var loadContext = new AvatarLoadContext("_AvatarId_" + _lastUserIndex++.ToString(), "", avatarSpecJsonText, capability);
                    loadContext.placeHolderAvatar = _otherAvatarPlaceHolderAvatar;
                    {
                        var avatar = PicoAvatarManager.instance.LoadAvatar(loadContext, (avatar, avatarEntity) => {
                            avatar.PlayAnimation("idle", 0, "BottomLayer");
                        });

                        {
                            float avatarDistX = UnityEngine.Random.Range(0.0f, avatarSpawnRange);
                            float avatarDistY = UnityEngine.Random.Range(0.0f, avatarSpawnRange * 0.5f);
                            float avatarDistZ = UnityEngine.Random.Range(0.0f, avatarSpawnRange);
                            //
                            avatar.transform.localPosition = avatarSpawnCenter + new Vector3(avatarDistX, avatarDistY, avatarDistZ);
                        }
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
