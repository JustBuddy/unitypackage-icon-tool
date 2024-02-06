/*
* BUDDYWORKS UnityPackage Icon Tool
* Copyright (C) 2024 BUDDYWORKS
* hi@buddyworks.wtf

* This program is free software; you can redistribute it and/or
* modify it under the terms of the GNU Lesser General Public
* License as published by the Free Software Foundation; either
* version 3 of the License, or (at your option) any later version.

* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
* Lesser General Public License for more details.

* You should have received a copy of the GNU Lesser General Public License
* along with this program; if not, write to the Free Software Foundation,
* Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

using CaptiveReality.IO.Filesystem;
using HarmonyLib;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using SHOpenFolderAndSelectItems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BUDDYWORKS.UnityPackageIcon
{
    [InitializeOnLoad]
    internal class UI : EditorWindow
    {
        private static Texture2D _selectedIcon = null;
        private static Texture2D SelectedIcon { get { return _selectedIcon; } set { UpdateIcon(value); } }
        private static Harmony Harmony;
        private static MethodInfo TopAreaMethod;//, ExportPackageMethod;
        private static FieldInfo m_ExportPackageItemsField, enabledStatusField, guidField;
        private static bool PatchGUI = false;

        private const string PackageName = "wtf.buddyworks.uit";
        private static string IconSavePath = $"Packages/{PackageName}/selectedIcon.txt";

        static UI()
        {
            if (File.Exists(IconSavePath))
            {
                string guid = File.ReadAllText(IconSavePath);
                if (!string.IsNullOrEmpty(guid))
                {
                    SelectedIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                }
            }

            Harmony = new Harmony(PackageName);

            Type packageExportType = typeof(EditorWindow).Assembly.GetType("UnityEditor.PackageExport");
            if (packageExportType == null)
            {
                Debug.LogError("[UnityPackage Icon] Failed to find the type UnityEditor.PackageExport!");

                return;
            }

            MethodInfo exportMethod = packageExportType.GetMethod("Export", BindingFlags.NonPublic | BindingFlags.Instance);
            if (exportMethod == null)
            {
                Debug.LogError("[UnityPackage Icon] Failed to find the method UnityEditor.PackageExport.Export()!");

                return;
            }

            TopAreaMethod = packageExportType.GetMethod("TopArea", BindingFlags.NonPublic | BindingFlags.Instance);
            if (TopAreaMethod == null)
            {
                Debug.LogError("[UnityPackage Icon] Failed to find the method UnityEditor.PackageExport.TopArea()!");

                return;
            }

            MethodInfo showExportPackageMethod = packageExportType.GetMethod("ShowExportPackage", BindingFlags.NonPublic | BindingFlags.Static);
            if (showExportPackageMethod == null)
            {
                Debug.LogError("[UnityPackage Icon] Failed to find the method UnityEditor.PackageExport.ShowExportPackage()!");

                return;
            }

            /*var packageUtilityType = typeof(EditorWindow).Assembly.GetType("UnityEditor.PackageUtilityType");
            if (packageUtilityType == null)
            {
                Debug.LogError("[UnityPackage Icon] Failed to find the type UnityEditor.PackageUtility!");

                return;
            }

            ExportPackageMethod = packageUtilityType.GetMethod("ExportPackage", BindingFlags.Public | BindingFlags.Static);
            if (ExportPackageMethod == null)
            {
                Debug.LogError("[UnityPackage Icon] Failed to find the method UnityEditor.PackageUtility.ExportPackage()!");

                return;
            }*/

            m_ExportPackageItemsField = packageExportType.GetField("m_ExportPackageItems", BindingFlags.NonPublic | BindingFlags.Instance);
            if (m_ExportPackageItemsField == null)
            {
                Debug.LogError("[UnityPackage Icon] Failed to find the field UnityEditor.PackageExport.m_ExportPackageItems!");

                return;
            }

            // https://github.com/Unity-Technologies/UnityCsReference/blob/2d4714b26573c9f220da0e266d62f42830c14ad6/Editor/Mono/PackageUtility.bindings.cs#L36
            Type exportPackageItemType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ExportPackageItem");
            if (exportPackageItemType == null)
            {
                Debug.LogError("[UnityPackage Icon] Failed to find the type UnityEditor.ExportPackageItem!");

                return;
            }

            enabledStatusField = exportPackageItemType.GetField("enabledStatus", BindingFlags.Public | BindingFlags.Instance);
            if (enabledStatusField == null)
            {
                Debug.LogError("[UnityPackage Icon] Failed to find the field UnityEditor.ExportPackageItem.enabledStatusField!");

                return;
            }

            guidField = exportPackageItemType.GetField("guid", BindingFlags.Public | BindingFlags.Instance);
            if (guidField == null)
            {
                Debug.LogError("[UnityPackage Icon] Failed to find the field UnityEditor.ExportPackageItem.guid!");

                return;
            }

            Harmony.Patch(exportMethod, new HarmonyMethod(typeof(UI).GetMethod(nameof(Export), BindingFlags.NonPublic | BindingFlags.Static)));
            Harmony.Patch(showExportPackageMethod, null, new HarmonyMethod(typeof(UI).GetMethod(nameof(ShowExportPackage), BindingFlags.NonPublic | BindingFlags.Static)));
        }

        [MenuItem("BUDDYWORKS/Set UnityPackage export icon")]
        private static void Init()
        {
            UI window = (UI)GetWindow(typeof(UI));

            window.titleContent = new GUIContent("UnityPackage Icon");

            //window.maxSize = new Vector2(300, 64);

            //window.Show();
            window.ShowModal();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Package icon:", EditorStyles.boldLabel, GUILayout.Width(84), GUILayout.Height(64));
                SelectedIcon = (Texture2D)EditorGUILayout.ObjectField(GUIContent.none, SelectedIcon, typeof(Texture2D), false, GUILayout.Height(64), GUILayout.Width(64));
            }
        }

        private static void UpdateIcon(Texture2D icon)
        {
            if (SelectedIcon == icon) return;

            if (icon != null && ImageDetection.GetImageFormat(AssetDatabase.GetAssetPath(icon)) != ImageDetection.ImageFormat.PNG)
            {
                _selectedIcon = null;

                Debug.LogWarning("[UnityPackage Icon] The icon must be PNG formatted!");

                return;
            }

            _selectedIcon = icon;

            File.WriteAllText(IconSavePath, SelectedIcon == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(SelectedIcon)));
        }

        // Prevents printing 4 GUIStyle errors on script domain reload.
        // https://github.com/Unity-Technologies/UnityCsReference/blob/2d4714b26573c9f220da0e266d62f42830c14ad6/Editor/Mono/GUI/PackageExport.cs#L46
        private static void ShowExportPackage()
        {
            if (PatchGUI) return;

            PatchGUI = true;

            Harmony.Patch(TopAreaMethod, new HarmonyMethod(typeof(UI).GetMethod(nameof(TopArea), BindingFlags.NonPublic | BindingFlags.Static)));
        }

        // https://github.com/Unity-Technologies/UnityCsReference/blob/2d4714b26573c9f220da0e266d62f42830c14ad6/Editor/Mono/GUI/PackageExport.cs#L235
        private static bool Export(dynamic __instance)
        {
            if (SelectedIcon == null) return true; // Skip our implementation

            string fileName = EditorUtility.SaveFilePanel("Export package ...", "", "", "unitypackage");
            if (fileName != "")
            {
                // build guid list
                List<string> guids = new List<string>();

                foreach (object ai in m_ExportPackageItemsField.GetValue(__instance)) // foreach (ExportPackageItem ai in m_ExportPackageItems)
                {
                    if ((int)enabledStatusField.GetValue(ai) > 0)
                    {
                        guids.Add((string)guidField.GetValue(ai)); // if (ai.enabledStatus > 0) guids.Add(ai.guid);
                    }
                }

                string iconPath = AssetDatabase.GetAssetPath(SelectedIcon);
                if (ImageDetection.GetImageFormat(iconPath) != ImageDetection.ImageFormat.PNG)
                {
                    Debug.LogError("[UnityPackage Icon] The selected icon was replaced with a non-PNG file!");

                    return false;
                }

                //ExportPackageMethod.Invoke(null, new object[] { guids.ToArray(), gzipTempPath }); // PackageUtility.ExportPackage(guids.ToArray(), fileName);

                Stream unityPackage;
                try
                {
                    unityPackage = new GZipOutputStream(File.Open(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                    {
                        FileName = "archtemp.tar" // Unity default
                    };
                }
                catch
                {
                    Debug.LogError($"[UnityPackage Icon] Failed to create or overwrite destination file at \"{fileName}\"!");

                    return false;
                }

                // Write the package ourselves
                using (TarOutputStream tarOutput = new TarOutputStream(unityPackage, Encoding.ASCII))
                {
                    TarEntry iconEntry = TarEntry.CreateEntryFromFile(iconPath);
                    iconEntry.TarHeader.Name = ".icon.png";

                    tarOutput.WriteEntry(iconEntry);

                    foreach (string guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(path)) continue;

                        bool isFile = File.Exists(path);
                        if (!isFile && !Directory.Exists(path)) continue;

                        string metaPath = $"{path}.meta";
                        if (!File.Exists(metaPath)) continue;

                        if (isFile) // Write asset data
                        {
                            TarEntry entry = TarEntry.CreateEntryFromFile(path);
                            entry.TarHeader.Name = $"{guid}/asset";
                            tarOutput.WriteEntry(entry);
                        }

                        TarEntry metaEntry = TarEntry.CreateEntryFromFile(metaPath); // Write metadata
                        metaEntry.TarHeader.Name = $"{guid}/asset.meta";
                        tarOutput.WriteEntry(metaEntry);

                        tarOutput.WriteEntry(TarEntry.CreateTarEntry($"{guid}/pathname"), Encoding.Default.GetBytes(path)); // Write path
                    }

                    tarOutput.Flush();
                }

                try
                {
                    ShowSelectedInExplorer.FileOrFolder(Path.GetFullPath(fileName));
                }
                catch
                {
                    Debug.LogError("[UnityPackage Icon] Failed to display exported package in explorer!");
                }

                __instance.Close();
                GUIUtility.ExitGUI();
            }

            return false;
        }

        // https://github.com/Unity-Technologies/UnityCsReference/blob/2d4714b26573c9f220da0e266d62f42830c14ad6/Editor/Mono/GUI/PackageExport.cs#L139
        private static bool TopArea(EditorWindow __instance)
        {
            if (!PatchGUI) return true;

            float totalTopHeight = 84f;//53f;
            Rect r = GUILayoutUtility.GetRect(__instance.position.width, totalTopHeight);

            // Background
            GUI.Label(r, GUIContent.none, Styles.PackageExportTopBarBg);

            // Package icon
            Texture2D icon = (Texture2D)EditorGUI.ObjectField(new Rect(r.x + 10f, r.yMin + 10, 64, 64), GUIContent.none, SelectedIcon, typeof(Texture2D), false);
            if (SelectedIcon != icon && icon != null && ImageDetection.GetImageFormat(AssetDatabase.GetAssetPath(icon)) == ImageDetection.ImageFormat.PNG)
            {
                SelectedIcon = icon;
            }

            // Header
            Rect titleRect = new Rect(r.x + 64 + 20f, r.yMin, r.width, r.height);
            GUI.Label(titleRect, Styles.PackageExportHeader, Styles.PackageExportTitle);

            return false;
        }

        private static class Styles
        {
            public static GUIStyle PackageExportTitle = "LargeBoldLabel";
            public static GUIStyle PackageExportTopBarBg = "OT TopBar";
            public static GUIContent PackageExportHeader = EditorGUIUtility.TrTextContent("Items to Export");

            static Styles()
            {
                Type stylesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.PackageExport+Styles");
                if (stylesType == null)
                {
                    Debug.LogError("[UnityPackage Icon] Failed to find the type UnityEditor.PackageExport.Styles!");

                    return;
                }

                /*FieldInfo titleField = stylesType.GetField("title", BindingFlags.Public | BindingFlags.Static);
                if (titleField != null)
                {
                    PackageExportTitle = (GUIStyle)titleField.GetValue(null);
                }
                else
                {
                    Debug.LogWarning("[UnityPackage Icon] Failed to find the field UnityEditor.PackageExport.Styles.title!");
                }

                FieldInfo topBarBgField = stylesType.GetField("topBarBg", BindingFlags.Public | BindingFlags.Static);
                if (topBarBgField != null)
                {
                    PackageExportTopBarBg = (GUIStyle)topBarBgField.GetValue(null);
                }
                else
                {
                    Debug.LogWarning("[UnityPackage Icon] Failed to find the field UnityEditor.PackageExport.Styles.topBarBgField!");
                }*/

                FieldInfo headerField = stylesType.GetField("header", BindingFlags.Public | BindingFlags.Static);
                if (headerField != null)
                {
                    PackageExportHeader = (GUIContent)headerField.GetValue(null);
                }
                else
                {
                    Debug.LogWarning("[UnityPackage Icon] Failed to find the field UnityEditor.PackageExport.Styles.header!");
                }
            }
        }
    }
}