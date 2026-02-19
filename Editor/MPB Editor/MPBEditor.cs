using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UdonSharpEditor;

namespace valenvrc.Tools.MPB
{
    [InitializeOnLoad]
    public class MPBEditor : EditorWindow
    {
        [System.Serializable]
        public class ShaderProperty
        {
            public string name;
            public ShaderUtil.ShaderPropertyType type;
            public float floatValue;
            public Color colorValue;
            public Vector4 vectorValue;
            public Texture textureValue;
            
            public ShaderProperty(string name, ShaderUtil.ShaderPropertyType type)
            {
                this.name = name;
                this.type = type;
                this.floatValue = 0f;
                this.colorValue = Color.white;
                this.vectorValue = Vector4.zero;
                this.textureValue = null;
            }
        }
        
        [System.Serializable]
        public class MaterialConfig
        {
            public Material material;
            public List<ShaderProperty> properties = new List<ShaderProperty>();
            public bool foldout = true;
        }
        
        [System.Serializable]
        public class RendererConfig
        {
            public Renderer renderer;
            public List<MaterialConfig> materials = new List<MaterialConfig>();
            public bool foldout = true;
            public bool hasPendingChanges = false;
        }
        
        [System.Serializable]
        public class MPBConfig
        {
            public List<RendererConfig> renderers = new List<RendererConfig>();
        }
        
        private const string CONFIG_PATH = "Packages/com.valenvrc.VMPBUE/Editor/MPBConfig.json";
        private const string APPLIER_PREF_KEY = "MPBEditor_ApplierInstanceID";
        
        private MPBConfig config = new MPBConfig();
        private Vector2 leftScrollPosition;
        private Vector2 rightScrollPosition;
        private int selectedRendererIndex = -1;
        private MPBApplierTool applierToImport;
        
        // Static constructor for InitializeOnLoad
        static MPBEditor()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChangedStatic;
            EditorSceneManager.sceneOpened += OnSceneOpenedStatic;
        }
        
        private static void OnPlayModeStateChangedStatic(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                // Reapply all property blocks after exiting play mode
                ApplyToAllStatic();
                ApplyFromPersistedApplier();
            }
        }
        
        private static void OnSceneOpenedStatic(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += ApplyFromPersistedApplier;
        }
        
        private static void ApplyFromPersistedApplier()
        {
            if (!EditorPrefs.HasKey(APPLIER_PREF_KEY))
                return;
            
            int instanceID = EditorPrefs.GetInt(APPLIER_PREF_KEY, 0);
            if (instanceID == 0)
                return;
            
            MPBApplierTool applier = EditorUtility.InstanceIDToObject(instanceID) as MPBApplierTool;
            if (applier != null && applier.meshes != null && applier.meshes.Length > 0)
            {
                int appliedMeshes = 0;
                foreach (MPBMesh mesh in applier.meshes)
                {
                    if (mesh != null)
                    {
                        mesh.ApplyAllMaterials();
                        appliedMeshes++;
                    }
                }
                
                if (appliedMeshes > 0)
                {
                    Debug.Log($"[MPBEditor] Auto-applied {appliedMeshes} mesh(es) from persisted applier: {applier.name}");
                }
            }
        }
        
        private static void ApplyToAllStatic()
        {
            if (!File.Exists(CONFIG_PATH))
                return;
            
            string json = File.ReadAllText(CONFIG_PATH);
            MPBConfig config = JsonUtility.FromJson<MPBConfig>(json);
            
            if (config == null || config.renderers == null)
                return;
            
            int appliedCount = 0;
            
            foreach (RendererConfig rendererConfig in config.renderers)
            {
                if (rendererConfig.renderer == null) continue;
                
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                Material[] materials = rendererConfig.renderer.sharedMaterials;
                
                for (int matIndex = 0; matIndex < materials.Length; matIndex++)
                {
                    Material mat = materials[matIndex];
                    if (mat == null) continue;
                    
                    MaterialConfig matConfig = rendererConfig.materials.FirstOrDefault(m => m.material == mat);
                    if (matConfig == null || matConfig.properties.Count == 0) continue;
                    
                    rendererConfig.renderer.GetPropertyBlock(mpb, matIndex);
                    
                    foreach (ShaderProperty prop in matConfig.properties)
                    {
                        switch (prop.type)
                        {
                            case ShaderUtil.ShaderPropertyType.Float:
                            case ShaderUtil.ShaderPropertyType.Range:
                                mpb.SetFloat(prop.name, prop.floatValue);
                                break;
                            case ShaderUtil.ShaderPropertyType.Color:
                                mpb.SetColor(prop.name, prop.colorValue);
                                break;
                            case ShaderUtil.ShaderPropertyType.Vector:
                                mpb.SetVector(prop.name, prop.vectorValue);
                                break;
                            case ShaderUtil.ShaderPropertyType.TexEnv:
                                mpb.SetTexture(prop.name, prop.textureValue);
                                break;
                        }
                    }
                    
                    rendererConfig.renderer.SetPropertyBlock(mpb, matIndex);
                }
                
                appliedCount++;
            }
            
            if (appliedCount > 0)
            {
                Debug.Log($"[MPB Editor] Reapplied properties to {appliedCount} renderers after exiting play mode");
            }
        }
        
        [MenuItem("ValenVRC/Tools/MPB Editor")]
        public static void ShowWindow()
        {
            GetWindow<MPBEditor>("MPB Editor");
        }
        
        private void OnEnable()
        {
            LoadConfig();
            LoadPersistedApplier();
        }
        
        private void OnDisable()
        {
            SaveConfig();
            SavePersistedApplier();
        }
        
        private void LoadPersistedApplier()
        {
            if (EditorPrefs.HasKey(APPLIER_PREF_KEY))
            {
                int instanceID = EditorPrefs.GetInt(APPLIER_PREF_KEY, 0);
                if (instanceID != 0)
                {
                    applierToImport = EditorUtility.InstanceIDToObject(instanceID) as MPBApplierTool;
                }
            }
        }
        
        private void SavePersistedApplier()
        {
            if (applierToImport != null)
            {
                EditorPrefs.SetInt(APPLIER_PREF_KEY, applierToImport.GetInstanceID());
            }
            else
            {
                EditorPrefs.DeleteKey(APPLIER_PREF_KEY);
            }
        }
        
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                // Reapply all property blocks after exiting play mode
                ApplyToAll();
            }
        }
        
        private void OnGUI()
        {
            HandleDragAndDrop();
            
            EditorGUILayout.BeginHorizontal();
            
            DrawLeftColumn();
            
            DrawRightColumn();
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // Import from Udon section
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Import from:", GUILayout.Width(80));
            applierToImport = (MPBApplierTool)EditorGUILayout.ObjectField(applierToImport, typeof(MPBApplierTool), true);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Apply to All", GUILayout.Height(30)))
            {
                ApplyToAll();
            }
            
            if (GUILayout.Button("Export to Udon", GUILayout.Height(30)))
            {
                ExportToUdon();
            }
            
            EditorGUI.BeginDisabledGroup(applierToImport == null);
            if (GUILayout.Button("Import from Udon", GUILayout.Height(30)))
            {
                ImportFromUdon();
            }
            EditorGUI.EndDisabledGroup();
            
            if (GUILayout.Button("Clear All", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clear All", "Remove all renderers and their properties?", "Yes", "No"))
                {
                    config.renderers.Clear();
                    selectedRendererIndex = -1;
                    SaveConfig();
                }
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawLeftColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            
            EditorGUILayout.LabelField("Renderers", EditorStyles.boldLabel);
            
            EditorGUILayout.Space(5);
            
            leftScrollPosition = EditorGUILayout.BeginScrollView(leftScrollPosition);
            
            for (int i = 0; i < config.renderers.Count; i++)
            {
                DrawRendererListItem(config.renderers[i], i);
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawRendererListItem(RendererConfig rendererConfig, int index)
        {
            if (rendererConfig.renderer == null)
            {
                config.renderers.RemoveAt(index);
                if (selectedRendererIndex >= config.renderers.Count)
                {
                    selectedRendererIndex = -1;
                }
                SaveConfig();
                return;
            }
            
            EditorGUILayout.BeginHorizontal();
            
            bool isSelected = selectedRendererIndex == index;
            
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            if (isSelected)
            {
                buttonStyle.normal.background = buttonStyle.active.background;
            }
            
            Color originalColor = GUI.backgroundColor;
            if (rendererConfig.hasPendingChanges)
            {
                GUI.backgroundColor = Color.yellow;
            }
            
            if (GUILayout.Button(rendererConfig.renderer.name, buttonStyle, GUILayout.Height(25)))
            {
                selectedRendererIndex = index;
            }
            
            GUI.backgroundColor = originalColor;
            
            if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(25)))
            {
                config.renderers.RemoveAt(index);
                if (selectedRendererIndex >= config.renderers.Count)
                {
                    selectedRendererIndex = -1;
                }
                SaveConfig();
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }
        
        private void DrawRightColumn()
        {
            EditorGUILayout.BeginVertical();
            
            if (selectedRendererIndex >= 0 && selectedRendererIndex < config.renderers.Count)
            {
                RendererConfig selectedRenderer = config.renderers[selectedRendererIndex];
                
                if (selectedRenderer.renderer == null)
                {
                    config.renderers.RemoveAt(selectedRendererIndex);
                    selectedRendererIndex = -1;
                    SaveConfig();
                    EditorGUILayout.EndVertical();
                    return;
                }
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Renderer: {selectedRenderer.renderer.name}", EditorStyles.boldLabel);
                
                if (GUILayout.Button("Apply", GUILayout.Width(60)))
                {
                    ApplyProperties(selectedRenderer);
                }
                
                if (GUILayout.Button("Revert", GUILayout.Width(60)))
                {
                    RevertProperties(selectedRenderer);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("GameObject", GUILayout.Width(80));
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(selectedRenderer.renderer.gameObject, typeof(GameObject), true);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(10);
                
                rightScrollPosition = EditorGUILayout.BeginScrollView(rightScrollPosition);
                
                for (int i = selectedRenderer.materials.Count - 1; i >= 0; i--)
                {
                    DrawMaterialConfig(selectedRenderer.materials[i], i, selectedRenderer);
                }
                
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.LabelField("Select a renderer from the left panel", EditorStyles.centeredGreyMiniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void HandleDragAndDrop()
        {
            Event evt = Event.current;
            Rect dropArea = GUILayoutUtility.GetRect(0.0f, 100.0f, GUILayout.ExpandWidth(true));
            
            GUIStyle centeredStyle = new GUIStyle(GUI.skin.box);
            centeredStyle.alignment = TextAnchor.MiddleCenter;
            centeredStyle.fontSize = 16;
            centeredStyle.fontStyle = FontStyle.Bold;
            
            GUI.Box(dropArea, "Drag Renderers Here", centeredStyle);
            
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        return;
                    
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        
                        foreach (Object draggedObject in DragAndDrop.objectReferences)
                        {
                            GameObject go = draggedObject as GameObject;
                            if (go != null)
                            {
                                Renderer renderer = go.GetComponent<Renderer>();
                                if (renderer != null)
                                {
                                    AddRenderer(renderer);
                                }
                            }
                        }
                    }
                    break;
            }
        }
        
        private void DrawMaterialConfig(MaterialConfig materialConfig, int index, RendererConfig rendererConfig)
        {
            if (materialConfig.material == null)
            {
                rendererConfig.materials.RemoveAt(index);
                SaveConfig();
                return;
            }
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            
            materialConfig.foldout = EditorGUILayout.Foldout(materialConfig.foldout, $"Material: {materialConfig.material.name}", true);
            
            EditorGUILayout.EndHorizontal();
            
            if (materialConfig.foldout)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.LabelField("Shader", materialConfig.material.shader.name);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);
                
                for (int i = materialConfig.properties.Count - 1; i >= 0; i--)
                {
                    DrawProperty(materialConfig.properties[i], i, materialConfig);
                }
                
                EditorGUILayout.Space(5);
                
                if (GUILayout.Button("Add Property"))
                {
                    ShowPropertyMenu(materialConfig);
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }
        
        private void DrawProperty(ShaderProperty prop, int index, MaterialConfig materialConfig)
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField(prop.name, GUILayout.Width(150));
            
            EditorGUI.BeginChangeCheck();
            
            switch (prop.type)
            {
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                    prop.floatValue = EditorGUILayout.FloatField(prop.floatValue);
                    break;
                case ShaderUtil.ShaderPropertyType.Color:
                    prop.colorValue = EditorGUILayout.ColorField(prop.colorValue);
                    break;
                case ShaderUtil.ShaderPropertyType.Vector:
                    prop.vectorValue = EditorGUILayout.Vector4Field("", prop.vectorValue);
                    break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    prop.textureValue = (Texture)EditorGUILayout.ObjectField(prop.textureValue, typeof(Texture), false);
                    break;
            }
            
            if (EditorGUI.EndChangeCheck() && selectedRendererIndex >= 0 && selectedRendererIndex < config.renderers.Count)
            {
                config.renderers[selectedRendererIndex].hasPendingChanges = true;
            }
            
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                materialConfig.properties.RemoveAt(index);
                SaveConfig();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void ShowPropertyMenu(MaterialConfig materialConfig)
        {
            GenericMenu menu = new GenericMenu();
            HashSet<string> existingProps = new HashSet<string>(materialConfig.properties.Select(p => p.name));
            
            Material mat = materialConfig.material;
            
            if (mat == null || mat.shader == null)
            {
                menu.AddDisabledItem(new GUIContent("No shader available"));
                menu.ShowAsContext();
                return;
            }
            
            Shader shader = mat.shader;
            int propertyCount = ShaderUtil.GetPropertyCount(shader);
            
            Dictionary<ShaderUtil.ShaderPropertyType, List<(string name, string desc)>> propertiesByType = 
                new Dictionary<ShaderUtil.ShaderPropertyType, List<(string, string)>>();
            
            for (int i = 0; i < propertyCount; i++)
            {
                string propName = ShaderUtil.GetPropertyName(shader, i);
                
                if (existingProps.Contains(propName))
                    continue;
                
                ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(shader, i);
                string propDescription = ShaderUtil.GetPropertyDescription(shader, i);
                
                if (!propertiesByType.ContainsKey(propType))
                {
                    propertiesByType[propType] = new List<(string, string)>();
                }
                
                propertiesByType[propType].Add((propName, propDescription));
            }
            
            if (propertiesByType.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No properties available"));
            }
            else
            {
                foreach (var kvp in propertiesByType.OrderBy(x => x.Key.ToString()))
                {
                    ShaderUtil.ShaderPropertyType propType = kvp.Key;
                    List<(string name, string desc)> props = kvp.Value;
                    
                    foreach (var prop in props.OrderBy(p => p.name))
                    {
                        string displayName = prop.name;
                        if (displayName.Length > 50)
                        {
                            displayName = displayName.Substring(0, 47) + "...";
                        }
                        
                        string menuPath = $"{propType}/{displayName}";
                        
                        string propName = prop.name;
                        menu.AddItem(new GUIContent(menuPath), false, () =>
                        {
                            materialConfig.properties.Add(new ShaderProperty(propName, propType));
                            SaveConfig();
                        });
                    }
                }
            }
            
            menu.ShowAsContext();
        }
        
        private void AddRenderer(Renderer renderer)
        {
            if (config.renderers.Any(r => r.renderer == renderer))
                return;
            
            RendererConfig rendererConfig = new RendererConfig { renderer = renderer };
            
            Material[] materials = renderer.sharedMaterials;
            foreach (Material mat in materials)
            {
                if (mat != null)
                {
                    rendererConfig.materials.Add(new MaterialConfig { material = mat });
                }
            }
            
            config.renderers.Add(rendererConfig);
            SaveConfig();
        }
        
        private void AddSelectedRenderers()
        {
            GameObject[] selected = Selection.gameObjects;
            
            foreach (GameObject obj in selected)
            {
                Renderer renderer = obj.GetComponent<Renderer>();
                
                if (renderer != null)
                {
                    AddRenderer(renderer);
                }
            }
        }
        
        private void ApplyProperties(RendererConfig rendererConfig)
        {
            if (rendererConfig.renderer == null) return;
            
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            
            Material[] materials = rendererConfig.renderer.sharedMaterials;
            
            for (int matIndex = 0; matIndex < materials.Length; matIndex++)
            {
                Material mat = materials[matIndex];
                if (mat == null) continue;
                
                MaterialConfig matConfig = rendererConfig.materials.FirstOrDefault(m => m.material == mat);
                if (matConfig == null || matConfig.properties.Count == 0) continue;
                
                rendererConfig.renderer.GetPropertyBlock(mpb, matIndex);
                
                foreach (ShaderProperty prop in matConfig.properties)
                {
                    switch (prop.type)
                    {
                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            mpb.SetFloat(prop.name, prop.floatValue);
                            break;
                        case ShaderUtil.ShaderPropertyType.Color:
                            mpb.SetColor(prop.name, prop.colorValue);
                            break;
                        case ShaderUtil.ShaderPropertyType.Vector:
                            mpb.SetVector(prop.name, prop.vectorValue);
                            break;
                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            mpb.SetTexture(prop.name, prop.textureValue);
                            break;
                    }
                }
                
                rendererConfig.renderer.SetPropertyBlock(mpb, matIndex);
            }
            
            rendererConfig.hasPendingChanges = false;
            
            Debug.Log($"Applied properties to {rendererConfig.renderer.name}");
        }
        
        private void RevertProperties(RendererConfig rendererConfig)
        {
            if (File.Exists(CONFIG_PATH))
            {
                string json = File.ReadAllText(CONFIG_PATH);
                MPBConfig savedConfig = JsonUtility.FromJson<MPBConfig>(json);
                
                if (savedConfig != null)
                {
                    RendererConfig savedRenderer = savedConfig.renderers.FirstOrDefault(r => r.renderer == rendererConfig.renderer);
                    
                    if (savedRenderer != null)
                    {
                        rendererConfig.materials = savedRenderer.materials;
                        rendererConfig.hasPendingChanges = false;
                        Repaint();
                        Debug.Log($"Reverted properties for {rendererConfig.renderer.name}");
                    }
                }
            }
        }
        
        private void ApplyToAll()
        {
            foreach (RendererConfig rendererConfig in config.renderers)
            {
                ApplyProperties(rendererConfig);
            }
            
            Debug.Log($"Applied properties to {config.renderers.Count} renderers");
        }
        
        private void ImportFromUdon()
        {
            if (applierToImport == null)
            {
                EditorUtility.DisplayDialog("Import Error", "Please select an MPB Applier to import from.", "OK");
                return;
            }
            
            if (applierToImport.meshes == null || applierToImport.meshes.Length == 0)
            {
                EditorUtility.DisplayDialog("Import Error", "The selected MPB Applier has no meshes to import.", "OK");
                return;
            }
            
            bool clearExisting = EditorUtility.DisplayDialog(
                "Import from Udon",
                "Do you want to clear existing configuration before importing?",
                "Clear and Import",
                "Merge with Existing"
            );
            
            if (clearExisting)
            {
                config.renderers.Clear();
                selectedRendererIndex = -1;
            }
            
            int importedMeshes = 0;
            int importedMaterials = 0;
            
            foreach (MPBMesh mpbMesh in applierToImport.meshes)
            {
                if (mpbMesh == null || mpbMesh.targetRenderer == null)
                    continue;
                
                // Check if renderer already exists in config
                RendererConfig rendererConfig = config.renderers.FirstOrDefault(r => r.renderer == mpbMesh.targetRenderer);
                
                if (rendererConfig == null)
                {
                    rendererConfig = new RendererConfig
                    {
                        renderer = mpbMesh.targetRenderer,
                        foldout = true,
                        hasPendingChanges = false
                    };
                    config.renderers.Add(rendererConfig);
                }
                
                // Process materials
                if (mpbMesh.materials != null)
                {
                    foreach (MPBMaterial mpbMaterial in mpbMesh.materials)
                    {
                        if (mpbMaterial == null || mpbMaterial.material == null)
                            continue;
                        
                        // Check if material already exists in the renderer config
                        MaterialConfig matConfig = rendererConfig.materials.FirstOrDefault(m => m.material == mpbMaterial.material);
                        
                        if (matConfig == null)
                        {
                            matConfig = new MaterialConfig
                            {
                                material = mpbMaterial.material,
                                foldout = true
                            };
                            rendererConfig.materials.Add(matConfig);
                        }
                        else
                        {
                            // Clear existing properties to replace them
                            matConfig.properties.Clear();
                        }
                        
                        // Import properties
                        if (mpbMaterial.propertyNames != null)
                        {
                            for (int i = 0; i < mpbMaterial.propertyNames.Length; i++)
                            {
                                string propName = mpbMaterial.propertyNames[i];
                                int propType = mpbMaterial.propertyTypes[i];
                                
                                ShaderProperty shaderProp = new ShaderProperty(propName, (ShaderUtil.ShaderPropertyType)propType);
                                
                                // Set the appropriate value based on type
                                switch (propType)
                                {
                                    case 0: // Color
                                        if (i < mpbMaterial.colorValues.Length)
                                            shaderProp.colorValue = mpbMaterial.colorValues[i];
                                        break;
                                    case 1: // Vector
                                        if (i < mpbMaterial.vectorValues.Length)
                                            shaderProp.vectorValue = mpbMaterial.vectorValues[i];
                                        break;
                                    case 2: // Float
                                    case 3: // Range
                                        if (i < mpbMaterial.floatValues.Length)
                                            shaderProp.floatValue = mpbMaterial.floatValues[i];
                                        break;
                                    case 4: // Texture
                                        if (i < mpbMaterial.textureValues.Length)
                                            shaderProp.textureValue = mpbMaterial.textureValues[i];
                                        break;
                                }
                                
                                matConfig.properties.Add(shaderProp);
                            }
                        }
                        
                        importedMaterials++;
                    }
                }
                
                importedMeshes++;
            }
            
            SaveConfig();
            Repaint();
            
            EditorUtility.DisplayDialog(
                "Import Complete",
                $"Successfully imported {importedMeshes} mesh(es) with {importedMaterials} material(s).",
                "OK"
            );
            
            Debug.Log($"[MPBEditor] Imported {importedMeshes} meshes with {importedMaterials} materials from {applierToImport.name}");
        }
        
        private void ExportToUdon()
        {
            GameObject applierObj;
            MPBApplierTool applier;
            bool isNewApplier = false;
            
            // Use existing applier if assigned, otherwise create a new one
            if (applierToImport != null)
            {
                applier = applierToImport;
                applierObj = applier.gameObject;
                
                // Clear existing child objects
                for (int i = applierObj.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(applierObj.transform.GetChild(i).gameObject);
                }
                
                Debug.Log($"[MPBEditor] Reusing existing MPB Applier: {applierObj.name}");
            }
            else
            {
                applierObj = new GameObject("MPB Applier");
                applier = applierObj.AddUdonSharpComponent<MPBApplierTool>();
                isNewApplier = true;
                Debug.Log($"[MPBEditor] Created new MPB Applier");
            }
            
            List<MPBMesh> meshList = new List<MPBMesh>();
            
            foreach (RendererConfig rendererConfig in config.renderers)
            {
                if (rendererConfig.renderer == null) continue;
                
                GameObject meshObj = new GameObject($"Mesh_{rendererConfig.renderer.name}");
                meshObj.transform.SetParent(applierObj.transform);
                
                MPBMesh mpbMesh = meshObj.AddUdonSharpComponent<MPBMesh>();
                mpbMesh.targetRenderer = rendererConfig.renderer;
                
                List<MPBMaterial> materialList = new List<MPBMaterial>();
                
                foreach (MaterialConfig matConfig in rendererConfig.materials)
                {
                    if (matConfig.material == null || matConfig.properties.Count == 0) continue;
                    
                    GameObject matObj = new GameObject($"Mat_{matConfig.material.name}");
                    matObj.transform.SetParent(meshObj.transform);
                    
                    MPBMaterial mpbMaterial = matObj.AddUdonSharpComponent<MPBMaterial>();
                    mpbMaterial.material = matConfig.material;
                    
                    List<string> names = new List<string>();
                    List<int> types = new List<int>();
                    List<float> floats = new List<float>();
                    List<Color> colors = new List<Color>();
                    List<Vector4> vectors = new List<Vector4>();
                    List<Texture> textures = new List<Texture>();
                    
                    foreach (ShaderProperty prop in matConfig.properties)
                    {
                        names.Add(prop.name);
                        types.Add((int)prop.type);
                        floats.Add(prop.floatValue);
                        colors.Add(prop.colorValue);
                        vectors.Add(prop.vectorValue);
                        textures.Add(prop.textureValue);
                    }
                    
                    mpbMaterial.propertyNames = names.ToArray();
                    mpbMaterial.propertyTypes = types.ToArray();
                    mpbMaterial.floatValues = floats.ToArray();
                    mpbMaterial.colorValues = colors.ToArray();
                    mpbMaterial.vectorValues = vectors.ToArray();
                    mpbMaterial.textureValues = textures.ToArray();
                    
                    materialList.Add(mpbMaterial);
                }
                
                mpbMesh.materials = materialList.ToArray();
                meshList.Add(mpbMesh);
            }
            
            applier.meshes = meshList.ToArray();
            
            Selection.activeGameObject = applierObj;
            EditorGUIUtility.PingObject(applierObj);
            
            string actionText = isNewApplier ? "Created" : "Updated";
            Debug.Log($"{actionText} MPB Applier GameObject with {meshList.Count} meshes");
        }
        
        private void SaveConfig()
        {
            string directory = Path.GetDirectoryName(CONFIG_PATH);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            string json = JsonUtility.ToJson(config, true);
            File.WriteAllText(CONFIG_PATH, json);
            AssetDatabase.Refresh();
        }
        
        private void LoadConfig()
        {
            if (File.Exists(CONFIG_PATH))
            {
                string json = File.ReadAllText(CONFIG_PATH);
                config = JsonUtility.FromJson<MPBConfig>(json);
                
                if (config == null)
                {
                    config = new MPBConfig();
                }
            }
        }
    }
}