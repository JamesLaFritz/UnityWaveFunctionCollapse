using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityWaveFunctionCollapse.Editor
{
    public class Editor : EditorWindow
    {
        [SerializeField] private TextAsset _samplesXML;
        
        [MenuItem("Wave Function Collapse/Generate Data From Samples")]
        private static void ShowWindow()
        {
            Editor window = GetWindow<Editor>();
            window.titleContent = new GUIContent("Wave Function Collapse");
            window.Show();
        }

        private void OnGUI()
        {
            _samplesXML  = EditorGUILayout.ObjectField(_samplesXML, typeof(TextAsset), true) as TextAsset;
            
            var e = GUI.enabled;
            // ReSharper disable once Unity.PerformanceCriticalCodeNullComparison
            GUI.enabled = _samplesXML != null && !string.IsNullOrWhiteSpace(_samplesXML.text);
            if (GUILayout.Button("Generate"))
            {
                Generate();
            }

            GUI.enabled = e;
        }

        private void Generate()
        {
            XDocument xDoc = XDocument.Parse(_samplesXML.text);

            foreach (XElement xElem in xDoc.Root.Elements("overlapping", "simpletiled"))
            {
                Model model;
                var xName = xElem.Get<string>("name");

                var isOverlapping = xElem.Name == "overlapping";
                var size = xElem.Get("size", isOverlapping ? 48 : 24);
                var width = xElem.Get("width", size);
                var height = xElem.Get("height", size);
                var periodic = xElem.Get("periodic", false);
                var heuristicString = xElem.Get<string>("heuristic");
                Model.Heuristic heuristic = heuristicString == "Scanline"
                    ? Model.Heuristic.Scanline
                    : (heuristicString == "MRV" ? Model.Heuristic.MRV : Model.Heuristic.Entropy);

                if (isOverlapping)
                {
                    var patternSize = xElem.Get("N", 3);
                    var periodicInput = xElem.Get("periodicInput", true);
                    var symmetry = xElem.Get("symmetry", 8);
                    var ground = xElem.Get("ground", false);

                    Debug.Log(
                        $"=== OVERLAPPING: Name: {xName}, Width {width}, Height {height}," +
                        $" Periodic: {periodic}, Heuristic: {heuristic} Pattern Size: {patternSize}," +
                        $" Periodic Input: {periodicInput}, Ground: {ground}, ===");

                    model = new OverlappingModel(xName, width, height, periodic, heuristic, patternSize, periodicInput, symmetry, ground);
                }
                else
                {
                    var subset = xElem.Get<string>("subset");
                    var blackBackground = xElem.Get("blackBackground", false);
                    
                    Debug.Log(
                        $"=== OVERLAPPING: Name: {xName}, Width {width}, Height {height}, Periodic: {periodic}," +
                        $" Heuristic: {heuristic} Subset: {subset}, Black Background: {blackBackground}, ===");

                    model = new SimpleTiledModel(xName, width, height, periodic, heuristic, subset, blackBackground);
                }

                for (var i = 0; i < xElem.Get("screenshots", 2); i++)
                {
                    for (var k = 0; k < 10; k++)
                    {
                        var seed = Random.state.GetHashCode();
                        var success = model.Run(seed, xElem.Get("limit", -1));
                        var fileName = $"output/{xName} {seed}";
                        if (success)
                        {
                            model.Save($"{fileName}.png");
                            if (model is SimpleTiledModel stModel && xElem.Get("textOutput", false))
                                System.IO.File.WriteAllText($"Assets/UnityWaveFunctionCollapse/{fileName}.txt",
                                    stModel.TextOutput());
                            Debug.Log($"Completed: {xName}");
                            break;
                        }

                        Debug.LogWarning($"CONTRADICTION: {fileName}");
                    }
                }
            }
            
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log("Totally Complete");
        }
    }
}