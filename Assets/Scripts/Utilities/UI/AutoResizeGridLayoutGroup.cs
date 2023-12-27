using UnityEngine;
using UnityEngine.UI;

public class AutoResizeGridLayoutGroup : GridLayoutGroup
{
    private RectTransform _RectTransform;

    protected override void Awake()
    {
        base.Awake();

        _RectTransform = GetComponent<RectTransform>();
    }

    public override void SetLayoutVertical()
    {
        if (_RectTransform && constraint != Constraint.Flexible)
        {
            float cellWidth = cellSize.x;
            float cellHeight = cellSize.y;

            switch (constraint)
            {
                case Constraint.FixedRowCount:
                    cellHeight = (_RectTransform.rect.height - padding.vertical - (constraintCount - 1) * spacing.y) / constraintCount;
                    break;
                case Constraint.FixedColumnCount:
                    cellWidth = (_RectTransform.rect.width - padding.horizontal - (constraintCount - 1) * spacing.x) / constraintCount;
                    break;
            }

            cellSize = new Vector2(cellWidth, cellHeight);
        }

        base.SetLayoutVertical();
    }
}