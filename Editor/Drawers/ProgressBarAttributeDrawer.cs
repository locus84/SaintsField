﻿using System;
using System.Linq;
using System.Reflection;
using SaintsField.Editor.Core;
using SaintsField.Editor.Utils;
using SaintsField.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaintsField.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(ProgressBarAttribute))]
    public class ProgressBarAttributeDrawer: SaintsPropertyDrawer
    {
        private struct MetaInfo
        {
            // ReSharper disable InconsistentNaming
            public string Error;

            public float Min;  // dynamic
            public float Max;  // dynamic
            public Color Color;
            public Color BackgroundColor;
            // ReSharper enable InconsistentNaming
        }

        private static MetaInfo GetMetaInfo(ISaintsAttribute saintsAttribute, object parent)
        {
            ProgressBarAttribute progressBarAttribute = (ProgressBarAttribute) saintsAttribute;

            float min = progressBarAttribute.Min;
            if(progressBarAttribute.MinCallback != null)
            {
                (string error, float value) = Util.GetCallbackFloat(parent, progressBarAttribute.MinCallback);
                if (error != "")
                {
                    return new MetaInfo
                    {
                        Error = error,
                        Max = 100f,
                    };
                }
                min = value;
            }

            float max = progressBarAttribute.Max;
            if(progressBarAttribute.MaxCallback != null)
            {
                (string error, float value) = Util.GetCallbackFloat(parent, progressBarAttribute.MaxCallback);
                if (error != "")
                {
                    return new MetaInfo
                    {
                        Error = error,
                        Max = 100f,
                    };
                }
                max = value;
            }

            Color color = progressBarAttribute.Color.GetColor();

            if(progressBarAttribute.ColorCallback != null)
            {
                (string error, Color value) = GetCallbackColor(parent, progressBarAttribute.ColorCallback);
                if (error != "")
                {
                    return new MetaInfo
                    {
                        Error = error,
                        Max = 100f,
                    };
                }
                color = value;
            }

            Color backgroundColor = progressBarAttribute.BackgroundColor.GetColor();
            // ReSharper disable once InvertIf
            if(progressBarAttribute.BackgroundColorCallback != null)
            {
                (string error, Color value) = GetCallbackColor(parent, progressBarAttribute.BackgroundColorCallback);
                if (error != "")
                {
                    return new MetaInfo
                    {
                        Error = error,
                        Max = 100f,
                    };
                }
                backgroundColor = value;
            }

            // (string titleError, string _) = GetTitle(property, progressBarAttribute.TitleCallback, progressBarAttribute.Step, curValue, min, max, parent);
            // if (titleError != "")
            // {
            //     return new MetaInfo
            //     {
            //         Error = "",
            //         Min = min,
            //         Max = max,
            //         Color = color,
            //         BackgroundColor = backgroundColor,
            //         // Title = null,
            //     };
            // }

            return new MetaInfo
            {
                Error = "",
                Min = min,
                Max = max,
                Color = color,
                BackgroundColor = backgroundColor,
                // Title = title,
            };
        }

        private static (string error, Color value) GetCallbackColor(object target, string by)
        {
            (ReflectUtils.GetPropType getPropType, object fieldOrMethodInfo) found = ReflectUtils.GetProp(target.GetType(), by);

            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (found.Item1 == ReflectUtils.GetPropType.NotFound)
            {
                return ($"No field or method named `{by}` found on `{target}`", Color.white);
            }

            if (found.Item1 == ReflectUtils.GetPropType.Property)
            {
                return ObjToColor(((PropertyInfo)found.Item2).GetValue(target));
            }
            if (found.Item1 == ReflectUtils.GetPropType.Field)
            {
                return ObjToColor(((FieldInfo)found.Item2).GetValue(target));
            }
            // ReSharper disable once InvertIf
            if (found.Item1 == ReflectUtils.GetPropType.Method)
            {
                MethodInfo methodInfo = (MethodInfo)found.Item2;
                ParameterInfo[] methodParams = methodInfo.GetParameters();
                Debug.Assert(methodParams.All(p => p.IsOptional));
                // Debug.Assert(methodInfo.ReturnType == typeof(bool));
                return ObjToColor(methodInfo.Invoke(target, methodParams.Select(p => p.DefaultValue).ToArray()));
            }
            throw new ArgumentOutOfRangeException(nameof(found), found, null);
        }

        private static (string error, Color color) ObjToColor(object obj)
        {
            // ReSharper disable once ConvertSwitchStatementToSwitchExpression
            switch (obj)
            {
                case Color color:
                    return ("", color);
                case string str:
                    return ("", Colors.GetColorByStringPresent(str));
                case EColor eColor:
                    return ("", eColor.GetColor());
                default:
                    return ($"target is not a color: {obj}", Color.white);
            }
        }

        private static (string error, string title) GetTitle(SerializedProperty property, string titleCallback, float step, float curValue, float minValue, float maxValue, object parent)
        {
            if (titleCallback == null)
            {
                if(property.propertyType == SerializedPropertyType.Integer)
                {
                    return ("", $"{(int)curValue} / {(int)maxValue}");
                }

                if (step <= 0)
                {
                    return ("", $"{curValue} / {maxValue}");
                }

                string valueStr = step.ToString(System.Globalization.CultureInfo.InvariantCulture);
                int decimalPointIndex = valueStr.IndexOf(System.Globalization.CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal);

                int decimalPlaces = 0;

                if (decimalPointIndex >= 0)
                {
                    decimalPlaces = valueStr.Length - decimalPointIndex - 1;
                }

                string formatValue = curValue.ToString("F" + decimalPlaces);
                // Debug.Log($"curValue={curValue}, format={formatValue}");

                return ("", $"{formatValue} / {maxValue}");
            }
            const BindingFlags bindAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic |
                                          BindingFlags.Public | BindingFlags.DeclaredOnly;

            MethodInfo methodInfo = parent.GetType().GetMethod(titleCallback, bindAttr);

            if (methodInfo == null)
            {
                return ($"Can not find method `{titleCallback}` on `{parent}`", null);
            }

            string title;
            try
            {
                title = (string)methodInfo.Invoke(parent,
                    new object[]{curValue, minValue, maxValue, property.displayName});
            }
            catch (TargetInvocationException e)
            {
                Debug.Assert(e.InnerException != null);
                Debug.LogException(e);
                return (e.InnerException.Message, null);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return (e.Message, null);
            }

            return ("", title);
        }

        private static float BoundValue(float curValue, float minValue, float maxValue, float step) => step <= 0 ? Mathf.Clamp(curValue, minValue, maxValue) : Util.BoundFloatStep(curValue, minValue, maxValue, step);

        #region IMGUI
        protected override float GetFieldHeight(SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute,
            bool hasLabelWidth)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        protected override void DrawField(Rect position, SerializedProperty property, GUIContent label, ISaintsAttribute saintsAttribute,
            object parent)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive, position);
            Debug.Log(label.text.Length);
            Rect fieldRect = EditorGUI.PrefixLabel(position, controlId, label);
            // EditorGUI.DrawRect(position, Color.yellow);
            EditorGUI.DrawRect(fieldRect, EColor.Blue.GetColor());

            float curValue = property.floatValue;
            float percent = curValue / 100f;
            Rect fillRect = RectUtils.SplitWidthRect(fieldRect, fieldRect.width * percent).leftRect;

            EditorGUI.DrawRect(fillRect, EColor.Green.GetColor());

            Event e = Event.current;
            // Debug.Log($"{e.isMouse}, {e.mousePosition}");
            // ReSharper disable once InvertIf
            // Debug.Log($"{e.type} {e.isMouse}, {e.button}, {e.mousePosition}");

            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && fieldRect.Contains(e.mousePosition))
            {
                float newValue = (e.mousePosition.x - fieldRect.x) / fieldRect.width * 100f;
                property.floatValue = newValue;
                SetValueChanged(property);
            }

            EditorGUI.DropShadowLabel(fieldRect, $"{curValue:0.00}%");

            // if(e.type == EventType.MouseDrag && )

            // if (position.Contains(e.mousePosition))
            // {
            //     Debug.Log($"cap: {e.type} {e.isMouse}, {e.button}, {e.mousePosition}");
            // }
        }
        #endregion

        #region UI Toolkit

        private class UIToolkitPayload
        {
            // ReSharper disable once InconsistentNaming
            public readonly VisualElement Background;
            // ReSharper disable once InconsistentNaming
            public readonly VisualElement Progress;
            public MetaInfo metaInfo;

            public UIToolkitPayload(VisualElement background, VisualElement progress, MetaInfo metaInfo)
            {
                Background = background;
                Progress = progress;
                this.metaInfo = metaInfo;
            }
        }

        private static string NameProgressBar(SerializedProperty property) => $"{property.propertyPath}__ProgressBar";
        private static string NameLabel(SerializedProperty property) => $"{property.propertyPath}__ProgressBar_Label";
        private static string NameHelpBox(SerializedProperty property) => $"{property.propertyPath}__ProgressBar_HelpBox";

        protected override VisualElement CreateFieldUIToolKit(SerializedProperty property, ISaintsAttribute saintsAttribute,
            VisualElement container, Label fakeLabel, object parent)
        {
            ProgressBarAttribute progressBarAttribute = (ProgressBarAttribute)saintsAttribute;

            MetaInfo metaInfo = GetMetaInfo(progressBarAttribute,
                parent);

            Label label = Util.PrefixLabelUIToolKit(new string(' ', property.displayName.Length), 0);
            label.name = NameLabel(property);

            #region ProgrssBar

            ProgressBar progressBar = new ProgressBar
            {
                name = NameProgressBar(property),

                title = GetTitle(property, progressBarAttribute.TitleCallback, progressBarAttribute.Step, property.propertyType == SerializedPropertyType.Integer? property.intValue: property.floatValue, metaInfo.Min, metaInfo.Max, parent).title,
                lowValue = 0,
                highValue = metaInfo.Max - metaInfo.Min,
                value = property.floatValue,

                style =
                {
                    flexGrow = 1,
                },
            };

            Type type = typeof(AbstractProgressBar);
            FieldInfo backgroundFieldInfo = type.GetField("m_Background", BindingFlags.NonPublic | BindingFlags.Instance);

            VisualElement background = null;
            if (backgroundFieldInfo != null)
            {
                background = (VisualElement) backgroundFieldInfo.GetValue(progressBar);
                // background.style.backgroundColor = EColor.Aqua.GetColor();
                background.style.backgroundColor = metaInfo.BackgroundColor;
            }

            FieldInfo progressFieldInfo = type.GetField("m_Progress", BindingFlags.NonPublic | BindingFlags.Instance);
            VisualElement progress = null;
            if(progressFieldInfo != null)
            {
                progress = (VisualElement) progressFieldInfo.GetValue(progressBar);
                progress.style.backgroundColor = metaInfo.Color;
            }

            progressBar.userData = new UIToolkitPayload(background, progress, metaInfo);
            #endregion

            VisualElement root = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1,
                },
            };

            root.Add(label);
            root.Add(progressBar);

            return root;

        }

        protected override VisualElement CreateBelowUIToolkit(SerializedProperty property, ISaintsAttribute saintsAttribute, int index,
            VisualElement container, object parent)
        {
            return new HelpBox("", HelpBoxMessageType.Error)
            {
                name = NameHelpBox(property),
                style =
                {
                    display = DisplayStyle.None,
                },
            };
        }

        protected override void OnAwakeUIToolkit(SerializedProperty property, ISaintsAttribute saintsAttribute, int index, VisualElement container,
            Action<object> onValueChangedCallback, object parent)
        {
            ProgressBar progressBar = container.Q<ProgressBar>(NameProgressBar(property));

            progressBar.RegisterCallback<PointerDownEvent>(evt =>
            {
                progressBar.CapturePointer(0);
                OnProgressBarInteract(property, (ProgressBarAttribute)saintsAttribute, container, progressBar, evt.localPosition, onValueChangedCallback, parent);
            });
            progressBar.RegisterCallback<PointerUpEvent>(_ =>
            {
                progressBar.ReleasePointer(0);
            });
            progressBar.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if(progressBar.HasPointerCapture(0))
                {
                    OnProgressBarInteract(property, (ProgressBarAttribute)saintsAttribute, container, progressBar,
                        evt.localPosition, onValueChangedCallback, parent);
                }
            });
        }

        protected override void OnUpdateUIToolkit(SerializedProperty property, ISaintsAttribute saintsAttribute, int index,
            VisualElement container, Action<object> onValueChanged, object parent)
        {
            MetaInfo metaInfo = GetMetaInfo(saintsAttribute,
                parent);

            ProgressBar progressBar = container.Q<ProgressBar>(NameProgressBar(property));
            UIToolkitPayload uiToolkitPayload = (UIToolkitPayload)progressBar.userData;
            MetaInfo oldMetaInfo = uiToolkitPayload.metaInfo;

            bool changed = false;
            string error = metaInfo.Error;

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if(metaInfo.Min != oldMetaInfo.Min
               // ReSharper disable once CompareOfFloatsByEqualityOperator
               || metaInfo.Max != oldMetaInfo.Max)
            {
                changed = true;
                progressBar.highValue = metaInfo.Max - metaInfo.Min;
                float propValue = property.propertyType == SerializedPropertyType.Integer
                    ? property.intValue
                    : property.floatValue;

                ProgressBarAttribute progressBarAttribute = (ProgressBarAttribute)saintsAttribute;

                if(propValue < metaInfo.Min || propValue > metaInfo.Max)
                {
                    // Debug.Log($"update change: {metaInfo.Min} <= {propValue} <= {metaInfo.Max}");
                    propValue = ChangeValue(property, progressBarAttribute, container, progressBar, Mathf.Clamp(propValue, metaInfo.Min, metaInfo.Max), metaInfo.Min, metaInfo.Max,
                        onValueChanged, parent);
                    // Debug.Log($"now prop = {propValue}");
                }

                progressBar.value = propValue - metaInfo.Min;

                (string titleError, string title) = GetTitle(property, progressBarAttribute.TitleCallback, progressBarAttribute.Step, propValue, metaInfo.Min, metaInfo.Max, parent);
                // Debug.Log($"update change title {title}");
                progressBar.title = title;
                if (titleError != "")
                {
                    error = titleError;
                }
            }

            if(metaInfo.Color != oldMetaInfo.Color && uiToolkitPayload.Progress != null)
            {
                changed = true;
                uiToolkitPayload.Progress.style.backgroundColor = metaInfo.Color;
            }

            if(metaInfo.BackgroundColor != oldMetaInfo.BackgroundColor && uiToolkitPayload.Background != null)
            {
                changed = true;
                uiToolkitPayload.Background.style.backgroundColor = metaInfo.BackgroundColor;
            }

            if (changed)
            {
                // progressBar.userData = metaInfo;
                uiToolkitPayload.metaInfo = metaInfo;
            }

            UpdateHelpBox(property, container, error);
        }

        protected override void ChangeFieldLabelToUIToolkit(SerializedProperty property, ISaintsAttribute saintsAttribute, int index,
            VisualElement container, string labelOrNull)
        {
            Label label = container.Q<Label>(NameLabel(property));
            label.style.display = labelOrNull == null ? DisplayStyle.None : DisplayStyle.Flex;
            label.text = labelOrNull ?? "";
        }

        private static void OnProgressBarInteract(SerializedProperty property, ProgressBarAttribute progressBarAttribute, VisualElement container, ProgressBar progressBar, Vector3 mousePosition, Action<object> onValueChangedCallback, object parent)
        {
            float curWidth = progressBar.resolvedStyle.width;
            if(float.IsNaN(curWidth))
            {
                return;
            }

            UIToolkitPayload uiToolkitPayload = (UIToolkitPayload)progressBar.userData;

            float curValue = Mathf.Lerp(uiToolkitPayload.metaInfo.Min, uiToolkitPayload.metaInfo.Max, mousePosition.x / curWidth);
            ChangeValue(property, progressBarAttribute, container, progressBar, curValue, uiToolkitPayload.metaInfo.Min, uiToolkitPayload.metaInfo.Max, onValueChangedCallback, parent);
        }

        private static float ChangeValue(SerializedProperty property, ProgressBarAttribute progressBarAttribute, VisualElement container, ProgressBar progressBar, float curValue, float minValue, float maxValue, Action<object> onValueChangedCallback, object parent)
        {
            // UIToolkitPayload uiToolkitPayload = (UIToolkitPayload)progressBar.userData;

            float newValue = BoundValue(curValue, minValue, maxValue, progressBarAttribute.Step);

            // Debug.Log($"curValue={curValue}, newValue={newValue}");
            float propValue = property.propertyType == SerializedPropertyType.Integer
                ? property.intValue
                : property.floatValue;

            // Debug.Log($"curValue={curValue}, newValue={newValue}, propValue={propValue}");
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (propValue == newValue)
            {
                return propValue;
            }

            // Debug.Log($"update value to {newValue}");

            progressBar.value = newValue - minValue;

            if (property.propertyType == SerializedPropertyType.Integer)
            {
                int intValue = (int)newValue;
                property.intValue = intValue;
                property.serializedObject.ApplyModifiedProperties();
                onValueChangedCallback.Invoke(intValue);
            }
            else
            {
                property.floatValue = newValue;
                property.serializedObject.ApplyModifiedProperties();
                onValueChangedCallback.Invoke(newValue);
            }

            (string error, string title) = GetTitle(property, progressBarAttribute.TitleCallback, progressBarAttribute.Step, newValue, minValue, maxValue, parent);
            // Debug.Log($"change title to {title}");
            progressBar.title = title;
            UpdateHelpBox(property, container, error);

            return newValue;
        }

        private static void UpdateHelpBox(SerializedProperty property, VisualElement container, string error)
        {
            HelpBox helpBox = container.Q<HelpBox>(NameHelpBox(property));
            if (helpBox.text == error)
            {
                return;
            }
            helpBox.text = error;
            helpBox.style.display = string.IsNullOrEmpty(error) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        #endregion
    }
}
