using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Marioalexsan.PerformanceMetrics;

public class PieChartWithLegend : MonoBehaviour
{
    private const int TotalColors = 12;
    
    public Vector2 Size
    {
        get => _chart.Size;
        set => _chart.Size = value;
    }
    
    public int ItemCount => _items.Count;

    public void RemoveAt(int index)
    {
        _descriptionDirty = true;
        _items.RemoveAt(index);
        _chart.RemoveAt(index);
    }

    public void InsertAt(int index, string label, float value)
    {
        _descriptionDirty = true;
        _items.Insert(index, (label, value));
        _chart.InsertAt(index, value);
    }

    public void Clear()
    {
        _descriptionDirty = true;
        _items.Clear();
        _chart.Clear();
    }

    public string Title
    {
        get => _titleString;
        set
        {
            _titleString = value;
            if (_title)
                _title.text = value;
        }
    }
    private string _titleString = "Chart";

    private PieChart _chart = null!;
    private Text _title = null!;
    private Text _detailsLabels = null!;
    private Text _detailsValues = null!;
    private bool _descriptionDirty;
    
    private readonly List<(string Label, float Value)> _items = [];

    private void Update()
    {
        if (!_descriptionDirty)
            return;

        _descriptionDirty = false;
        
        if (_items.Count == 0)
        {
            _detailsLabels.text = "";
            return;
        }

        var builder = new StringBuilder(256);
        
        for (int i = 0; i < _items.Count; i++)
            builder.AppendLine(_items[i].Label);

        builder.Length -= Environment.NewLine.Length;
        _detailsLabels.text = builder.ToString();

        builder.Clear();

        for (int i = 0; i < _items.Count; i++)
            builder.AppendLine($"{_items[i].Value:F2}");

        builder.Length -= Environment.NewLine.Length;
        _detailsValues.text = builder.ToString();
    }

    private void Awake()
    {
        var font = Resources.Load<Font>("_graphic/_font/terminal-grotesque");
        
        var objGroup = gameObject.AddComponent<VerticalLayoutGroup>();
        objGroup.childForceExpandHeight = false;
        
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(gameObject.transform);
        _title = titleObj.AddComponent<Text>();
        _title.text = _titleString;
        _title.font = font;
        _title.fontSize = 22;
        
        var chartObj = new GameObject("PieChart");
        chartObj.transform.SetParent(gameObject.transform);

        _chart = chartObj.AddComponent<PieChart>();
        
        for (int i = 0; i < _items.Count; i++)
            _chart.InsertAt(i, _items[i].Value);
        
        var detailsObj = new GameObject("Details");
        detailsObj.transform.SetParent(gameObject.transform);
        var detailsLayout = detailsObj.AddComponent<HorizontalLayoutGroup>();
        detailsLayout.childAlignment = TextAnchor.UpperLeft;
        detailsLayout.childForceExpandWidth = false; // Prevent jitter
        detailsLayout.spacing = 10;
        
        var detailsLabelsObj = new GameObject("DetailsLabels");
        detailsLabelsObj.transform.SetParent(detailsObj.transform);
        _detailsLabels = detailsLabelsObj.AddComponent<Text>();
        _detailsLabels.font = font;
        _detailsLabels.fontSize = 20;
        _detailsLabels.text = "";
        
        var detailsValuesObj = new GameObject("DetailsValues");
        detailsValuesObj.transform.SetParent(detailsObj.transform);
        _detailsValues = detailsValuesObj.AddComponent<Text>();
        _detailsValues.font = font;
        _detailsValues.fontSize = 20;
        _detailsValues.text = "";
    }
}