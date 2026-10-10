using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace ASFR
{
    public partial class ASFRInstaller
    {
        List<string> CheckSetup()
        {
            var errors = new List<string>();
            if (!avatar)
            {
                errors.Add(ASFRLanguage.T("Choose an original scene avatar."));
                return errors;
            }

            if (EditorUtility.IsPersistent(avatar))
                errors.Add(ASFRLanguage.T("Use an avatar in the scene, not a prefab asset."));
            if (avatar.transform.Find("ASFR System"))
                errors.Add(ASFRLanguage.T("Select the original avatar. Generated ASFR copies cannot be used as input."));
            if (!mainArmature || mainArmature == avatar.transform || !mainArmature.IsChildOf(avatar.transform))
                errors.Add(ASFRLanguage.T("Choose the main armature inside the avatar under Advanced."));
            if (mainBody && (!mainBody.sharedMesh || !mainBody.transform.IsChildOf(avatar.transform)))
                errors.Add(ASFRLanguage.T("Main body must be a mesh inside the selected avatar."));
            if (mainBody && clothing.Contains(mainBody))
                errors.Add(ASFRLanguage.T("The main body cannot also be marked as clothing."));
            var animator = avatar.GetComponent<Animator>();
            if (!animator || !animator.isHuman || !animator.avatar || !animator.avatar.isValid)
                errors.Add(ASFRLanguage.T("Progressive freezing requires a valid humanoid Animator on the avatar root."));
            if (innerCoating.Any(r => r && selected.Contains(r)) && shellOffset <= 0)
                errors.Add(ASFRLanguage.T("Coat inner faces needs a positive shell offset under Advanced (try 0.0005 metres)."));
            var scale = avatar.transform.lossyScale;
            if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0)
                errors.Add(ASFRLanguage.T("Avatar scale must be positive on all axes."));
            if (!selected.Any(r => r && r.sharedMesh))
                errors.Add(ASFRLanguage.T("Include at least one skinned mesh."));
            if (overlayMaterials.Count < 1 || overlayMaterials.Count > 6)
                errors.Add(ASFRLanguage.T("Choose between one and six TransformationMats."));
            for (int i = 0; i < overlayMaterials.Count; i++)
                if (!overlayMaterials[i])
                    errors.Add(ASFRLanguage.T("Assign TransformationMat ") + (i + 1) + ASFRLanguage.T(" or remove the empty entry."));
                else if (!ASFROverlayMaterial.Supported(overlayMaterials[i]))
                    errors.Add(ASFRLanguage.T("TransformationMat ") + (i + 1) + ASFRLanguage.T(" needs a supported Poiyomi or full lilToon shader."));
            foreach (var r in selected.Concat(clothing).Where(r => r).Distinct())
            {
                if (!r.transform.IsChildOf(avatar.transform))
                {
                    errors.Add(r.name + ASFRLanguage.T(": mesh belongs to another avatar. Reselect the original avatar."));
                    continue;
                }

                if (!r.sharedMesh)
                {
                    errors.Add(r.name + ASFRLanguage.T(": mesh is missing."));
                    continue;
                }

                if (!r.sharedMesh.isReadable)
                {
                    errors.Add(r.name + ASFRLanguage.T(": enable mesh Read/Write before generating."));
                    continue;
                }

                errors.AddRange(ValidateSkin(r, avatar.transform));
                if (clothing.Contains(r))
                    foreach (var material in r.sharedMaterials.Where(m => m).Distinct())
                        if (!ASFROverlayMaterial.Supported(material))
                            errors.Add(r.name + " / " + material.name + ASFRLanguage.T(": clothing removal needs Poiyomi or full lilToon. Clear its clothing mark to skip removal."));
            }

            if (addVisionOverlay && !Shader.Find("ASFR/Frozen Vision"))
                errors.Add(ASFRLanguage.T("FrozenVision.shader is missing. Import the complete package."));
            if (addStatueCamera && !Shader.Find("ASFR/Statue Camera View"))
                errors.Add(ASFRLanguage.T("StatueCameraView.shader is missing. Import the complete package."));
            return errors.Distinct().ToList();
        }

        static void ValidateMenu(VRCExpressionsMenu menu, HashSet<VRCExpressionsMenu> visited)
        {
            if (!menu || !visited.Add(menu))
                return;
            if (menu.controls.Count > 8)
                throw new InvalidOperationException("Generated menu exceeds eight controls: " + menu.name);
            foreach (var item in menu.controls)
            {
                if (!item.icon)
                    throw new InvalidOperationException("Generated menu icon is missing: " + item.name);
                if (item.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    ValidateMenu(item.subMenu, visited);
            }
        }
    }
}
