using System.Text;

namespace ChatShared;

/// <summary>
/// Bir TextReader'dan '\n' ile biten satırları okur ve tek bir satırın uzunluğunu sınırlar.
/// StreamReader.ReadLineAsync sınırsız okuduğu için, satır sonu göndermeyen bir istemci
/// belleği doldurabilirdi; bu sınıf buna izin vermez.
/// </summary>
public sealed class BoundedLineReader
{
    private const int BufferSize = 1024;

    private readonly TextReader _reader;
    private readonly int _maxLength;
    private readonly char[] _buffer = new char[BufferSize];
    private int _position;
    private int _length;

    public BoundedLineReader(TextReader reader, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        _reader = reader;
        _maxLength = maxLength;
    }

    /// <summary>
    /// Bir satır okur (sondaki '\r' ve '\n' dahil edilmez).
    /// Bağlantı kapandıysa null döner; yarım kalmış satır teslim edilmez.
    /// Satır sınırı aşılırsa InvalidDataException fırlatır; akış bu durumdan sonra kullanılmamalıdır.
    /// </summary>
    public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        var line = new StringBuilder();

        while (true)
        {
            if (_position == _length)
            {
                _length = await _reader.ReadAsync(_buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                _position = 0;

                if (_length == 0)
                {
                    return null;
                }
            }

            char c = _buffer[_position++];

            if (c == '\n')
            {
                if (line.Length > 0 && line[^1] == '\r')
                {
                    line.Length--;
                }

                if (line.Length > _maxLength)
                {
                    throw TooLong();
                }

                return line.ToString();
            }

            // Sondaki '\r' satır sonunun parçası olabilir, bu yüzden bellek koruması 1 karakter paylı yapılır.
            if (line.Length > _maxLength)
            {
                throw TooLong();
            }

            line.Append(c);
        }
    }

    private InvalidDataException TooLong() => new($"Satır {_maxLength} karakter sınırını aşıyor.");
}
