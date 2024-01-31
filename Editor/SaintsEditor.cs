﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SaintsField.Editor.Playa;
using SaintsField.Editor.Playa.Renderer;
// ReSharper disable once RedundantUsingDirective
using SaintsField.Editor.Playa.RendererGroup;
using SaintsField.Editor.Utils;
using SaintsField.Playa;
using UnityEditor;
using UnityEngine;
#if UNITY_2022_2_OR_NEWER && !SAINTSFIELD_UI_TOOLKIT_DISABLE
using UnityEditor.UIElements;
using UnityEngine.UIElements;
#endif
#if SAINTSFIELD_DOTWEEN
using DG.DOTweenEditor;
#endif


namespace SaintsField.Editor
{
    public class SaintsEditor: UnityEditor.Editor
    {
        private MonoScript _monoScript;
        private List<SaintsFieldWithInfo> _fieldWithInfos = new List<SaintsFieldWithInfo>();

#if SAINTSFIELD_DOTWEEN
        private static readonly HashSet<SaintsEditor> AliveInstances = new HashSet<SaintsEditor>();
        private void RemoveInstance()
        {
            AliveInstances.Remove(this);
            if (AliveInstances.Count == 0)
            {
                DOTweenEditorPreview.Stop();
            }
        }
#endif

        #region UI

        #region UIToolkit
#if UNITY_2022_2_OR_NEWER && !SAINTSFIELD_UI_TOOLKIT_DISABLE

        public override VisualElement CreateInspectorGUI()
        {
            Setup();
            // return new Label("This is a Label in a Custom Editor");
            if (target == null)
            {
                return new HelpBox("The target object is null. Check for missing scripts.", HelpBoxMessageType.Error);
            }

            VisualElement root = new VisualElement();

#if SAINTSFIELD_DOTWEEN
            root.RegisterCallback<AttachToPanelEvent>(_ => AliveInstances.Add(this));
            root.RegisterCallback<DetachFromPanelEvent>(_ => RemoveInstance());
#endif

            if(_monoScript)
            {
                ObjectField objectField = new ObjectField("Script")
                {
                    bindingPath = "m_Script",
                    value = _monoScript,
                    allowSceneObjects = false,
                    objectType = typeof(MonoScript),
                };
                objectField.Bind(serializedObject);
                objectField.SetEnabled(false);
                root.Add(objectField);
            }

            while (_fieldWithInfos.Count > 0)
            {
                ISaintsRenderer renderer = PopRenderer(_fieldWithInfos, TryFixUIToolkit, serializedObject.targetObject);
                if(renderer != null)
                {
                    root.Add(renderer.CreateVisualElement());
                }
            }

            // foreach (SaintsFieldWithInfo fieldWithInfo in _fieldWithInfos)
            // {
            //     AbsRenderer renderer = MakeRenderer(fieldWithInfo, TryFixUIToolkit);
            //     if(renderer != null)
            //     {
            //         root.Add(renderer.CreateVisualElement());
            //     }
            // }

            return root;
        }

        protected virtual bool TryFixUIToolkit => true;

#endif
        #endregion

        #region IMGUI

        public override bool RequiresConstantRepaint() => true;

        public virtual void OnEnable()
        {
            Setup();
#if SAINTSFIELD_DOTWEEN
            AliveInstances.Add(this);
#endif
        }

        public virtual void OnDestroy()
        {
#if SAINTSFIELD_DOTWEEN
            RemoveInstance();
#endif
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
            {
                Debug.LogError("The target object is null. Check for missing scripts.");
                return;
            }

            if(_monoScript)
            {
                using(new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Script", _monoScript, typeof(MonoScript), false);
                }
            }

            serializedObject.Update();

            // _doTweenPlayGroup = null;
            List<SaintsFieldWithInfo> fieldWithInfoList = _fieldWithInfos.ToList();
#if SAINTSFIELD_DOTWEEN
            DOTweenPlayGroup preDoTweenPlayGroup = _doTweenPlayGroup;
            _doTweenPlayGroup = null;
#endif
            // if (_doTweenPlayGroup != null)
            // {
            //     fieldWithInfoList.RemoveAll(each => each.groups.Any(eachGroup => eachGroup.GroupBy == DOTweenPlayAttribute.DOTweenPlayGroupBy));
            // }
            while (fieldWithInfoList.Count > 0)
            {
                ISaintsRenderer renderer = PopRenderer(fieldWithInfoList, false, serializedObject.targetObject);
#if SAINTSFIELD_DOTWEEN
                if(renderer is DOTweenPlayGroup doTweenPlayGroup)
                {
                    _doTweenPlayGroup = preDoTweenPlayGroup ?? doTweenPlayGroup;
                    _doTweenPlayGroup.Render();
                    continue;
                }
#endif
                renderer?.Render();
                // renderer.AfterRender();
            }

            serializedObject.ApplyModifiedProperties();
        }
        #endregion

        #endregion

        private void Setup()
        {
            #region MonoScript
            if (serializedObject.targetObject)
            {
                try
                {
                    _monoScript = MonoScript.FromMonoBehaviour((MonoBehaviour) serializedObject.targetObject);
                }
                catch (Exception)
                {
                    try
                    {
                        _monoScript = MonoScript.FromScriptableObject((ScriptableObject)serializedObject.targetObject);
                    }
                    catch (Exception)
                    {
                        _monoScript = null;
                    }
                }
            }
            else
            {
                _monoScript = null;
            }
            #endregion

            List<SaintsFieldWithInfo> fieldWithInfos = new List<SaintsFieldWithInfo>();
            List<Type> types = ReflectUtils.GetSelfAndBaseTypes(target);
            string[] serializableFields = GetSerializedProperties().ToArray();
            foreach (int inherentDepth in Enumerable.Range(0, types.Count))
            {
                Type systemType = types[inherentDepth];

                FieldInfo[] allFields = systemType
                    .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic |
                               BindingFlags.Public | BindingFlags.DeclaredOnly);

                #region SerializedField

                IEnumerable<FieldInfo> serializableFieldInfos =
                    allFields.Where(fieldInfo =>
                        {
                            if (serializableFields.Contains(fieldInfo.Name))
                            {
                                return true;
                            }

                            // Name            : <GetHitPoint>k__BackingField
                            if (fieldInfo.Name.StartsWith("<") && fieldInfo.Name.EndsWith(">k__BackingField"))
                            {
                                return serializedObject.FindProperty(fieldInfo.Name) != null;
                            }

                            // return !fieldInfo.IsLiteral // const
                            //        && !fieldInfo.IsStatic // static
                            //        && !fieldInfo.IsInitOnly;
                            return false;
                        }
                        // readonly
                    );

                foreach (FieldInfo fieldInfo in serializableFieldInfos)
                {
                    // Debug.Log($"Name            : {fieldInfo.Name}");
                    // Debug.Log($"Declaring Type  : {fieldInfo.DeclaringType}");
                    // Debug.Log($"IsPublic        : {fieldInfo.IsPublic}");
                    // Debug.Log($"MemberType      : {fieldInfo.MemberType}");
                    // Debug.Log($"FieldType       : {fieldInfo.FieldType}");
                    // Debug.Log($"IsFamily        : {fieldInfo.IsFamily}");
                    OrderedAttribute orderProp = fieldInfo.GetCustomAttribute<OrderedAttribute>();
                    int order = orderProp?.Order ?? int.MinValue;

                    fieldWithInfos.Add(new SaintsFieldWithInfo
                    {
                        groups = fieldInfo.GetCustomAttributes<Attribute>().OfType<ISaintsGroup>().ToArray(),

                        RenderType = SaintsRenderType.SerializedField,
                        FieldInfo = fieldInfo,
                        InherentDepth = inherentDepth,
                        Order = order,
                        // serializable = true,
                    });
                }
                #endregion

                #region nonSerFieldInfo
                IEnumerable<FieldInfo> nonSerFieldInfos = allFields
                    .Where(f => f.GetCustomAttributes(typeof(ShowInInspectorAttribute), true).Length > 0);
                foreach (FieldInfo nonSerFieldInfo in nonSerFieldInfos)
                {
                    OrderedAttribute orderProp = nonSerFieldInfo.GetCustomAttribute<OrderedAttribute>();
                    int order = orderProp?.Order ?? int.MinValue;
                    fieldWithInfos.Add(new SaintsFieldWithInfo
                    {
                        groups = nonSerFieldInfo.GetCustomAttributes<Attribute>().OfType<ISaintsGroup>().ToArray(),

                        RenderType = SaintsRenderType.NonSerializedField,
                        // memberType = nonSerFieldInfo.MemberType,
                        FieldInfo = nonSerFieldInfo,
                        InherentDepth = inherentDepth,
                        Order = order,
                        // serializable = false,
                    });
                }
                #endregion

                #region Method

                MethodInfo[] methodInfos = systemType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic |
                                BindingFlags.Public | BindingFlags.DeclaredOnly);

                // var methodAllAttribute = methodInfos
                //     .SelectMany(each => each.GetCustomAttributes<Attribute>())
                //     .Where(each => each is ISaintsMethodAttribute)
                //     .ToArray();

                // IEnumerable<ISaintsMethodAttribute> buttonMethodInfos = methodAllAttribute.OfType<ISaintsMethodAttribute>().Length > 0);

                foreach (MethodInfo methodInfo in methodInfos)
                {
                    Attribute[] allMethodAttributes = methodInfo.GetCustomAttributes<Attribute>().ToArray();

                    if(allMethodAttributes.Any(each => each is ISaintsMethodAttribute))
                    {
                        OrderedAttribute orderProp =
                            allMethodAttributes.FirstOrDefault(each => each is OrderedAttribute) as OrderedAttribute;
                        int order = orderProp?.Order ?? int.MinValue;
                        fieldWithInfos.Add(new SaintsFieldWithInfo
                        {
                            groups = allMethodAttributes.OfType<ISaintsGroup>().ToArray(),

                            // memberType = MemberTypes.Method,
                            RenderType = SaintsRenderType.Method,
                            MethodInfo = methodInfo,
                            InherentDepth = inherentDepth,
                            Order = order,
                        });
                    }


                }
                #endregion

                #region NativeProperty
                IEnumerable<PropertyInfo> propertyInfos = systemType
                    .GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(p => p.GetCustomAttributes(typeof(ShowInInspectorAttribute), true).Length > 0);

                foreach (PropertyInfo propertyInfo in propertyInfos)
                {
                    OrderedAttribute orderProp =
                        propertyInfo.GetCustomAttribute<OrderedAttribute>();
                    int order = orderProp?.Order ?? int.MinValue;
                    fieldWithInfos.Add(new SaintsFieldWithInfo
                    {
                        groups = propertyInfo.GetCustomAttributes<Attribute>().OfType<ISaintsGroup>().ToArray(),

                        RenderType = SaintsRenderType.NativeProperty,
                        PropertyInfo = propertyInfo,
                        InherentDepth = inherentDepth,
                        Order = order,
                    });
                }
                #endregion
            }

            _fieldWithInfos = fieldWithInfos
                .WithIndex()
                .OrderBy(each => each.value.InherentDepth)
                .ThenBy(each => each.value.Order)
                .ThenBy(each => each.index)
                .Select(each => each.value)
                .ToList();
        }

        private IEnumerable<string> GetSerializedProperties()
        {
            // outSerializedProperties.Clear();
            // ReSharper disable once ConvertToUsingDeclaration
            using (SerializedProperty iterator = serializedObject.GetIterator())
            {
                // ReSharper disable once InvertIf
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        // outSerializedProperties.Add(serializedObject.FindProperty(iterator.name));
                        yield return iterator.name;
                    } while (iterator.NextVisible(false));
                }
            }
        }

        protected virtual AbsRenderer MakeRenderer(SaintsFieldWithInfo fieldWithInfo, bool tryFixUIToolkit)
        {
            // Debug.Log($"field {fieldWithInfo.fieldInfo?.Name}/{fieldWithInfo.fieldInfo?.GetCustomAttribute<ExtShowHideConditionBase>()}");
            switch (fieldWithInfo.RenderType)
            {
                case SaintsRenderType.SerializedField:
                    return new SerializedFieldRenderer(this, fieldWithInfo, tryFixUIToolkit);
                case SaintsRenderType.NonSerializedField:
                    return new NonSerializedFieldRenderer(this, fieldWithInfo, tryFixUIToolkit);
                case SaintsRenderType.Method:
                    return new MethodRenderer(this, fieldWithInfo, tryFixUIToolkit);
                case SaintsRenderType.NativeProperty:
                    return new NativePropertyRenderer(this, fieldWithInfo, tryFixUIToolkit);
                default:
                    throw new ArgumentOutOfRangeException(nameof(fieldWithInfo.RenderType), fieldWithInfo.RenderType, null);
            }
        }

#if SAINTSFIELD_DOTWEEN
        // every inspector instance can only have ONE doTweenPlayGroup
        private DOTweenPlayGroup _doTweenPlayGroup = null;
#endif

        protected virtual ISaintsRenderer PopRenderer(List<SaintsFieldWithInfo> fieldWithInfos, bool tryFixUIToolkit,
            object parent)
        {
            SaintsFieldWithInfo fieldWithInfo = fieldWithInfos[0];

            // Debug.Log($"group {fieldWithInfo.MethodInfo.Name} {fieldWithInfo.groups.Count}: {string.Join(",", fieldWithInfo.groups.Select(each => each.GroupBy))}");
#if SAINTSFIELD_DOTWEEN
            if(fieldWithInfo.groups.Count > 0)
            {
                ISaintsGroup group = fieldWithInfo.groups[0];
                Debug.Assert(group.GroupBy == DOTweenPlayAttribute.DOTweenPlayGroupBy);
                List<SaintsFieldWithInfo> groupFieldWithInfos = fieldWithInfos
                    .Where(each => each.groups.Any(eachGroup => eachGroup.GroupBy == group.GroupBy))
                    .ToList();

#if SAINTSFIELD_DEBUG && SAINTSFIELD_DEBUG_EDITOR_DOTWEEN
                Debug.Assert(_doTweenPlayGroup == null, "doTweenPlayGroup should only be created once");
#endif
                // Debug.Log($"create doTween play: {groupFieldWithInfos.Count}, {fieldWithInfo.MethodInfo.Name}");
                _doTweenPlayGroup = new DOTweenPlayGroup(groupFieldWithInfos.Select(each => (each.MethodInfo,
                    (DOTweenPlayAttribute)each.groups[0])), parent);
                fieldWithInfos.RemoveAll(each => groupFieldWithInfos.Contains(each));
                return _doTweenPlayGroup;
            }
#endif

            fieldWithInfos.RemoveAt(0);
            AbsRenderer result = MakeRenderer(fieldWithInfo, tryFixUIToolkit);
            // Debug.Log($"{result}, {fieldWithInfo.RenderType}, {fieldWithInfo.MethodInfo?.Name}");
            return result;
        }
    }
}