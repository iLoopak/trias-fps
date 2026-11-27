using UnityEngine;

namespace Trias.Native
{
    // Restores adult human proportions on the licensed Kenney rig that drives every pilot.
    // The rig is chibi-proportioned (legs 35 % of height, head 27 %), which squashed the Blender
    // suit limbs (authored for a 0.35 m thigh and 0.37 m shin) to toy size. The clips carry constant
    // translation curves on every bone, so the joint offsets are re-applied after animation each
    // frame, before hand placement and Blender suit mounting read the joints.
    // Visual only: the server hitbox (rules.js, 1.78 m body) is unchanged and now matches the helmet.
    [DefaultExecutionOrder(-200)]
    internal sealed class PilotProportions : MonoBehaviour
    {
        static readonly (string Joint, float Factor)[] Offsets =
        {
            ("LeftLeg", 1.41f), ("RightLeg", 1.41f),          // knee from hip: thigh
            ("LeftFoot", 1.41f), ("RightFoot", 1.41f),        // ankle from knee: shin
            ("LeftForeArm", 1.30f), ("RightForeArm", 1.30f),  // elbow from shoulder: upper arm
            ("LeftHand", 1.15f), ("RightHand", 1.15f),        // wrist from elbow: forearm
            ("Head", .45f),                                   // skull base sits on a short neck
        };

        Transform[] joints;
        Vector3[] rest;

        internal static PilotProportions Attach(GameObject rig)
        {
            var proportions = rig.AddComponent<PilotProportions>();
            var all = rig.GetComponentsInChildren<Transform>();
            proportions.joints = new Transform[Offsets.Length];
            proportions.rest = new Vector3[Offsets.Length];
            for (int i = 0; i < Offsets.Length; i++)
            {
                var joint = System.Array.Find(all, t => t.name == Offsets[i].Joint);
                proportions.joints[i] = joint;
                if (joint != null) proportions.rest[i] = joint.localPosition;
            }
            proportions.Apply();
            return proportions;
        }

        internal void Apply()
        {
            for (int i = 0; i < joints.Length; i++)
                if (joints[i] != null) joints[i].localPosition = rest[i] * Offsets[i].Factor;
        }

        void LateUpdate() => Apply();
    }
}
