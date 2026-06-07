/*
 * SimpleAvatarUploader
 * A simple script to upload avatars with VRCSDK Public API
 * https://gist.github.com/anatawa12/029f749b527ed0a8d6dc853a5bcf9b94
 *
 * This tool is made to provide a simple way to upload avatars with the same way as Continuous Avatar Uploader does, to test compatibility with other avatar-related tools.
 *
 * Continouus avatar uploader currently uses `IVRCSdkAvatarBuilderApi.Build` API to build,
 * and use `VRCApi.UpdateAvatarBundle` to upload the avatar bundle just built.
 * 
 * Click `Tools/anatawa12 gists/SimpleAvatarUploader` to open this window.
 * Set avatar descriptor to upload, and click `Upload` button to upload it.
 *
 * MIT License
 * 
 * Copyright (c) 2023 anatawa12
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

#if UNITY_EDITOR && (!ANATAWA12_GISTS_VPM_PACKAGE || GIST_029f749b527ed0a8d6dc853a5bcf9b94)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.Api;
using VRC.SDKBase.Editor.Validation;
using Object = UnityEngine.Object;

namespace anatawa12.gists
{
    internal class SimpleAvatarUploader : EditorWindow
    {
        private const string GIST_NAME = "Simple Avatar Uploader";

        [MenuItem("Tools/anatawa12 gists/" + GIST_NAME)]
        static void Create() => GetWindow<SimpleAvatarUploader>(GIST_NAME);

        VRCAvatarDescriptor avatarDescriptor;

        private void OnGUI()
        {
            GUILayout.Label("Simple Avatar Uploader", EditorStyles.boldLabel);
            avatarDescriptor = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("Avatar Descriptor", avatarDescriptor, typeof(VRCAvatarDescriptor), true);

            if (avatarDescriptor == null)
            {
                EditorGUILayout.HelpBox("Set avatar descriptor to upload", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Upload"))
            {
                UploadAvatar(avatarDescriptor);
            }
        }

        private static async void UploadAvatar(VRCAvatarDescriptor avatarDescriptor, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder))
                {
                    EditorUtility.DisplayDialog("Simple Avatar Uploader", "VRC SDK Avatar Builder is not available.", "OK");
                    return;
                }

                var (vrcAvatar, uploadingNewAvatar) = await PrepareVRCAvatar(avatarDescriptor.gameObject, cancellationToken);
                var pipelineManager = avatarDescriptor.GetComponent<PipelineManager>();

                var bundlePath = await builder.Build(avatarDescriptor.gameObject);

                ValidateBuiltBundleSize(bundlePath);
                
                // get uploaded avatar info
                vrcAvatar = await VRCApi.GetAvatar(pipelineManager.blueprintId, forceRefresh: true,
                    cancellationToken: cancellationToken);

                vrcAvatar = await UpdateAvatarFallbackTagIfNeeded(pipelineManager, vrcAvatar, cancellationToken: cancellationToken);

                // This window does not support uploading thumbnail.
                // This make invalid avatar for new avatar upload, but this is just a tool to test upload process,
                // so we skip thumbnail upload here.
                
                await UploadWithRetry(async () =>
                {
                    vrcAvatar = await VRCApi.UpdateAvatarBundle(vrcAvatar.ID, vrcAvatar, bundlePath,
                        cancellationToken: cancellationToken);
                });
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Simple Avatar Uploader", "Failed to build avatar bundle. See log for more details: " + e.Message, "OK");
                Debug.LogException(e);
                return;
            }
            
            async Task UploadWithRetry(Func<Task> uploadAction)
            {
                const int uploadRetryCount = 0;
                var remainingRetries = uploadRetryCount;
                for (;;)
                {
                    try
                    {
                        await uploadAction();
                        return;
                    }
                    catch (UploadException e)
                    {
                        if (e.Message == "This file was already uploaded")
                        {
                            Debug.Log("Uploading skipped: already uploaded");
                        }

                        if (remainingRetries == 0) throw;
                        remainingRetries--;

                        var currentTry = uploadRetryCount - remainingRetries;
                        Debug.LogWarning(
                            $"Uploading failed with exception, retrying... ({currentTry}/{uploadRetryCount}): {e}");
                    }
                }
            }
        }
        
        private static async Task<(VRCAvatar, bool isNewAvatar)> PrepareVRCAvatar(GameObject gameObject,
            //Action<string> saveBlueprintId = null,
            CancellationToken cancellationToken = default)
        {
            var pipelineManager = gameObject.GetComponent<PipelineManager>();
            if (!pipelineManager)
                pipelineManager = gameObject.AddComponent<PipelineManager>();

            VRCAvatar vrcAvatar;
            bool isNewAvatar;

            if (string.IsNullOrEmpty(pipelineManager.blueprintId))
            {
                // The blueprint id is not assigned yet. Reserving new blueprint id or simply assigning one.
#if CAU_VRCSDK_BASE_3_9_0
                vrcAvatar = await VRCApi.CreateAvatarRecord(CreateNewAvatarInfo(), cancellationToken: cancellationToken);
                if (string.IsNullOrEmpty(vrcAvatar.ID)) throw new Exception("Failed to reserve an avatar ID");
                pipelineManager.blueprintId = vrcAvatar.ID;
#else
                // pipelineManager.AssignId() doesn't mark pipeline manager dirty
                pipelineManager.AssignId();
                vrcAvatar = CreateNewAvatarInfo();
#endif
                //saveBlueprintId?.Invoke(pipelineManager.blueprintId);
                isNewAvatar = true;
            }
            else
            {
                // when blueprint id is already assigned, try to get avatar info
                try
                {
                    vrcAvatar = await VRCApi.GetAvatar(pipelineManager.blueprintId, true, cancellationToken);
                }
                catch (ApiErrorException ex)
                {
                    if (ex.StatusCode != HttpStatusCode.NotFound)
                        throw new Exception("Unknown error", ex);
                    vrcAvatar = default;
                }


                if (string.IsNullOrEmpty(vrcAvatar.ID))
                {
                    isNewAvatar = true;
                    vrcAvatar = CreateNewAvatarInfo();
                    Debug.LogWarning("We found avatar with blueprint id set but not uploaded. This is not supported by VRCSDK since 3.9.0.");
                }
                else
                {
                    if (APIUser.CurrentUser == null || vrcAvatar.AuthorId != APIUser.CurrentUser?.id)
                        throw new Exception("Uploading other user avatar.");
#if CAU_VRCSDK_BASE_3_9_0
                    isNewAvatar = vrcAvatar.PendingUpload;
#else
                    isNewAvatar = false;
#endif
                }
            }

            return (vrcAvatar, isNewAvatar);

            VRCAvatar CreateNewAvatarInfo() => new()
            {
                Name = pipelineManager.gameObject.name,
                Description = "",
                Tags = new List<string>(),
                ReleaseStatus = "private",
            };
        }

        private static void ValidateBuiltBundleSize(string bundlePath)
        {
            var isMobbile = ValidationEditorHelpers.IsMobilePlatform();

            if (ValidationEditorHelpers.CheckIfAssetBundleFileTooLarge(ContentType.Avatar, bundlePath, out var fileSize, isMobbile))
            {
                var limit = ValidationHelpers.GetAssetBundleSizeLimit(ContentType.Avatar, isMobbile);
                throw new ValidationException($"Avatar download size is too large for the target platform. " +
                                              $"{ValidationHelpers.FormatFileSize(fileSize)}({fileSize} bytes) " +
                                              $"> {ValidationHelpers.FormatFileSize(limit)}({limit} bytes)");
            }

            if (ValidationEditorHelpers.CheckIfUncompressedAssetBundleFileTooLarge(ContentType.Avatar, out var uncompressedSize, isMobbile))
            {
                var limit = ValidationHelpers.GetAssetBundleSizeLimit(ContentType.Avatar, isMobbile, false);
                throw new ValidationException($"Avatar uncompressed size is too large for the target platform. " +
                                              $"{ValidationHelpers.FormatFileSize(uncompressedSize)} ({uncompressedSize} bytes) " +
                                              $"> {ValidationHelpers.FormatFileSize(limit)}({limit} bytes)");
            }
        }

        private const string AvatarFallbackTag = "author_quest_fallback";
        private static async Task<VRCAvatar> UpdateAvatarFallbackTagIfNeeded(
            PipelineManager pipelineManager,
            VRCAvatar vrcAvatar,
            CancellationToken cancellationToken = default)
        {
            // Update fallback tag
            if (vrcAvatar.Tags?.Contains(AvatarFallbackTag) ?? false)
            {
                if (pipelineManager.fallbackStatus is PipelineManager.FallbackStatus.InvalidPerformance or PipelineManager.FallbackStatus.InvalidRig)
                {
                    vrcAvatar.Tags = vrcAvatar.Tags.Where(t => t != AvatarFallbackTag).ToList();
                    vrcAvatar = await VRCApi.UpdateAvatarInfo(pipelineManager.blueprintId, vrcAvatar, cancellationToken: cancellationToken);
                }
            }

            return vrcAvatar;
        }
    }
}

#endif
