using UnityEngine;
using UnityEngine.UI;

namespace Marioalexsan.PerformanceMetrics;

public class PieChart : MaskableGraphic, ILayoutElement
{
    private const int TotalColors = 12;
    
    public Vector2 Size
    {
        get => _size;
        set
        {
            _size = value;
            MarkDirty();
        }
    }
    private Vector2 _size = new Vector2(200, 200);
    
    public int ItemCount => _weights.Count;

    public void RemoveAt(int index)
    {
        _weights.RemoveAt(index);
        MarkDirty();
    }

    public void InsertAt(int index, float value)
    {
        _weights.Insert(index, value);
        MarkDirty();
    }

    public void Clear()
    {
        _weights.Clear();
        MarkDirty();
    }

    private readonly List<float> _weights = [];
    
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (_weights.Count == 0)
            return;

        float totalWeight = 0;

        for (int i = 0; i < _weights.Count; i++)
            totalWeight += _weights[i];

        const float detail = 128;

        float weightPerStep = totalWeight / detail;

        float accumulatedWeight = 0f;
        int currentItem = 0;
        
        var lastVertex = Vector2.up * _size / 2;

        for (int i = 1; i <= detail; i++)
        {
            // I want to render this clockwise from Vector2.up, so this means adding +pi/2 to the argument
            // and inverting the X coordinate. Also, lastVertex needs to account for this
            
            var angle = 2 * Math.PI * i / detail + Math.PI / 2;
            var pos = new Vector3(-(float)Math.Cos(angle), (float)Math.Sin(angle), 0f) * _size / 2;
            var sectionColor = Color.HSVToRGB(currentItem / (float)_weights.Count, 1, 1);

            vh.AddVert(Vector2.zero, sectionColor, Vector4.zero);
            vh.AddVert(lastVertex, sectionColor, Vector4.zero);
            vh.AddVert(pos, sectionColor, Vector4.zero);

            var startIndex = (i - 1) * 3;
            
            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);

            lastVertex = pos;

            accumulatedWeight += weightPerStep;

            if (accumulatedWeight >= _weights[currentItem])
            {
                accumulatedWeight -= _weights[currentItem];
                currentItem = (currentItem + 1) % _weights.Count;
            }
        }
    }

    public void CalculateLayoutInputHorizontal()
    {
        minWidth = Size.x;
        preferredWidth = Size.x;
        flexibleWidth = 0;
    }

    public void CalculateLayoutInputVertical()
    {
        minHeight = Size.y;
        preferredHeight = Size.y;
        flexibleHeight = 0;
    }

    public float minWidth { get; private set; }
    public float preferredWidth { get; private set; }
    public float flexibleWidth { get; private set; }
    public float minHeight { get; private set; }
    public float preferredHeight { get; private set; }
    public float flexibleHeight { get; private set; }
    public int layoutPriority { get; private set; } = 0;
}