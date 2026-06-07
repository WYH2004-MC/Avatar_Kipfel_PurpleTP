// MIT License
// https://gist.github.com/anatawa12/581b66619711eaf5ebacbd85369d62e6
// 
// Copyright (c) 2023 anatawa12
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#if UNITY_EDITOR && (!ANATAWA12_GISTS_VPM_PACKAGE || GIST_581b66619711eaf5ebacbd85369d62e6)

using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace anatawa12.gists
{
    internal static class SetRandomBlueprintId
    {
        private static readonly Type PipelineManagerType = FindType("VRC.Core.PipelineManager");
        private static readonly Type AvatarDescriptorType = FindType("VRC.SDKBase.VRC_AvatarDescriptor");
        private static readonly Type SceneDescriptorType = FindType("VRC.SDKBase.VRC_SceneDescriptor");

        static SetRandomBlueprintId()
        {
        }

        [MenuItem("CONTEXT/PipelineManager/Set Random Blueprint ID")]
        private static async void Component(MenuCommand menuCommand)
        {
            if (PipelineManagerType == null || !PipelineManagerType.IsInstanceOfType(menuCommand.context))
                return;
            var context = (Component)menuCommand.context;

            var serialized = new SerializedObject(menuCommand.context);
            var blueprintId = serialized.FindProperty("blueprintId");
            var contentType = serialized.FindProperty("contentType");
            var completedSDKPipeline = serialized.FindProperty("completedSDKPipeline");

            if (!string.IsNullOrEmpty(blueprintId.stringValue))
            {
                if (!EditorUtility.DisplayDialog("Confirm?",
                        "BlueprintID is already set. Are you want to clear and set new BlueprintID?", "Clear & Set",
                        "Cancel"))
                {
                    return;
                }
            }

            bool isAvatar = context.GetComponent(AvatarDescriptorType);
            bool isScene = context.GetComponent(SceneDescriptorType);

            if (isAvatar && isScene)
            {
                EditorUtility.DisplayDialog("Error?",
                    "Both VRC Avatar Descriptor and VRC Scene Descriptor are attached!", "OK");
                return;
            }
            
            if (!isAvatar && !isScene)
            {
                EditorUtility.DisplayDialog("Error?",
                    "Both VRC Avatar Descriptor and VRC Scene Descriptor are attached!", "OK");
                return;
            }

            if (isAvatar)
            {
                blueprintId.stringValue = await CreateNewAvatar(context.gameObject.name);
                contentType.enumValueIndex = (int)ContentType.avatar;
            }
            else
            {
                Assert.IsTrue(isScene);
                blueprintId.stringValue = "wrld_" + Guid.NewGuid();
                contentType.enumValueIndex = (int)ContentType.world;
            }

            completedSDKPipeline.boolValue = false;

            serialized.ApplyModifiedProperties();
        }

        private static async Task<string> CreateNewAvatar(string name)
        {
            var vrcApiType = FindType("VRC.SDKBase.Editor.Api.VRCApi");
            var vrcAvatarType = FindType("VRC.SDKBase.Editor.Api.VRCAvatar");
            var nameProperty = vrcAvatarType?.GetProperty("Name");
            var descriptionProperty = vrcAvatarType?.GetProperty("Description");
            var tagsProperty = vrcAvatarType?.GetProperty("Tags");
            var releaseStatusProperty = vrcAvatarType?.GetProperty("ReleaseStatus");
            var idProperty = vrcAvatarType?.GetProperty("ID");
            var taskOfVrcAvatarType = vrcAvatarType != null ? FindType("System.Threading.Tasks.Task`1")?.MakeGenericType(vrcAvatarType) : null;
            var taskOfVRCAvatarResultProperty = taskOfVrcAvatarType?.GetProperty("Result");

            // We don't simply use VRCApi.CreateNewAvatar because it may not exist in older SDK versions.
            // We don't simply use GetMethods because VRChat preserves source-compatibility but not binary-compatibility, in other words, VRChat may add some optional parameters.
            var createAvatarRecordMethod = vrcApiType
                ?.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                .Where(method =>
                {
                    var parameters = method.GetParameters();
                    return method.Name == "CreateAvatarRecord"
                           && parameters.Length >= 1
                           && parameters[0].ParameterType == vrcAvatarType
                           && parameters.Skip(1).All(p => p.IsOptional);
                })
                .FirstOrDefault();
            if (
                vrcApiType != null && vrcAvatarType != null && 
                createAvatarRecordMethod != null &&
                nameProperty != null && nameProperty.PropertyType == typeof(string) && nameProperty.GetSetMethod() is {} setNameMethod &&
                descriptionProperty != null && descriptionProperty.PropertyType == typeof(string) && descriptionProperty.GetSetMethod() is {} setDescriptionMethod &&
                tagsProperty != null && tagsProperty.PropertyType == typeof(System.Collections.Generic.List<string>) && tagsProperty.GetSetMethod() is {} setTagsMethod &&
                releaseStatusProperty != null && releaseStatusProperty.PropertyType == typeof(string) && releaseStatusProperty.GetSetMethod() is {} setReleaseStatusMethod &&
                idProperty != null && idProperty.PropertyType == typeof(string) &&
                taskOfVrcAvatarType != null && taskOfVrcAvatarType.IsAssignableFrom(createAvatarRecordMethod.ReturnType) &&
                taskOfVRCAvatarResultProperty != null && taskOfVRCAvatarResultProperty.GetGetMethod() is {} getResultMethod
                )
            {
                var vrcAvatarInstance = Activator.CreateInstance(vrcAvatarType);
                setNameMethod.Invoke(vrcAvatarInstance, new object[] { name });
                setDescriptionMethod.Invoke(vrcAvatarInstance, new object[] { "" });
                setTagsMethod.Invoke(vrcAvatarInstance, new object[] { new System.Collections.Generic.List<string>() });
                setReleaseStatusMethod.Invoke(vrcAvatarInstance, new object[] { "private" });

                // create parameters with default values
                var parameters = createAvatarRecordMethod.GetParameters();
                var args = new object[parameters.Length];
                args[0] = vrcAvatarInstance;
                for (int i = 1; i < parameters.Length; i++)
                    args[i] = Type.Missing;
                var task = (Task)createAvatarRecordMethod.Invoke(null, args);
                await task;
                var createdAvatar = getResultMethod.Invoke(task, null);
                var id = (string)idProperty.GetValue(createdAvatar);

                return id;
            }
            else
            {
                return "avtr_" + Guid.NewGuid();
            }
            // The versions before CreateNewAvatar method. simply use random guid.
        }

        private static Type FindType(string typeName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(avatarValidation => avatarValidation != null);
        }

        private enum ContentType
        {
            avatar,
            world,
        }
    }
}

#endif
