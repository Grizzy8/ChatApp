using System.Security;

namespace ChatClient
{
    // Sohbet geçmişinin kaydedilmesi ve bağlanınca yüklenmesiyle ilgili form kodu.
    public partial class ClientForm
    {
        private ChatHistoryStore? _history;

        /// <summary>
        /// Yeni bir sohbet oturumu başlatır: ekranı temizler ve bugünün geçmişini gri satırlar olarak yükler.
        /// Yükleme bilerek eşzamanlıdır; böylece araya yeni gelen mesajlar geçmişin önüne geçemez.
        /// Geçmiş okunamazsa sohbet yine de başlar, yalnızca uyarı gösterilir.
        /// </summary>
        private void BeginHistorySession(string? username)
        {
            _chatView.Clear();
            _history = null;
            SetHistoryWarning(null);

            if (username is null)
            {
                return;
            }

            try
            {
                var history = new ChatHistoryStore(username);
                IReadOnlyList<string> lines = history.ReadLastLines(DateTime.Today, ClientConstants.MaxHistoryLinesToLoad);
                _history = history;

                if (lines.Count == 0)
                {
                    return;
                }

                foreach (string line in lines)
                {
                    _chatView.AppendHistory(line);
                }

                _chatView.AppendSeparator("Önceki mesajlar");
            }
            catch (Exception ex) when (IsHistoryFileError(ex))
            {
                SetHistoryWarning($"Geçmiş yüklenemedi: {ex.Message}");
            }
        }

        /// <summary>Mesaj satırını geçmiş dosyasına ekler. Hata sohbeti durdurmaz, yalnızca uyarı olarak gösterilir.</summary>
        private async Task SaveToHistoryAsync(DateTime time, string line)
        {
            ChatHistoryStore? history = _history;
            if (history is null)
            {
                return;
            }

            try
            {
                await history.AppendAsync(time, line);
                if (!IsDisposed)
                {
                    SetHistoryWarning(null);
                }
            }
            catch (Exception ex) when (IsHistoryFileError(ex))
            {
                if (!IsDisposed)
                {
                    SetHistoryWarning($"Geçmiş kaydedilemedi: {ex.Message}");
                }
            }
        }

        private void SetHistoryWarning(string? warning)
        {
            historyStatusLabel.Text = warning ?? string.Empty;
            historyStatusLabel.ForeColor = ClientConstants.WarningColor;
        }

        private static bool IsHistoryFileError(Exception ex) =>
            ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or ArgumentException;
    }
}
