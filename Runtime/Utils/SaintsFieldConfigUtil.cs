
using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SaintsField.Utils
{
    public static class SaintsFieldConfigUtil
    {
        public const string EditorResourcePath = "SaintsField/SaintsFieldConfig.asset";

        public static SaintsFieldConfig Config;

        public static SaintsFieldConfig GetConfig()
        {
#if UNITY_EDITOR
            if (Config == null)
            {
                ReloadConfig();
            }
#endif

            return Config;
        }

#if UNITY_EDITOR
#if UNITY_2019_2_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ReloadConfig()
        {
#if SAINTSFIELD_DEBUG
            UnityEngine.Debug.Log("Load SaintsFieldConfig");
#endif
            try
            {
                Config = (SaintsFieldConfig)EditorGUIUtility.Load(EditorResourcePath);
            }
            catch (Exception e)
            {
                // do nothing
#if SAINTSFIELD_DEBUG
                UnityEngine.Debug.LogWarning(e);
#endif
            }
        }
#endif
#endif

        public static EXP GetComponentExp(EXP defaultValue) => GetConfig()?.getComponentExp ?? defaultValue;

        public static EXP GetComponentInChildrenExp(EXP defaultValue) => GetConfig()?.getComponentInChildrenExp ?? defaultValue;
        public static EXP GetComponentInParentExp(EXP defaultValue) => GetConfig()?.getComponentInParentExp ?? defaultValue;
        public static EXP GetComponentInParentsExp(EXP defaultValue) => GetConfig()?.getComponentInParentsExp ?? defaultValue;
        public static EXP GetComponentInSceneExp(EXP defaultValue) => GetConfig()?.getComponentInSceneExp ?? defaultValue;
        public static EXP GetPrefabWithComponentExp(EXP defaultValue) => GetConfig()?.getPrefabWithComponentExp ?? defaultValue;
        public static EXP GetScriptableObjectExp(EXP defaultValue) => GetConfig()?.getScriptableObjectExp ?? defaultValue;
        public static EXP GetByXPathExp(EXP defaultValue) => GetConfig()?.getByXPathExp ?? defaultValue;
        public static EXP GetComponentByPathExp(EXP defaultValue) => GetConfig()?.getComponentByPathExp ?? defaultValue;
        public static EXP FindComponentExp(EXP defaultValue) => GetConfig()?.findComponentExp ?? defaultValue;

        public static int ResizableTextAreaMinRow() => GetConfig()?.resizableTextAreaMinRow ?? 3;
    }
}
