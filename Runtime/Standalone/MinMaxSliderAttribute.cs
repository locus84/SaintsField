﻿using System;
using UnityEngine;

namespace ExtInspector.Standalone
{
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class MinMaxSliderAttribute : PropertyAttribute
    {
        public readonly float Min;
        public readonly float Max;
        public readonly bool DataFields = true;
        public readonly bool FlexibleFields = true;
        public readonly bool Bound = true;
        public readonly bool Round = true;

        public MinMaxSliderAttribute() : this(0, 1)
        {
        }

        public MinMaxSliderAttribute(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }
}
