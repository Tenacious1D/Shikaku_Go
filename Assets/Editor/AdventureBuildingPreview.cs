using System.Collections.Generic;
using Shikaku.Logic;
using Shikaku.Menu;
using Shikaku.UI.Buildings;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// A non-persistent sandbox: never reads or changes player completion.
public sealed class AdventureBuildingPreview : EditorWindow
{
    private BuildingDefinition _previewDefinition;
    [MenuItem("Shikaku/Adventure/Building Preview")]
    public static void Open() => GetWindow<AdventureBuildingPreview>("Building Preview");

    private void OnDisable()
    {
        if (_previewDefinition != null) DestroyImmediate(_previewDefinition);
    }

    public void CreateGUI()
    {
        rootVisualElement.Clear();
        rootVisualElement.style.paddingLeft = 16;
        rootVisualElement.style.paddingRight = 16;
        rootVisualElement.style.backgroundColor = new Color(0.94f, 0.91f, 0.83f);
        _previewDefinition = CreateInstance<BuildingDefinition>();
        _previewDefinition.hideFlags = HideFlags.HideAndDontSave;
        var help = new Label("Preview actual chapter masks and automatic styles at two sizes. This does not change player saves.");
        help.style.whiteSpace = WhiteSpace.Normal;
        rootVisualElement.Add(help);
        var choices = new List<string> { "Mixed masks / setbacks / overhang" };
        foreach (var chapter in PuzzleCatalog.StoryPacks) choices.Add(chapter.PackPath);
        var source = new DropdownField("Chapter", choices, 0);
        var styleField = new ObjectField("Definition override") { objectType = typeof(BuildingDefinition) };
        var seedField = new IntegerField("Automatic appearance seed");
        var floorSlider = new SliderInt("Completed floors", 0, 6);
        rootVisualElement.Add(source); rootVisualElement.Add(styleField);
        rootVisualElement.Add(seedField); rootVisualElement.Add(floorSlider);
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        var map = new BuildingView();
        map.style.width = 280; map.style.height = 330;
        var solved = new BuildingView();
        solved.style.width = 420; solved.style.height = 450;
        row.Add(map); row.Add(solved);
        rootVisualElement.Add(row);
        var sizes = new[] { new Vector2Int(7,7), new Vector2Int(7,6), new Vector2Int(6,6),
            new Vector2Int(6,5), new Vector2Int(5,5), new Vector2Int(8,4) };
        var sample = new PuzzlePackData { packId = "sample", puzzles = new PuzzleEntry[sizes.Length] };
        for (int i = 0; i < sizes.Length; i++)
            sample.puzzles[i] = new PuzzleEntry { id = $"preview_{i}", width = sizes[i].x, height = sizes[i].y };
        sample.puzzles[0].mask = "1111111/1111111/0111110/0111110/0111110/1111111/1111111";
        sample.puzzles[4].mask = "11111/11011/10001/11011/11111";
        sample.puzzles[5].mask = "11111111/00111100/00111100/11111111";
        void Bind()
        {
            string path = source.index == 0 ? "preview" : source.value;
            var pack = source.index == 0 ? sample : PuzzleLoader.LoadPackCached(path);
            var assigned = styleField.value as BuildingDefinition;
            if (assigned == null && source.index > 0) assigned = AdventureBuildingData.Load(path)?.Definition;
            _previewDefinition.buildingId = assigned != null ? assigned.buildingId : pack.packId;
            _previewDefinition.style = assigned != null ? assigned.style : null;
            _previewDefinition.appearanceSeed = (assigned != null ? assigned.appearanceSeed : 0) + seedField.value;
            _previewDefinition.overrideArchitecture = assigned != null && assigned.overrideArchitecture;
            _previewDefinition.architecture = assigned != null ? assigned.architecture : BuildingArchitecture.Classic;
            _previewDefinition.overrideColors = assigned != null && assigned.overrideColors;
            if (assigned != null)
            {
                _previewDefinition.wallColor = assigned.wallColor;
                _previewDefinition.roofColor = assigned.roofColor;
            }
            var data = AdventureBuildingData.FromPack(path, pack, _previewDefinition);
            floorSlider.highValue = data.Floors.Count;
            floorSlider.SetValueWithoutNotify(Mathf.Min(floorSlider.value, data.Floors.Count));
            map.SetBuilding(data); solved.SetBuilding(data);
            map.SetProgress(floorSlider.value); solved.SetProgress(floorSlider.value);
        }
        source.RegisterValueChangedCallback(_ => Bind());
        seedField.RegisterValueChangedCallback(_ => Bind());
        styleField.RegisterValueChangedCallback(_ => Bind());
        floorSlider.RegisterValueChangedCallback(_ =>
        { map.SetProgress(floorSlider.value); solved.SetProgress(floorSlider.value); });
        rootVisualElement.Add(new Button(() =>
        {
            int next = floorSlider.value;
            if (next >= floorSlider.highValue) return;
            map.SetProgress(next); solved.SetProgress(next);
            map.AnimateFloor(next); solved.AnimateFloor(next);
            floorSlider.SetValueWithoutNotify(next + 1);
        }) { text = "Construct next floor in both views" });
        Bind();
    }
}
