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
    public class UnityPackageIcon : EditorWindow
    {
        private const string PackageName = "wtf.buddyworks.uit";
        private static string IconSavePath = $"Packages/{PackageName}/selectedIcon.txt";

        private static Texture2D _selectedIcon = null;
        private static Texture2D SelectedIcon { get { return _selectedIcon; } set { UpdateIcon(value); } }
        private static Harmony Harmony;
        private static MethodInfo TopAreaMethod;
        private static FieldInfo m_ExportPackageItemsField, enabledStatusField, guidField;
        private static bool PatchGUI = false;

        static UnityPackageIcon()
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

            Harmony.Patch(exportMethod, new HarmonyMethod(typeof(UnityPackageIcon).GetMethod(nameof(Export), BindingFlags.NonPublic | BindingFlags.Static)));
            Harmony.Patch(showExportPackageMethod, null, new HarmonyMethod(typeof(UnityPackageIcon).GetMethod(nameof(ShowExportPackage), BindingFlags.NonPublic | BindingFlags.Static)));
        }

        [MenuItem("BUDDYWORKS/Set UnityPackage export icon")]
        private static void Init()
        {
            UnityPackageIcon window = (UnityPackageIcon)GetWindow(typeof(UnityPackageIcon));

            window.titleContent = new GUIContent("UnityPackage Icon");

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

            string path = null;
            if (icon != null)
            {
                path = AssetDatabase.GetAssetPath(icon);

                if (string.IsNullOrEmpty(path))
                {
                    _selectedIcon = null;

                    Debug.LogError("[UnityPackage Icon] The path to the icon could not be found!");

                    return;
                }
                else if (ImageDetection.GetImageFormat(path) != ImageDetection.ImageFormat.PNG)
                {
                    _selectedIcon = null;

                    Debug.LogError("[UnityPackage Icon] The icon must be PNG formatted!");

                    return;
                }
            }

            _selectedIcon = icon;

            string guid = SelectedIcon == null ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (Directory.Exists(Path.GetDirectoryName(IconSavePath))) File.WriteAllText(IconSavePath, guid);
        }

        // Prevents printing 4 GUIStyle errors on script domain reload.
        // https://github.com/Unity-Technologies/UnityCsReference/blob/2d4714b26573c9f220da0e266d62f42830c14ad6/Editor/Mono/GUI/PackageExport.cs#L46
        private static void ShowExportPackage()
        {
            if (PatchGUI) return;

            PatchGUI = true;

            Harmony.Patch(TopAreaMethod, new HarmonyMethod(typeof(UnityPackageIcon).GetMethod(nameof(TopArea), BindingFlags.NonPublic | BindingFlags.Static)));
        }

        // https://github.com/Unity-Technologies/UnityCsReference/blob/2d4714b26573c9f220da0e266d62f42830c14ad6/Editor/Mono/GUI/PackageExport.cs#L235
        private static bool Export(EditorWindow __instance)
        {
            if (SelectedIcon == null) return true; // No icon, skip our implementation

            string iconPath = AssetDatabase.GetAssetPath(SelectedIcon);
            if (string.IsNullOrEmpty(iconPath))
            {
                Debug.LogWarning("[UnityPackage Icon] The selected icon's path cannot be found, exporting without icon!");

                SelectedIcon = null;

                return true;
            }
            else if (ImageDetection.GetImageFormat(iconPath) != ImageDetection.ImageFormat.PNG)
            {
                Debug.LogWarning("[UnityPackage Icon] The selected icon was replaced with a non-PNG file, exporting without icon!");

                SelectedIcon = null;

                return true;
            }

            string fileName = EditorUtility.SaveFilePanel("Export package ...", "", "", "unitypackage");
            if (fileName != "")
            {
                // build guid list
                List<string> guids = new List<string>();

                foreach (object ai in (Array)m_ExportPackageItemsField.GetValue(__instance)) // foreach (ExportPackageItem ai in m_ExportPackageItems)
                {
                    if ((int)enabledStatusField.GetValue(ai) > 0)
                    {
                        guids.Add((string)guidField.GetValue(ai)); // if (ai.enabledStatus > 0) guids.Add(ai.guid);
                    }
                }

                try
                {
                    ExportPackage(fileName, guids, iconPath);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UnityPackage Icon] {e.Message}");
                }

                __instance.Close();
                GUIUtility.ExitGUI();
            }

            return false;
        }

        /// <summary>
        /// Exports a UnityPackage, optionally with an icon.
        /// </summary>
        /// <param name="path">UnityPackage destination path</param>
        /// <param name="guids">A collection of asset GUID strings</param>
        /// <param name="iconPath">Path to the icon file</param>
        /// <param name="showFile">Display the exported package in explorer upon completion</param>
        /// <exception cref="IOException">Thrown when the destination path cannot be accessed or written to.</exception>
        /// <exception cref="Exception">Thrown when <paramref name="showFile"/> is true and the exported package failed to display in explorer.</exception>
        public static void ExportPackage(string path, IEnumerable<string> guids, string iconPath = null, bool showFile = true)
        {
            Stream unityPackage;
            try
            {
                unityPackage = new GZipOutputStream(File.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                {
                    FileName = "archtemp.tar" // Unity default
                };
            }
            catch
            {
                throw new IOException($"Failed to create or overwrite destination file \"{path}\"!");
            }

            // Write the package ourselves
            using (TarOutputStream tarOutput = new TarOutputStream(unityPackage, Encoding.ASCII))
            {
                if (!string.IsNullOrEmpty(iconPath))
                {
                    TarEntry iconEntry = TarEntry.CreateEntryFromFile(iconPath); // Write icon
                    iconEntry.TarHeader.Name = ".icon.png";
                    tarOutput.WriteEntry(iconEntry);
                }

                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath)) continue;

                    bool isFile = File.Exists(assetPath);
                    if (!isFile && !Directory.Exists(assetPath)) continue;

                    string metaPath = $"{assetPath}.meta";
                    if (!File.Exists(metaPath)) continue;

                    if (isFile) // Write asset data
                    {
                        TarEntry entry = TarEntry.CreateEntryFromFile(assetPath);
                        entry.TarHeader.Name = $"{guid}/asset";
                        tarOutput.WriteEntry(entry);
                    }

                    TarEntry metaEntry = TarEntry.CreateEntryFromFile(metaPath); // Write metadata
                    metaEntry.TarHeader.Name = $"{guid}/asset.meta";
                    tarOutput.WriteEntry(metaEntry);

                    tarOutput.WriteEntry(TarEntry.CreateTarEntry($"{guid}/pathname"), Encoding.Default.GetBytes(assetPath)); // Write path
                }

                tarOutput.Flush();
            }

            try
            {
                ShowSelectedInExplorer.FileOrFolder(Path.GetFullPath(path));
            }
            catch
            {
                throw new Exception("Failed to display exported package in explorer!");
            }
        }

        // https://github.com/Unity-Technologies/UnityCsReference/blob/2d4714b26573c9f220da0e266d62f42830c14ad6/Editor/Mono/GUI/PackageExport.cs#L139
        private static bool TopArea(EditorWindow __instance)
        {
            float totalTopHeight = 84f;//53f;
            Rect r = GUILayoutUtility.GetRect(__instance.position.width, totalTopHeight);

            // Background
            GUI.Label(r, GUIContent.none, PackageExportStyles.topBarBg);

            // Package icon
            SelectedIcon = (Texture2D)EditorGUI.ObjectField(new Rect(r.x + 10f, r.yMin + 10, 64, 64), GUIContent.none, SelectedIcon, typeof(Texture2D), false);

            // Header
            Rect titleRect = new Rect(r.x + 64 + 20f, r.yMin, r.width, r.height);
            GUI.Label(titleRect, PackageExportStyles.header, PackageExportStyles.title);

            return false;
        }

        private static class PackageExportStyles
        {
            public static GUIStyle title = "LargeBoldLabel";
            public static GUIStyle topBarBg = "OT TopBar";
            public static GUIContent header = EditorGUIUtility.TrTextContent("Items to Export");

            static PackageExportStyles()
            {
                Type stylesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.PackageExport+Styles");
                if (stylesType == null)
                {
                    Debug.LogError("[UnityPackage Icon] Failed to find the type UnityEditor.PackageExport.Styles!");

                    return;
                }

                FieldInfo titleField = stylesType.GetField("title", BindingFlags.Public | BindingFlags.Static);
                if (titleField != null)
                {
                    title = (GUIStyle)titleField.GetValue(null);
                }
                else
                {
                    Debug.LogWarning("[UnityPackage Icon] Failed to find the field UnityEditor.PackageExport.Styles.title!");
                }

                FieldInfo topBarBgField = stylesType.GetField("topBarBg", BindingFlags.Public | BindingFlags.Static);
                if (topBarBgField != null)
                {
                    topBarBg = (GUIStyle)topBarBgField.GetValue(null);
                }
                else
                {
                    Debug.LogWarning("[UnityPackage Icon] Failed to find the field UnityEditor.PackageExport.Styles.topBarBgField!");
                }

                FieldInfo headerField = stylesType.GetField("header", BindingFlags.Public | BindingFlags.Static);
                if (headerField != null)
                {
                    header = (GUIContent)headerField.GetValue(null);
                }
                else
                {
                    Debug.LogWarning("[UnityPackage Icon] Failed to find the field UnityEditor.PackageExport.Styles.header!");
                }
            }
        }
    }
}