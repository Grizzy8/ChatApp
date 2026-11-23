namespace ChatClient;

/// <summary>
/// Seçili öğeye tekrar tıklayınca seçimi kaldıran ListBox. Standart ListBox'ta bu mümkün değildir
/// ve yerel seçim MouseDown olayından önce yapıldığı için olay içinde "önceden seçiliydi mi" anlaşılamaz;
/// bu yüzden karar, yerel işlemden önce pencere mesajı düzeyinde verilir.
/// </summary>
public sealed class DeselectableListBox : ListBox
{
    private const int WmLeftButtonDown = 0x0201;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmLeftButtonDown)
        {
            // LParam: düşük 16 bit x, yüksek 16 bit y (işaretli).
            long lParam = m.LParam.ToInt64();
            var point = new Point((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF));
            int clickedIndex = IndexFromPoint(point);

            if (clickedIndex >= 0 && clickedIndex == SelectedIndex)
            {
                ClearSelected();
                return;
            }
        }

        base.WndProc(ref m);
    }
}
