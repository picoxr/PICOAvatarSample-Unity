using Pico.Avatar.Sample;
using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;

namespace Pico
{
    namespace Avatar
    {
        public class WeakNetworkModeSample : PicoAppLaunchBase
        {
            #region Public Fields

            public UnityEngine.UI.Text btnText;
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
                if(btnText != null)
                {
                    btnText.text = "Wait AddAvatar!";
                }
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

                if (btnText != null)
                {
                    btnText.text = "AddAvatar Now!";
                }
            }

            private void OnDestroy()
            {
            }
            #endregion


            #region Private Fields

            private bool m_loginPlatformSDK = false;

            private ActionAvatar _mainActionAvatar = null;

            #endregion


            #region Private Methods

            private IEnumerator Coroutine_DoInitialize()
            {
                while (!PicoAvatarManager.canLoadAvatar)
                {
                    yield return null;
                }
            }

            /**
             * @brief load avatar. Invoked from UI.
             */
            [Preserve()]
            public void LoadAvatar()
            {
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
                else
                {
                    // dispose first.
                    _mainActionAvatar.Dispose();
                    _mainActionAvatar.SetAsMainAvatar();
                    _mainActionAvatar.StartAvatar(_mainActionAvatar.userId);
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
