// Input capture, separated from gameplay execution (master skill §1).
// Keyboard: W/Up, S/Down, A/D/Left/Right, Space. Touch buttons in GameUI
// set TouchThrottle/TouchSteer/TouchFire, which override keyboard when set.
using UnityEngine;
using SmashKarts.Core;

namespace SmashKarts.Runtime
{
    public static class InputProvider
    {
        public static float TouchThrottle, TouchSteer;
        public static bool TouchFire, TouchActive;

        public static SmashKarts.Core.Input Get()
        {
            if (TouchActive) return new SmashKarts.Core.Input { Throttle = TouchThrottle, Steer = TouchSteer, Fire = TouchFire };
            float throttle = 0, steer = 0;
            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow)) throttle += 1;
            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow)) throttle -= 1;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) steer += 1;
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) steer -= 1;
            return new SmashKarts.Core.Input { Throttle = throttle, Steer = steer, Fire = UnityEngine.Input.GetKey(KeyCode.Space) };
        }

        public static bool MenuPressed => UnityEngine.Input.GetKeyDown(KeyCode.Escape);
        public static bool ScoreboardHeld => UnityEngine.Input.GetKey(KeyCode.Tab);
    }
}
