// MIT License
//
// Copyright (c) 2023 Haï~ (@vr_hai github.com/hai-vr)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//     of this software and associated documentation files (the "Software"), to deal
//     in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
//     furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
//     copies or substantial portions of the Software.
//
//     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor;

namespace Triturbo.FaceTrackingFramework.QuantizationParameters
{
    public class DeleteOscConfig
    {
        [InitializeOnLoadMethod]
        public static void RegisterCallback()
        {
            VRCSdkControlPanel.OnSdkPanelEnable += OnSdkPanelEnable;
        }

        private static void OnSdkPanelEnable(object sender, EventArgs args)
        {
            if (VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder))
            {
                builder.OnSdkUploadSuccess += OnSdkUploadSuccess;
            }
        }

        private static void OnSdkUploadSuccess(object sender, string avatarId)
        {
            TryDeleteOscConfigFile(avatarId);
        }


        public static bool TryDeleteOscConfigFile(string avatarId)
        {
            if (string.IsNullOrEmpty(avatarId)) return false;
            if (!APIUser.IsLoggedIn) return false;

            var userId = APIUser.CurrentUser.id;
            if (ContainsPathTraversalElements(userId) || ContainsPathTraversalElements(avatarId))
            {
                // Prevent the remote chance of a path traversal
                return false;
            }

            var endbit = $"/VRChat/VRChat/OSC/{userId}/Avatars/{avatarId}.json";
            var oscConfigFile = $"{VRC_SdkBuilder.GetLocalLowPath()}{endbit}";
            var printLocation = $"%LOCALAPPDATA%Low{endbit}"; // Doesn't print the account name to the logs
            if (!File.Exists(oscConfigFile)) return false;

            var fileAttributes = File.GetAttributes(oscConfigFile);
            if (fileAttributes.HasFlag(FileAttributes.Directory)) return false;

            try
            {
                File.Delete(oscConfigFile);
                Debug.Log($"[TriturboFT] Removed the OSC config file located at {printLocation}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TriturboFT] Failed to remove the OSC config file at {printLocation}; {e.ToString()}");
                return false;
            }

            return true;
        }

        private static bool ContainsPathTraversalElements(string susStr)
        {
            return susStr.Contains("/") || susStr.Contains("\\") || susStr.Contains(".") || susStr.Contains("*");
        }
    }
}