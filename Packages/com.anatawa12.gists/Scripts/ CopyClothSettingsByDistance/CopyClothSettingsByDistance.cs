/*
 * Copy Cloth Settings By Distance
 * https://gist.github.com/anatawa12/3448b46d43b9f89ac22be985bf9c3c21
 *
 * Copy cloth settings from source cloth to destination cloth based on distance from a point.
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

#if UNITY_EDITOR && (!ANATAWA12_GISTS_VPM_PACKAGE || GIST_3448b46d43b9f89ac22be985bf9c3c21)

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using Object = System.Object;

namespace anatawa12.gists
{
    internal class CopyClothSettingsByDistance : EditorWindow
    {
        [MenuItem("Tools/anatawa12 gists/Copy Cloth Settings By Distance")]
        public static void Open() => GetWindow<CopyClothSettingsByDistance>("Copy Cloth Settings By Distance");

        Cloth sourceCloth;
        Cloth destinationCloth;
        bool copyMaxDistance = true;
        bool copyCollisionSphereDistance = true;

        private void OnGUI()
        {
            Undo.RecordObject(this, "Copy Cloth Settings By Distance UI Change");

            sourceCloth = (Cloth)EditorGUILayout.ObjectField("Source Cloth", sourceCloth, typeof(Cloth), true);
            destinationCloth = (Cloth)EditorGUILayout.ObjectField("Destination Cloth", destinationCloth, typeof(Cloth), true);
            copyMaxDistance = EditorGUILayout.Toggle("Copy Max Distance", copyMaxDistance);
            copyCollisionSphereDistance = EditorGUILayout.Toggle("Copy Collision Sphere Distance", copyCollisionSphereDistance);

            if (GUILayout.Button("Copy Settings"))
            {
                if (sourceCloth == null)
                {
                    Debug.LogError("Source Cloth is null");
                    return;
                }
                if (destinationCloth == null)
                {
                    Debug.LogError("Destination Cloth is null");
                    return;
                }
                CopySettings();
            }
        }

        private void CopySettings()
        {
            // copy cloth.coefficients based on distance of each vertex
            var sourceBakedMesh = new Mesh();
            var destinationBakedMesh = new Mesh();
            sourceCloth.GetComponent<SkinnedMeshRenderer>().BakeMesh(sourceBakedMesh);
            destinationCloth.GetComponent<SkinnedMeshRenderer>().BakeMesh(destinationBakedMesh);

            // create dest => source vertex map
            var destToSourceMap = CreateDestToSourceMap(sourceBakedMesh, destinationBakedMesh);

            // do copy coefficients
            var sourceCoefficients = sourceCloth.coefficients;
            var destinationCoefficients = destinationCloth.coefficients;

            // resize coefficients if needed
            if (destinationCoefficients.Length != destinationBakedMesh.vertexCount)
                Array.Resize(ref destinationCoefficients, destinationBakedMesh.vertexCount);
            if (sourceCoefficients.Length != sourceBakedMesh.vertexCount)
                Array.Resize(ref sourceCoefficients, sourceBakedMesh.vertexCount);

            for (var i = 0; i < destinationBakedMesh.vertexCount; i++)
            {
                var sourceIndex = destToSourceMap[i];

                var sourceCoefficient = sourceCoefficients[sourceIndex];
                var destinationCoefficient = destinationCoefficients[i];

                if (copyMaxDistance) destinationCoefficient.maxDistance = sourceCoefficient.maxDistance;
                if (copyCollisionSphereDistance) destinationCoefficient.collisionSphereDistance = sourceCoefficient.collisionSphereDistance;

                destinationCoefficients[i] = destinationCoefficient;
            }

            destinationCloth.coefficients = destinationCoefficients;
        }

        private int[] CreateDestToSourceMap(Mesh sourceBakedMesh, Mesh destinationBakedMesh)
        {
            var mapping = new int[destinationBakedMesh.vertexCount];

            for (var i = 0; i < destinationBakedMesh.vertices.Length; i++)
            {
                var position = destinationBakedMesh.vertices[i];
                var closestIndex = -1;
                var closestDistance = float.MaxValue;
                for (var j = 0; j < sourceBakedMesh.vertices.Length; j++)
                {
                    var sourcePosition = sourceBakedMesh.vertices[j];
                    var distance = Vector3.Distance(position, sourcePosition);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestIndex = j;
                    }
                }
                mapping[i] = closestIndex;
            }

            return mapping;
        }
    }
}

#endif
