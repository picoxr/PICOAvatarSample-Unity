//#define PAV_MATERIAL_DATA_TEXTURE

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Pico
{
	namespace Avatar
	{
		/// <summary>
		/// AvatarRenderMesh extracts mesh data from native library and create Unity MeshRenderer.
		/// </summary>
		public class PicoAvatarRenderMesh : MonoBehaviour
		{
			#region Public Properties

			// Unity mesh renderer used to display mesh.
			public MeshRenderer meshRenderer
			{
				get => _staticMeshRenderer;
			}

			// Unity skin mesh renderer used to display mesh.
			public SkinnedMeshRenderer skinMeshRenderer
			{
				get => _skinMeshRenderer;
			}

			//
			public delegate void OnMaterialsUpdate(PicoAvatarRenderMesh renderMesh);

			public OnMaterialsUpdate onMaterialsUpdate = null;

			//
			public delegate void OnMeshUpdate(PicoAvatarRenderMesh renderMesh);

			public OnMeshUpdate onMeshUpdate = null;

			// whether the object has been destroyed.
			public bool isDestroyed
			{
				get => _destroyed;
			}

			// has tangent. MaterialConfiguration configurate the property.
			public bool hasTangent
			{
				get => _HasTangent;
			}

			public bool materialNeedTangent
			{
				get => _materialNeedTangent;
			}

			// cached Lod level from AvatarLod/AvatarPrimitive.
			public AvatarLodLevel lodLevel
			{
				get => _CachedLodLevel;
			}

			// render material.
			public PicoAvatarRenderMaterial[] renderMaterials { get; protected set; }

			// whether show as outline.
			public AvatarEffectKind avatarEffectKind { get; protected set; } = AvatarEffectKind.None;

			// whether data is ready.
			public bool isRenderDataReady
			{
				get => _isRenderDataReady;
			}

			// whether mesh data is ready. Means all gpu buffer are ready.
			public bool isRenderMeshDataReady
			{
				get => _isRenderMeshDataReady;
			}

			// whether all runtime material data is ready. means all textures are created.
			public bool isRenderMaterialDataReady
			{
				get => _isRenderMaterialDataReady;
			}

			// shared mesh buffer. holds mesh + morph + joints
			public AvatarMeshBuffer avatarMeshBuffer
			{
				get => _avatarMeshBuffer;
			}

			// 
			public float[] blendshapeWeights
			{
				get => _blendshapeWeights;
			}

			// whether need updated each frame.
			public bool needUpdateSimulation { get; private set; } = false;

			// native render mesh handler.
			internal System.IntPtr nativeRenderMeshHandler
			{
				get => _nativeRenderMeshHandler;
			}

			// avatar skeleton the mesh used.
			public AvatarSkeleton avatarSkeleton { get; private set; }

			#endregion


			#region Public Methods

			internal PicoAvatarRenderMesh()
			{
				//
				if (PicoAvatarStats.instance != null)
				{
					PicoAvatarStats.instance.IncreaseInstanceCount(PicoAvatarStats.InstanceType.AvatarRenderMesh);
				}
			}
			
            //Manually destroy
            internal virtual void Destroy()
			{
				// avoid duplicated decrease.
				if (!_destroyed && PicoAvatarStats.instance != null)
				{
					PicoAvatarStats.instance.DecreaseInstanceCount(PicoAvatarStats.InstanceType.AvatarRenderMesh);
				}

				_destroyed = true;
				//
				_isRenderDataReady = false;
				_isRenderMeshDataReady = false;

				//
				if (_staticMeshFilter != null)
				{
					_staticMeshFilter.sharedMesh = null;
					_staticMeshFilter = null;
				}

				if (_staticMeshRenderer)
				{
					_staticMeshRenderer.sharedMaterial = null;
					_staticMeshRenderer = null;
				}

				if (_skinMeshRenderer != null)
				{
					_skinMeshRenderer.sharedMesh = null;
					_skinMeshRenderer.sharedMaterial = null;
					_skinMeshRenderer = null;
				}

				// release render material.
				DestroyAvatarRenderMaterials();

				//
				ReferencedObject.ReleaseField(ref _avatarMeshBuffer);

				// dispose NativeArrary for _DynamicBuffer.
				if (_DynamicCpuData.IsCreated)
				{
					_DynamicCpuData.Dispose();
				}

				// not a reference counted object.
				NativeObject.ReleaseNative(ref _nativeRenderMeshHandler);

				// clear the field at last.
				_materialConfiguration = null;
			}

			// Unity framework invoke the method when scene object destroyed. 
			protected virtual void OnDestroy()
			{
				Destroy();
			}
			
            //Update Unity render data from native simulation data.
            //@remark Derived class SHOULD override the method to do actual update.
            internal virtual void UpdateSimulationRenderData()
			{
			}
            
            //Destroy AvatarRenderMaterials created from native AvatarRenderMaterial.
            private void DestroyAvatarRenderMaterials()
			{
				if (renderMaterials != null)
				{
					for (int i = 0; i < renderMaterials.Length; ++i)
					{
						renderMaterials[i]?.Release();
						renderMaterials[i] = null;
					}
				}

				renderMaterials = null;
			}
            
            //Sets material with native material and prescribed lod level.
            //Invoked by derived class when build render mesh.
            protected void BuildAndApplyRuntimeMaterials(PicoAvatarRenderMaterial[] mats)
			{
				if (meshRenderer == null && skinMeshRenderer == null)
				{
					return;
				}

				var runtimeMaterials = new Material[mats.Length];
				for (int i = 0; i < mats.Length; ++i)
				{
					//
					var runtimeMaterial = mats[i].GetRuntimeMaterial(this);
					//
					if (runtimeMaterial != null)
					{
						FillRuntimeMaterialWithAvatarRenderMaterial(mats[i], runtimeMaterial);
						runtimeMaterials[i] = runtimeMaterial;
					}
					else
					{
						AvatarEnv.Log(DebugLogMask.GeneralError, "Failed to get runtime material.");
					}
				}

				if (skinMeshRenderer)
				{
					skinMeshRenderer.sharedMaterials = runtimeMaterials;
				}
				else
				{
					meshRenderer.sharedMaterials = runtimeMaterials;
					// add a empty MaterialPropertyBlock to make renderer incompatible with the SRP Batcher
					meshRenderer.SetPropertyBlock(new MaterialPropertyBlock());
				}

				//
				_isRenderDataReady = true;

				onMaterialsUpdate?.Invoke(this);
			}
            
            //Fill properties and textures of runtime unity material with AvatarRenderMaterial.
            protected void FillRuntimeMaterialWithAvatarRenderMaterial(PicoAvatarRenderMaterial mat, Material material)
			{
				mat.UpdateMaterial(material);

				////
				//material.SetBuffer("_staticBuffer", _staticMeshBuffer.runtimeBuffer);
				//material.SetBuffer("_dynamicBuffer", _DynamicBuffer);
				//material.SetBuffer("_outputBuffer", _OutPositionBuffer);
				//material.SetInt("_staticBufferOffset", (int) _staticMeshBuffer.morphAndSkinDataInfo.staticBufferOffset);
				//material.SetInt("_dynamicBufferOffset", (int) _staticMeshBuffer.morphAndSkinDataInfo.dynamicBufferOffset);
				//material.SetInt("_vertexIndexOffset", 0);

				//if (_MaterialDataTexture)
				//{
				//    material.SetTexture("_materialDataTexture", _MaterialDataTexture);
				//    material.SetVector("_materialDataTextureSize", new Vector4(_MaterialDataTexture.width, _MaterialDataTexture.height, 0, 0));
				//}
				//else if (_MaterialDataBuffer != null)
				//{
				//    material.SetBuffer("_materialDataBuffer", _MaterialDataBuffer);
				//}

				// whether disable shadow casting.
				if (PicoAvatarApp.instance.renderSettings.forceDisableReceiveShadow)
				{
					material.DisableKeyword("_MAIN_LIGHT_SHADOWS");
					material.DisableKeyword("_MAIN_LIGHT_SHADOWS_CASCADE");
					material.DisableKeyword("SHADOWS_SHADOWMASK");
					//
					material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
					material.SetFloat("_ReceiveShadows", 0.0f);
				}

				//
				if (this.avatarEffectKind != AvatarEffectKind.None)
				{
					if (avatarEffectKind == AvatarEffectKind.SimpleOutline)
					{
						material.EnableKeyword("PAV_AVATAR_LOD_OUTLINE");
						//
						{
							material.SetFloat("_Surface", (float)PicoAvatarRenderMaterial.SurfaceType.Opaque);
							material.SetFloat("_ColorMask", (float)0.0);
						}
					}
				}

				// whether need tangent.
				this._materialNeedTangent = mat.has_BumpMap;
				//
				if (!material.HasProperty(PicoAvatarApp.instance.renderSettings.materialConfiguration.unityID_BumpMap))
				{
					this._materialNeedTangent = false;
				}

				if (this.lodLevel >= AvatarLodLevel.Lod2)
				{
					material.SetFloat("_BaseColorAmplify", 0.8f);
				}
#if DEBUG
				if (false)
				{
					var keywords = material.shaderKeywords;
					var sb = new System.Text.StringBuilder();
					sb.Append("material ");
					sb.Append(material.shader.name);
					sb.Append(" keywords:");
					foreach (var x in keywords)
					{
						sb.Append(x);
						sb.Append("|");
					}

					AvatarEnv.Log(DebugLogMask.GeneralInfo, sb.ToString());
				}
#endif
			}

			//Fill a runtime unity material with AvatarRenderMaterial. Usually used to update material runtimly and manually by avatar sdk developer.
			public void FillRuntimeMaterial(Material mat, int materialIndex = 0)
			{
				if (renderMaterials != null && (uint)materialIndex <= (uint)renderMaterials.Length)
				{
					FillRuntimeMaterialWithAvatarRenderMaterial(renderMaterials[materialIndex], mat);
				}
			}

			// When shader changed outside, should update material properties.
			public void OnShaderChanged()
			{
				if ((meshRenderer == null && skinMeshRenderer == null) || renderMaterials == null || !isRenderDataReady)
				{
					return;
				}

				var sharedMaterials = skinMeshRenderer != null
					? skinMeshRenderer.sharedMaterials
					: meshRenderer.sharedMaterials;
				if (sharedMaterials != null && sharedMaterials.Length == renderMaterials.Length)
				{
					for (int i = 0; i < sharedMaterials.Length; ++i)
					{
						FillRuntimeMaterialWithAvatarRenderMaterial(renderMaterials[i], sharedMaterials[i]);
					}
				}

				if (skinMeshRenderer != null)
				{
					skinMeshRenderer.sharedMaterials = sharedMaterials;
				}
				else
				{
					meshRenderer.sharedMaterials = sharedMaterials;
				}
			}

			internal void SetAvatarEffectKind(AvatarEffectKind avatarEffectKind_)
			{
				if (this.avatarEffectKind != avatarEffectKind_ && (skinMeshRenderer != null || meshRenderer != null))
				{
					this.avatarEffectKind = avatarEffectKind_;

					var sharedMaterials = skinMeshRenderer != null
						? skinMeshRenderer.sharedMaterials
						: meshRenderer.sharedMaterials;
					if (sharedMaterials != null && sharedMaterials.Length == renderMaterials.Length)
					{
						for (int i = 0; i < sharedMaterials.Length; ++i)
						{
							FillRuntimeMaterialWithAvatarRenderMaterial(renderMaterials[i], sharedMaterials[i]);
						}
					}

					if (skinMeshRenderer != null)
					{
						skinMeshRenderer.sharedMaterials = sharedMaterials;
					}
					else
					{
						meshRenderer.sharedMaterials = sharedMaterials;
					}
				}
			}

			internal void Notify_AvatarSceneLightEnvChanged(PicoAvatarSceneLightEnv lightEnv)
			{
				if (renderMaterials != null)
				{
					for (int i = 0; i < renderMaterials.Length; ++i)
					{
						renderMaterials[i]?.OnAvatarSceneLightEnvChanged(lightEnv);
					}
				}
			}
			
            //Build material from native AvatarRenderMaterial and apply to the renderer.
            //@param renderMaterialHandles list of native AvatarRenderMaterial. Reference count has been added from invoker.
            internal bool BuildMaterialsFromNative(System.IntPtr[] renderMaterialHandles,
				AvatarLodLevel lodLevel, bool merged)
			{
				// create material with native material data. pass lifetime management of renderMaterialHandles to AvatarRenderMaterials.
				var newRenderMaterials = CreateRenderMaterials(renderMaterialHandles, lodLevel, merged);
				if (newRenderMaterials == null)
				{
					Destroy();
					return false;
				}

				{
					//UnityEngine.Profiling.Profiler.BeginSample("PicoPrimitiveRenderMesh.LoadTextures");
					for (int i = 0; i < newRenderMaterials.Length; ++i)
					{
						var renderMaterial = newRenderMaterials[i];
						if (renderMaterial != null)
						{
							if (!renderMaterial.LoadTexturesFromNativeMaterial(lodLevel))
							{
								renderMaterials = newRenderMaterials;
								Destroy();
								return false;
							}
						}
					}
					//UnityEngine.Profiling.Profiler.EndSample();
				}

				// create unity material and apply to unity mesh.
				BuildAndApplyRuntimeMaterials(newRenderMaterials);
				//
				DestroyAvatarRenderMaterials();

				// keep track current avatar render materials.
				renderMaterials = newRenderMaterials;

				return true;
			}

			#endregion


			#region Protected Fields

			internal AvatarLod _AvatarLod;

			#endregion


			#region Private Fields

			// whether has been destroyed.
			private bool _destroyed = false;

			// has tangent. MaterialConfiguration configurate the property.
			private bool _HasTangent = true;

			// cached lod level from AvatarLod/AvatarPrimitive
			public AvatarLodLevel _CachedLodLevel = 0;

			// The handle need be reference counted.
			private System.IntPtr _nativeRenderMeshHandler;

			// sets by derived class object.
			private bool _isRenderDataReady = false;
			private bool _isRenderMeshDataReady = false;
			private bool _isRenderMaterialDataReady = false;

			// shared mesh buffer. holds mesh + morph + joints
			private AvatarMeshBuffer _avatarMeshBuffer;

			// global shared material configuration.
			private PicoMaterialConfiguration _materialConfiguration;

			// cpu data.
			private NativeArray<int> _DynamicCpuData;

			//
			private MorphAndSkinSimulationGpuData _DynamicGpuDataInfo;

			// Unity static mesh filter.
			private MeshFilter _staticMeshFilter;

			// Unity mesh renderer to hold material.
			private MeshRenderer _staticMeshRenderer;

			// Unity Skinned MeshRenderer
			private SkinnedMeshRenderer _skinMeshRenderer;

			// whether material need tangent.
			private bool _materialNeedTangent = false;

			// last active morph channels, used to turn of active morph channels.
			private int[] _lastMorphChannelActiveFrames = null;

			// last frame that update morph and skin simulation
			private int _lastUpdateMorphAndSkinSimulationBufferFrame = 0;

			// whether cache the morphChannelWeights
			private Boolean _cacheMorphChannelWeights = false;

			// cached morphChannelWeights
			private float[] _blendshapeWeights;

			#endregion


			#region Protected Methods
			
            //Build mesh object.
            //@param  renderMeshHandle native handle to AvatarRenderMesh. Reference count has been added.
            //@param allowGpuDataCompressd whether allow compress gpu data.
            protected bool CreateUnityRenderMesh(System.IntPtr renderMeshHandle, AvatarSkeleton avatarSkeleton_,
				AvatarLodLevel lodLevel_,
				System.IntPtr owner, bool allowGpuDataCompressd,
				AvatarShaderType mainShaderType = AvatarShaderType.Invalid,
				bool cacheMorphChannelWeights = false,
				bool depressSkin = false)
			{
				//if (AvatarEnv.NeedLog(DebugLogMask.AvatarLoad))
				//{
				//    AvatarEnv.Log(DebugLogMask.AvatarLoad, "AvatarRenderMesh.CreateUnityRenderMesh.Start");
				//}

				avatarSkeleton = avatarSkeleton_;

				// check
				if (_nativeRenderMeshHandler != System.IntPtr.Zero || _staticMeshFilter != null ||
				    _skinMeshRenderer != null)
				{
					NativeObject.ReleaseNative(ref renderMeshHandle);
					//
					AvatarEnv.Log(DebugLogMask.GeneralError, "PicoAvatarRenderMesh has been created!");
					return false;
				}

				// keep native render mesh.
				_nativeRenderMeshHandler = renderMeshHandle;

				_materialConfiguration = PicoAvatarRenderMaterial.materialConfiguration;
				//
				{
					// _staticMeshBuffer.Retain() has been invoked.
					_avatarMeshBuffer = AvatarMeshBuffer.CreateAndRefMeshBuffer(renderMeshHandle,
						_materialConfiguration.needTangent,
						allowGpuDataCompressd, depressSkin);
					if (_avatarMeshBuffer == null)
					{
						return false;
					}
				}
				//
				_CachedLodLevel = lodLevel_;

				//
				_cacheMorphChannelWeights = cacheMorphChannelWeights;

				// whether has tangent data.
				_HasTangent = _avatarMeshBuffer.hasTangent;

				// add mesh filter.
				{
					if (avatarSkeleton != null && !Utility.IsNullOrEmpty(_avatarMeshBuffer.boneNameHashes))
					{
						var boneNameHahes = _avatarMeshBuffer.boneNameHashes;
						var bones = new Transform[boneNameHahes.Length];
						//
						for (int i = 0; i < boneNameHahes.Length; ++i)
						{
							var trans = avatarSkeleton.GetTransform(boneNameHahes[i]);
							if (trans == null)
							{
								AvatarEnv.Log(DebugLogMask.GeneralError, "transform for a bone not found!");
							}

							bones[i] = trans;
						}

						var rootBoneTrans = avatarSkeleton.GetTransform(_avatarMeshBuffer.rootBoneNameHash);

						_skinMeshRenderer = this.gameObject.AddComponent<SkinnedMeshRenderer>();
						_skinMeshRenderer.sharedMesh = _avatarMeshBuffer.mesh;
						_skinMeshRenderer.bones = bones;
						_skinMeshRenderer.rootBone =
							rootBoneTrans == null ? avatarSkeleton.rootTransform : rootBoneTrans;
						_skinMeshRenderer.localBounds = _avatarMeshBuffer.mesh.bounds;

						// whether disable shadow casting.
						if (PicoAvatarApp.instance.renderSettings.forceDisableCastShadow ||
						    mainShaderType == AvatarShaderType.Eyelash_Base)
						{
							_skinMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
						}

						// whether disable shadow casting.
						if (PicoAvatarApp.instance.renderSettings.forceDisableReceiveShadow ||
						    lodLevel_ > AvatarLodLevel.Lod2)
						{
							_skinMeshRenderer.receiveShadows = false;
						}
					}
					else
					{
						_staticMeshFilter = this.gameObject.AddComponent<MeshFilter>();
						_staticMeshFilter.sharedMesh = _avatarMeshBuffer.mesh;

						// add mesh renderer.
						_staticMeshRenderer = this.gameObject.AddComponent<MeshRenderer>();

						// whether disable shadow casting.
						if (PicoAvatarApp.instance.renderSettings.forceDisableCastShadow ||
						    mainShaderType == AvatarShaderType.Eyelash_Base)
						{
							_staticMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
						}

						// whether disable shadow casting.
						if (PicoAvatarApp.instance.renderSettings.forceDisableReceiveShadow ||
						    lodLevel_ > AvatarLodLevel.Lod2)
						{
							_staticMeshRenderer.receiveShadows = false;
						}
					}
				}
				//
				{
					InitializeSimulationGpuData(owner);
				}
				//
				_isRenderMeshDataReady = true;

				//if (AvatarEnv.NeedLog(DebugLogMask.AvatarLoad))
				//{
				//    AvatarEnv.Log(DebugLogMask.AvatarLoad, "AvatarRenderMesh.CreateUnityRenderMesh.end");
				//}
				return true;
			}
            
            //Initialize gpu data.
            protected void InitializeSimulationGpuData(System.IntPtr owner)
			{
				//
				needUpdateSimulation = false;

				// Dynamic Buffer
				if (_avatarMeshBuffer == null)
				{
					return;
				}

				// currently on morph data needed for each frame updation.
				if (_avatarMeshBuffer.meshInfo.blendShapeCount == 0)
				{
					return;
				}

				var dynamicBufferByteSize = _avatarMeshBuffer.morphAndSkinDataInfo.dynamicBufferByteSize;

				_DynamicCpuData = new NativeArray<int>((int)dynamicBufferByteSize / 4, Allocator.Persistent,
					NativeArrayOptions.UninitializedMemory);

				_DynamicGpuDataInfo = new MorphAndSkinSimulationGpuData();
				// currently only morph weights need updated.
				_DynamicGpuDataInfo.flags = (uint)RenderResourceDataFlags.Dynamic_HasMorphWeights;
				_DynamicGpuDataInfo.version = 0;
				_DynamicGpuDataInfo.dynamicBufferByteSize = dynamicBufferByteSize;
				unsafe
				{
					_DynamicGpuDataInfo.dataBuffer = (System.IntPtr)_DynamicCpuData.GetUnsafePtr();
				}

				_DynamicGpuDataInfo.materialData = System.IntPtr.Zero;

				// update at startup.
				UpdateMorphAndSkinSimulationBuffer(owner);

				// need update simulation.
				needUpdateSimulation = true;
			}
            
            //Invoked to update simulation buffer.
            //@return 
            protected bool UpdateMorphAndSkinSimulationBuffer(System.IntPtr meshOwner,
				RecordBodyAnimLevel level = RecordBodyAnimLevel.Count, bool expressionPlaybackEnabled = false)
			{
				if (!Utility.EnableRenderObject || !Utility.EnableSDKUpdate || _avatarMeshBuffer == null ||
				    _nativeRenderMeshHandler == System.IntPtr.Zero)
				{
					return false;
				}

				var morphChannelCount = _avatarMeshBuffer.meshInfo.blendShapeCount;

				// if has no morph data and only need update morph data, do nothing.
				if (!expressionPlaybackEnabled)
				{
					if (_DynamicGpuDataInfo.flags == (uint)RenderResourceDataFlags.Dynamic_HasMorphWeights)
					{
						// if has no bs or currently skip bs, do nothing.
						if (morphChannelCount == 0 || (level < RecordBodyAnimLevel.BasicBlendShape &&
						                               level != RecordBodyAnimLevel.DeviceInput))
						{
							//TODO: fadeout to empty bs weights
							return false;
						}
					}
				}

				var curFrameCount = Time.frameCount;
				if (_lastUpdateMorphAndSkinSimulationBufferFrame == curFrameCount)
				{
					return false;
				}

				_lastUpdateMorphAndSkinSimulationBufferFrame = curFrameCount;

				// check last active morph channels.
				if (_lastMorphChannelActiveFrames == null || _lastMorphChannelActiveFrames.Length != morphChannelCount)
				{
					_lastMorphChannelActiveFrames = new int[morphChannelCount];
				}

				if (_blendshapeWeights == null || _blendshapeWeights.Length != morphChannelCount)
				{
					_blendshapeWeights = new float[morphChannelCount];
				}

				UnityEngine.Profiling.Profiler.BeginSample("Update GPU Skin DynamicBuffer");

				// set current owner.
				_DynamicGpuDataInfo.owner = meshOwner;
				//
				var curFrameIndex = Time.frameCount;
				//
				if (pav_AvatarRenderMesh_FillMorphAndSkinSimulationGpuData(_nativeRenderMeshHandler,
					    ref _DynamicGpuDataInfo) == NativeResult.Success)
				{
					if (_skinMeshRenderer != null && _skinMeshRenderer.sharedMesh != null)
					{
						// update blendshapes
						var dynamicBuffer = _DynamicGpuDataInfo.dataBuffer;
						int offset = (int)_avatarMeshBuffer.morphAndSkinDataInfo.dynamicBufferOffset;
						int morphCount = Marshal.ReadInt32(dynamicBuffer, offset + 0);
						int morphIndexDataOffset = Marshal.ReadInt32(dynamicBuffer, offset + 4);
						int morphWeightDataOffset = Marshal.ReadInt32(dynamicBuffer, offset + 8);
						int blendShapeCount = _skinMeshRenderer.sharedMesh.blendShapeCount;
						unsafe
						{
							for (int i = 0; i < morphCount; ++i)
							{
								const int stride = 4;
								int index = Marshal.ReadInt32(dynamicBuffer, morphIndexDataOffset + stride * i);
								int weighti = Marshal.ReadInt32(dynamicBuffer, morphWeightDataOffset + stride * i);
								float weight = *(float*)&weighti;
								_lastMorphChannelActiveFrames[index] = curFrameIndex;
								// set current value.
								_skinMeshRenderer.SetBlendShapeWeight(index, weight * 100);
								// cache blendshape weights
								if (_cacheMorphChannelWeights)
								{
									_blendshapeWeights[index] = weight * 100;
								}
							}
						}

						// turn off not used channels.
						for (int i = 0; i < morphChannelCount; ++i)
						{
							if (_lastMorphChannelActiveFrames[i] != 0 &&
							    _lastMorphChannelActiveFrames[i] != curFrameIndex)
							{
								// clear active flag
								_lastMorphChannelActiveFrames[i] = 0;
								// clear deactived channel.
								_skinMeshRenderer.SetBlendShapeWeight(i, 0);
								// cache blendshape weights
								if (_cacheMorphChannelWeights)
								{
									_blendshapeWeights[i] = 0;
								}
							}
						}
					}
				}

				UnityEngine.Profiling.Profiler.EndSample();

				return true;
			}
            
            //Sets render material. 
            //@param renderMaterialHandles list of handle to native AvatarRenderMaterial. Reference count has been added from invoker.
            protected PicoAvatarRenderMaterial[] CreateRenderMaterials(System.IntPtr[] renderMaterialHandles,
				AvatarLodLevel lodLevel, bool merged)
			{
				PicoAvatarRenderMaterial[] materials = new PicoAvatarRenderMaterial[renderMaterialHandles.Length];
				bool success = true;
				for (int i = 0; i < materials.Length; ++i)
				{
					materials[i] = new PicoAvatarRenderMaterial(merged, _AvatarLod);
					materials[i].Retain();

					// try to load render material.
					if (!materials[i].LoadPropertiesFromNativeMaterial(renderMaterialHandles[i], lodLevel))
					{
						success = false;
					}
				}

				if (!success)
				{
					for (int i = 0; i < materials.Length; ++i)
					{
						materials[i]?.Release();
						materials[i] = null;
					}

					materials = null;
				}

				return materials;
			}

			#endregion


			#region Update Primitive Dirty Data
			
            //Rebuild gpu skin and morph data in work thread. invoke before UpdateMorphAndSkinResourceGpuData
            internal NativeResult RebuildGpuSkinAndMorphDataT()
			{
				return pav_AvatarRenderMesh_RebuildMorphAndSkinGpuDataT(_nativeRenderMeshHandler);
			}
            
            //update gpu skin and morph buffer.
            internal void UpdateMorphAndSkinResourceGpuData()
			{
				if (_avatarMeshBuffer != null)
				{
					if (_avatarMeshBuffer.UpdateMorphAndSkinResourceGpuData(_nativeRenderMeshHandler))
					{
						//// notify that mesh changed.
						//if(onMeshUpdate != null)
						//{
						//    onMeshUpdate(this);
						//}
					}
				}
			}
            
            //Update dirty mesh pnt data.
            internal void UpdateDirtyMeshPNTData()
			{
				if (_avatarMeshBuffer != null)
				{
					var needTangent = _materialNeedTangent && _HasTangent;
					_avatarMeshBuffer.UpdateMeshPNTData(_nativeRenderMeshHandler, _materialConfiguration, needTangent);
				}
			}
            
            //Update dirty material uniforms.
            internal void UpdateDirtyMaterialUniforms()
			{
				if (renderMaterials != null && (meshRenderer != null || skinMeshRenderer != null))
				{
					var unityMaterials = skinMeshRenderer != null
						? skinMeshRenderer.sharedMaterials
						: meshRenderer.sharedMaterials;
					var count = renderMaterials.Length;
					if (count == unityMaterials.Length)
					{
						for (int i = 0; i < count; ++i)
						{
							renderMaterials[i].UpdateDirtyMaterialUniforms();
							_materialConfiguration.UpdateToUniformsMaterial(renderMaterials[i], unityMaterials[i]);
						}
					}
				}
			}

			#endregion


			#region Native Methods

			const string PavDLLName = DllLoaderHelper.PavDLLName;

			[DllImport(PavDLLName, CallingConvention = CallingConvention.Cdecl)]
			private static extern NativeResult pav_AvatarRenderMesh_GetMorphAndSkinGpuDataInfo(
				System.IntPtr nativeHandle, uint requiredVersion, uint requiredFlags,
				ref MorphAndSkinDataInfo gpuDataInfo);

			[DllImport(PavDLLName, CallingConvention = CallingConvention.Cdecl)]
			private static extern NativeResult pav_AvatarRenderMesh_RebuildMorphAndSkinGpuDataT(
				System.IntPtr nativeHandle);

			[DllImport(PavDLLName, CallingConvention = CallingConvention.Cdecl)]
			private static extern NativeResult pav_AvatarRenderMesh_FillMorphAndSkinSimulationGpuData(
				System.IntPtr nativeHandle, ref MorphAndSkinSimulationGpuData gpuData);

			#endregion
		}


		// Render Mesh of an AvatarPrimitive instance.
		public class PicoPrimitiveRenderMesh : PicoAvatarRenderMesh
		{
			// owner primitive object
			public AvatarPrimitive primitive
			{
				get { return _Primitive; }
			}

			[Obsolete("Deprecated, use primitive instead!")]
			public AvatarPrimitive Primitive
			{
				get { return _Primitive; }
			}

			#region Framework Methods
			
            //Sets primitive and build mesh.
            internal void AttachPrimitive(AvatarPrimitive primitive)
			{
				_Primitive = primitive;
				_AvatarLod = _Primitive?.owner;
				//
				avatarEffectKind = primitive.owner.owner.owner.avatarEffectKind;
			}

			// enabled to make sure correct first frame rendering
			private void OnWillRenderObject()
			{
				if (isRenderDataReady && _Primitive != null && _Primitive.nativeHandle != System.IntPtr.Zero &&
				    needUpdateSimulation
				    && !_Primitive.owner.owner.isNativeUpdateSkippedThisFrame)
				{
					UpdateMorphAndSkinSimulationBuffer(_Primitive.nativeHandle,
						_Primitive.owner.owner.lastAnimationPlaybackLevel,
						_Primitive.owner.owner.expressionPlaybackEnabled);
				}
			}
			
            //Invoked when the scene object destroyed by Unity.
            internal override void Destroy()
			{
				if (_Primitive != null)
				{
					_Primitive.OnRenderMeshDestroy(this);
					_Primitive = null;
				}

				//
				base.Destroy();
			}

			#endregion


			#region Build Mesh/Material
			
            //Build renderer with native AvatarRenderMesh and AvatarRenderMaterial.
            //@param renderMeshHandle handle to native AvatarRenderMesh. Reference count has been added from invoker.
            //@param renderMaterialHandles handle to native AvatarRenderMaterial. Reference count has been added from invoker.
            internal bool BuildFromNativeRenderMeshAndMaterial(System.IntPtr renderMeshHandle,
				System.IntPtr[] renderMaterialHandles,
				AvatarSkeleton avatarSkeleton_, bool allowGpuDataCompressd, bool cacheMorphChannelWeights = false,
				bool depressSkin = false)
			{
				if (_Primitive == null || meshRenderer != null || skinMeshRenderer != null)
				{
					throw new System.Exception("BuildFromNativeRenderMesh invoked wrongly.");
				}

				// if native render mesh or render material not created, can not show unity mesh.
				if (renderMeshHandle == System.IntPtr.Zero || renderMaterialHandles == null)
				{
					return false;
				}

				//// log
				//if (AvatarEnv.NeedLog(DebugLogMask.GeneralInfo))
				//{
				//    AvatarEnv.Log(DebugLogMask.GeneralInfo, "PrimitiveRenderMesh.BuildFromNativeRenderMesh.Start");
				//}

				// build mesh with native mesh data for a lod level.
				{
					if (!CreateUnityRenderMesh(renderMeshHandle, avatarSkeleton_, _Primitive.lodLevel,
						    _Primitive.nativeHandle,
						    allowGpuDataCompressd, _Primitive.mainShaderType, cacheMorphChannelWeights, depressSkin))
					{
						return false;
					}
				}

				// create material with native material data
				{
					//UnityEngine.Profiling.Profiler.BeginSample("PicoPrimitiveRenderMesh.LoadTextures");
					if (!BuildMaterialsFromNative(renderMaterialHandles, _Primitive.lodLevel, false))
					{
						return false;
					}
					//UnityEngine.Profiling.Profiler.EndSample();
				}

				//
				if (needUpdateSimulation && _Primitive.owner != null)
				{
					_Primitive.owner.AddSimulationNeededAvatarPrimitive(_Primitive);
				}

				//// log
				//if (AvatarEnv.NeedLog(DebugLogMask.GeneralInfo))
				//{
				//    AvatarEnv.Log(DebugLogMask.GeneralInfo, "PrimitiveRenderMesh.BuildFromNativeRenderMesh.After");
				//}
				return true;
			}

			#endregion


			#region Update Simulation Data
			
            //Update morph and skin each frame.
            internal override void UpdateSimulationRenderData()
			{
				if (_Primitive != null && _Primitive.nativeHandle != System.IntPtr.Zero && needUpdateSimulation)
				{
					UpdateMorphAndSkinSimulationBuffer(_Primitive.nativeHandle,
						_Primitive.owner.owner.lastAnimationPlaybackLevel,
						_Primitive.owner.owner.expressionPlaybackEnabled);
				}
			}

			#endregion


			#region Private Fields

			// owner primitive.
			private AvatarPrimitive _Primitive;

			#endregion
		}
	}
}