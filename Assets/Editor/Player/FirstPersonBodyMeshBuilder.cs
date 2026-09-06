using System;
using System.Collections.Generic;
using UnityEngine;

namespace DetectiveGame.EditorTools
{
    /// <summary>Editor-only mesh processing, so the original FBX does not need Read/Write enabled in builds.</summary>
    public static class FirstPersonBodyMeshBuilder
    {
        public static Mesh Build(SkinnedMeshRenderer source, Transform excludedBoneRoot)
        {
            if (source == null || source.sharedMesh == null || excludedBoneRoot == null)
                throw new ArgumentException("A skinned mesh and a neck/head root are required.");

            Mesh original = source.sharedMesh;
            var bones = source.bones;
            var weights = original.boneWeights;
            if (weights.Length != original.vertexCount)
                throw new InvalidOperationException("The source must have per-vertex skin weights.");

            var excludedBones = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                excludedBones[i] = bones[i] != null && bones[i].IsChildOf(excludedBoneRoot);

            var excludedVertices = new bool[weights.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                float excludedWeight = (excludedBones[w.boneIndex0] ? w.weight0 : 0f)
                    + (excludedBones[w.boneIndex1] ? w.weight1 : 0f)
                    + (excludedBones[w.boneIndex2] ? w.weight2 : 0f)
                    + (excludedBones[w.boneIndex3] ? w.weight3 : 0f);
                excludedVertices[i] = excludedWeight >= 0.5f;
            }

            // Copy all UVs, normals, tangents, bind poses and blend shapes; change only triangle lists.
            Mesh body = UnityEngine.Object.Instantiate(original);
            body.name = original.name + "_FirstPersonBody";
            int removed = 0;
            int retained = 0;
            for (int submesh = 0; submesh < original.subMeshCount; submesh++)
            {
                int[] triangles = original.GetTriangles(submesh);
                var kept = new List<int>(triangles.Length);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    if (excludedVertices[triangles[i]] || excludedVertices[triangles[i + 1]] || excludedVertices[triangles[i + 2]])
                    {
                        removed++;
                        continue;
                    }
                    kept.Add(triangles[i]);
                    kept.Add(triangles[i + 1]);
                    kept.Add(triangles[i + 2]);
                    retained++;
                }
                body.SetTriangles(kept, submesh, false);
            }
            body.bounds = original.bounds;
            if (removed == 0 || retained == 0)
            {
                UnityEngine.Object.DestroyImmediate(body);
                throw new InvalidOperationException("Head masking removed either no faces or the entire body. Check the bone root.");
            }
            return body;
        }
    }
}
