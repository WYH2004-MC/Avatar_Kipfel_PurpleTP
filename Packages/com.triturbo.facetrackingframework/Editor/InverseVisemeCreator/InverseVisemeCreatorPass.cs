using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_NDMF && ENABLE_MA
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;


namespace Triturbo.FaceTrackingFramework
{
    public class InverseVisemeCreatorPass : Pass<InverseVisemeCreatorPass>
    {

        protected override void Execute(BuildContext context)
        {
            var component = context.AvatarRootObject.GetComponentInChildren<Runtime.InverseVisemeCreatorComponent>();
            if(component == null) return;

            var avatarDescriptor = context.AvatarDescriptor;
            avatarDescriptor.VisemeSkinnedMesh.sharedMesh = UnityEngine.Object.Instantiate(avatarDescriptor.VisemeSkinnedMesh.sharedMesh);

            CreateInverseBlendShapes(avatarDescriptor.VisemeSkinnedMesh.sharedMesh, avatarDescriptor.VisemeBlendShapes);

        }
        
        public static void CreateInverseBlendShapes(Mesh mesh, string[] blendShapeNames)
        {
            string[] viseme = new string[] { "v_sil", "v_pp", "v_ff", "v_th", "v_dd", "v_kk", "v_ch", "v_ss", "v_nn", "v_rr", "v_aa", "v_e", "v_ih", "v_oh", "v_ou" };


            if (mesh == null || blendShapeNames == null || blendShapeNames.Length < 15)
            {
                Debug.LogError("Mesh or blendshape names are null or empty.");
                return;
            }

            for (int i = 0; i < 15; i++)
            {
                string blendShapeName = blendShapeNames[i];
                if (string.IsNullOrWhiteSpace(blendShapeName)) continue;

                // Ensure the blendshape exists on the mesh
                int blendShapeIndex = FindBlendShapeIndex(mesh, blendShapeName);

                if (blendShapeIndex == -1)
                {
                    Debug.LogWarning($"Blendshape '{blendShapeName}' not found on the mesh.");
                    continue;
                }

                // Create the inverse blendshape
                CreateInverseBlendShape(mesh, blendShapeIndex, $"inverse.{viseme[i]}");
            }
        }
        
        private static int FindBlendShapeIndex(Mesh mesh, string blendShapeName)
        {
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                if (mesh.GetBlendShapeName(i) == blendShapeName)
                    return i;
            }
            return -1;
        }
        
        private static void CreateInverseBlendShape(Mesh mesh, int blendShapeIndex, string newShapeName)
        {
            if (mesh.GetBlendShapeIndex(newShapeName) != -1)
            {
                return;
            }

            int vertexCount = mesh.vertexCount;
            Vector3[] deltaVertices = new Vector3[vertexCount];
            Vector3[] deltaNormals = new Vector3[vertexCount];
            Vector3[] deltaTangents = new Vector3[vertexCount];


            int frameCount = mesh.GetBlendShapeFrameCount(blendShapeIndex);

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                mesh.GetBlendShapeFrameVertices(blendShapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);

                // Invert the delta vertices
                for (int i = 0; i < vertexCount; i++)
                {
                    deltaVertices[i] = -deltaVertices[i];
                }

                // Add the inverse blendshape to the mesh
                float frameWeight = mesh.GetBlendShapeFrameWeight(blendShapeIndex, frameIndex);
                mesh.AddBlendShapeFrame(newShapeName, frameWeight, deltaVertices, deltaNormals, deltaTangents);
            }
        }
    }

    
    

}
#endif