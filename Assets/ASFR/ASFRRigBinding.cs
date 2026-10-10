using UnityEngine;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace ASFR
{
    // Build-time marker only, removed by NDMF before upload.
    public sealed class ASFRRigBinding : MonoBehaviour, VRC.SDKBase.IEditorOnly
    {
        public VRCParentConstraint[] captures;
        public Transform mainArmature;
        public Transform statueGrabAnchor;
        public Transform statueOrigin;
        public Transform statueCameraMount;
        public Transform[] freezeAnchors;
        public VRCParentConstraint[] freezeRepresentatives;
        public SkinnedMeshRenderer[] overlaySources;
        public SkinnedMeshRenderer[] overlays;
        public SkinnedMeshRenderer[] clothingRemovalCopies;
        public Transform[] visibilitySources;
        public Transform[] visibilityGates;
    }
}
