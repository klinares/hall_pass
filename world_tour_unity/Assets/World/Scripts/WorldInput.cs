using System;
using UnityEngine;

namespace WorldTour.World
{
    /// <summary>
    /// Shared switches between the walking code and the gameplay layer (phase 8.1), so the world stays free of any UI
    /// reference. While <see cref="Paused"/> is true the player does not move and no click or key walks her anywhere.
    /// </summary>
    public static class WorldInput
    {
        /// <summary>A talk, gig, show or travel card is open: the world takes no input.</summary>
        public static bool Paused;

        /// <summary>Given the click position in screen pixels, true when the click landed on a UI control (it must not also walk her).</summary>
        public static Func<Vector2, bool> ClickIsOnUi;

        /// <summary>Given the click position, true when the gameplay layer used the click itself (a click on a crew member).</summary>
        public static Func<Vector2, bool> ClickUsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Paused = false; ClickIsOnUi = null; ClickUsed = null; }
    }

    /// <summary>Sets Animator parameters only when the controller really has them (the placeholder figures have no Animator at all).</summary>
    public static class AnimSafe
    {
        static readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<int, AnimatorControllerParameterType>> Cache =
            new System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<int, AnimatorControllerParameterType>>();

        static bool Has(Animator a, string name, AnimatorControllerParameterType type)
        {
            if (a == null || a.runtimeAnimatorController == null) return false;
            int id = a.GetInstanceID();
            System.Collections.Generic.Dictionary<int, AnimatorControllerParameterType> map;
            if (!Cache.TryGetValue(id, out map))
            {
                map = new System.Collections.Generic.Dictionary<int, AnimatorControllerParameterType>();
                foreach (var p in a.parameters) map[p.nameHash] = p.type;
                Cache[id] = map;
            }
            AnimatorControllerParameterType t;
            return map.TryGetValue(Animator.StringToHash(name), out t) && t == type;
        }

        public static void Float(Animator a, string name, float v) { if (Has(a, name, AnimatorControllerParameterType.Float)) a.SetFloat(name, v); }
        public static void Bool(Animator a, string name, bool v) { if (Has(a, name, AnimatorControllerParameterType.Bool)) a.SetBool(name, v); }
        public static void Trigger(Animator a, string name) { if (Has(a, name, AnimatorControllerParameterType.Trigger)) a.SetTrigger(name); }
    }
}
