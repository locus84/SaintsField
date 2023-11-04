﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SaintsField.Editor.Core;
using SaintsField.Editor.Utils;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SaintsField.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(VisibilityAttribute))]
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    [CustomPropertyDrawer(typeof(HideIfAttribute))]
    public class VisibilityAttributeDrawer: SaintsPropertyDrawer
    {
        protected override (bool isForHide, bool orResult) GetAndVisibility(SerializedProperty property, ISaintsAttribute saintsAttribute)
        {
            VisibilityAttribute visibilityAttribute = ((VisibilityAttribute)saintsAttribute);

            Object target = property.serializedObject.targetObject;
            Type type = target.GetType();

            _errors.Clear();

            return (visibilityAttribute.IsForHide, visibilityAttribute.OrCallbacks.All(callback => IsTruly(target, type, callback)));
        }

        private bool IsTruly(Object target, Type type, string by)
        {
            // SerializedObject serializedObject = property.serializedObject;
            //
            // SerializedProperty prop = serializedObject.FindProperty(by) ?? serializedObject.FindProperty($"<{by}>k__BackingField");
            // if (prop != null)
            // {
            //
            // }

            (ReflectUtils.GetPropType getPropType, object fieldOrMethodInfo) found = ReflectUtils.GetProp(type, by);
            switch (found)
            {
                case (ReflectUtils.GetPropType.NotFound, _):
                {
                    string error = $"No field or method named `{by}` found on `{target}`";
                    Debug.LogError(error);
                    _errors.Add(error);
                    return false;
                }
                case (ReflectUtils.GetPropType.Property, PropertyInfo propertyInfo):
                {
                    return ReflectUtils.Truly(propertyInfo.GetValue(target));
                }
                case (ReflectUtils.GetPropType.Field, FieldInfo foundFieldInfo):
                {
                    return ReflectUtils.Truly(foundFieldInfo.GetValue(target));
                }
                case (ReflectUtils.GetPropType.Method, MethodInfo methodInfo):
                {
                    ParameterInfo[] methodParams = methodInfo.GetParameters();
                    Debug.Assert(methodParams.All(p => p.IsOptional));
                    // Debug.Assert(methodInfo.ReturnType == typeof(bool));
                    return ReflectUtils.Truly(methodInfo.Invoke(target,
                        methodParams.Select(p => p.DefaultValue).ToArray()));
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(found), found, null);
            }
        }

        private readonly List<string> _errors = new List<string>();

        protected override bool WillDrawBelow(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute)
        {
            return _errors.Count > 0;
        }

        protected override Rect DrawBelow(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute)
        {
            if (_errors.Count == 0)
            {
                return position;
            }

            string error = string.Join("\n\n", _errors);

            (Rect errorRect, Rect leftRect) = RectUtils.SplitHeightRect(position, HelpBox.GetHeight(error, position.width, MessageType.Error));
            HelpBox.Draw(errorRect, error, MessageType.Error);
            return leftRect;
        }

        protected override float GetBelowExtraHeight(SerializedProperty property, GUIContent label,
            float width,
            ISaintsAttribute saintsAttribute)
        {
            // Debug.Log("check extra height!");
            if (_errors.Count == 0)
            {
                return 0;
            }

            // Debug.Log(HelpBox.GetHeight(_error));
            return HelpBox.GetHeight(string.Join("\n\n", _errors), width, MessageType.Error);
        }
    }
}