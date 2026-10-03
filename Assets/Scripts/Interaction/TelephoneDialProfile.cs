using System;
using UnityEngine;

namespace DetectiveGame.Interaction
{
    [CreateAssetMenu(menuName = "Detective Game/Telephone/Dial Angle Profile")]
    public sealed class TelephoneDialProfile : ScriptableObject
    {
        [Serializable]
        public sealed class DigitAngle
        {
            [Range(0, 9)] public int digit;
            [Range(0f, 360f)] public float targetAngle;
            [Range(0.1f, 45f)] public float angleTolerance = 12f;
            public DigitAngle(int digit, float angle) { this.digit = digit; targetAngle = angle; }
        }

        [SerializeField] private DigitAngle[] digits =
        {
            new DigitAngle(0, 340), new DigitAngle(1, 70), new DigitAngle(2, 100),
            new DigitAngle(3, 130), new DigitAngle(4, 160), new DigitAngle(5, 190),
            new DigitAngle(6, 220), new DigitAngle(7, 250), new DigitAngle(8, 280),
            new DigitAngle(9, 310)
        };

        public DigitAngle[] Digits => digits;
        public float MaximumTargetAngle
        {
            get
            {
                float result = 0f;
                if (digits != null)
                    foreach (DigitAngle entry in digits)
                        if (entry != null) result = Mathf.Max(result, entry.targetAngle);
                return result;
            }
        }

        public bool IsValid(out string error)
        {
            var seen = new bool[10];
            if (digits == null || digits.Length != 10) { error = "Configure exactly ten digit angles."; return false; }
            foreach (DigitAngle entry in digits)
            {
                if (entry == null || entry.digit < 0 || entry.digit > 9 || seen[entry.digit] ||
                    float.IsNaN(entry.targetAngle) || float.IsInfinity(entry.targetAngle) ||
                    entry.targetAngle <= 0 || entry.targetAngle > 360 ||
                    float.IsNaN(entry.angleTolerance) || float.IsInfinity(entry.angleTolerance) || entry.angleTolerance <= 0)
                { error = "Each digit 0–9 needs one positive angle and tolerance."; return false; }
                seen[entry.digit] = true;
            }
            error = null;
            return true;
        }

        public float[] GetAngles()
        {
            var result = new float[10];
            if (digits != null)
                foreach (DigitAngle entry in digits)
                    if (entry != null && entry.digit >= 0 && entry.digit < 10) result[entry.digit] = entry.targetAngle;
            return result;
        }

        public int MatchDigit(float angle)
        {
            int selected = -1;
            float closest = float.MaxValue;
            foreach (DigitAngle entry in digits)
            {
                if (entry == null) continue;
                float difference = Mathf.Abs(angle - entry.targetAngle);
                if (difference <= entry.angleTolerance && difference < closest)
                { closest = difference; selected = entry.digit; }
            }
            return selected;
        }
    }
}

