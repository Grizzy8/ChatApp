namespace ChatClient;

/// <summary>
/// Satırları kod ile çizilen, titremesiz ListBox: fare üstündeki satır izlenir ve her satırı
/// <see cref="Painter"/> temsilcisi çizer. Sabit satır yüksekliği kullanılır.
/// </summary>
public sealed class ThemedListBox : ListBox
{
    private int _hoverIndex = -1;
    private int _rowHeight = 40;

    /// <summary>Satır yüksekliği, 96 DPI'a göre mantıksal piksel olarak; ekran DPI'ına göre ölçeklenir.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int RowHeight
    {
        get => _rowHeight;
        set
        {
            _rowHeight = value;
            ApplyRowHeight();
        }
    }

    public ThemedListBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        BorderStyle = BorderStyle.None;
        IntegralHeight = false;
        BackColor = Theme.Sidebar;
        ForeColor = Theme.Text;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    /// <summary>Satırı çizen temsilci: (olay, öğe, fare üstünde mi).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action<DrawItemEventArgs, object, bool>? Painter { get; set; }

    /// <summary>Verilen öğeyi tıklanamaz (başlık/yer tutucu) saymak için kullanılır.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<object, bool>? IsSelectable { get; set; }

    /// <summary>Verilen noktadaki öğe; satır yoksa null.</summary>
    public object? ItemAt(Point point)
    {
        int index = IndexFromPoint(point);
        return index >= 0 && index < Items.Count ? Items[index] : null;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyRowHeight();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyRowHeight();
    }

    private void ApplyRowHeight() => ItemHeight = (int)Math.Round(_rowHeight * DeviceDpi / 96.0);

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count)
        {
            return;
        }

        using var background = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        Painter?.Invoke(e, Items[e.Index], e.Index == _hoverIndex);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index = IndexFromPoint(e.Location);
        if (index != _hoverIndex)
        {
            _hoverIndex = index;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex != -1)
        {
            _hoverIndex = -1;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        // Sağ tık da satırı seçsin (bağlam menüsü doğru öğeye uygulansın); başlıklar seçilemez.
        int index = IndexFromPoint(e.Location);
        if (index >= 0 && Items[index] is { } item && IsSelectable?.Invoke(item) == false)
        {
            return;
        }

        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right && index >= 0)
        {
            SelectedIndex = index;
        }
    }
}
