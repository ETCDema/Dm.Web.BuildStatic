using System.Text;

using Dm.Web.BuildStatic.Models;

namespace Dm.Web.BuildStatic.Services;

internal class HTMLSource: IDisposable
{
	private TextReader _reader;

	private Func<bool>? _readNext;

	private readonly StringBuilder _buf			= new();
	private readonly Dictionary<string, string?> _attrs = [];

	private int _line, _linePos;
	private string? _tagStart;

	public HTMLSource(TextReader reader)
	{
		_reader					= reader;
		_readNext				= _readText;
		_line					= 0;
		_linePos				= 0;
	}

	/// <inheritdoc/>
	public HTMLToken Token		{ get; private set; }	= HTMLToken.Text;

	/// <inheritdoc/>
	public string? TagName		{ get; private set; }

	public IReadOnlyDictionary<string, string?>? Attributes { get; private set; }

	public string? TryGetAttribyte(string name)
	{
		if (Token!=HTMLToken.Tag || name==null || _attrs.Count==0) return null;
		return _attrs.TryGetValue(name, out var v) ? v : null;
	}

	/// <inheritdoc/>
	public bool GetNext()
	{
		_buf.Clear();
		_attrs.Clear();
		Attributes				= null;

		return _readNext!=null && _readNext();
	}

	public string GetContent()
	{
		return Token==HTMLToken.Text || Token==HTMLToken.Comment
			 ? _buf.ToString()
			 : Token==HTMLToken.Tag
			 ? _readTagContent(TagName)
			 : string.Empty;
	}

	/// <inheritdoc/>
	public string GetInfo()
	{
		if (_reader==null) return "<out of source>";

		return string.Concat('[', _line, ':', _linePos, ']');
	}

	public override string ToString()
	{
		return Token==HTMLToken.Text
			 ? $"[{_buf.ToString()}]"
			 : Token==HTMLToken.Comment
			 ? $"<!--{_buf.ToString()}-->"
			 : Token==HTMLToken.Tag
			 ? $"<{TagName} {string.Join(" ", _attrs)} >"
			 : Token==HTMLToken.TagEnd
			 ? $"</{TagName}>"
			 : "???";
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		End();

		_reader?.Dispose();
		_reader					= null!;

		GC.SuppressFinalize(this);
	}

	/// <inheritdoc/>
	protected bool End()
	{
		_buf.Clear();
		_attrs.Clear();
		_line                   = 0;
		_linePos				= 0;
		_readNext				= null;
		TagName					= null;
		Token					= HTMLToken.Text;
		Attributes              = null;

		return false;
	}

	#region Чтение разных типов

	private bool _readText()
	{
		// Читаем поток (надеюсь он буферизированный 🤔)...
		var c					= _reader.Read();
		if (c==-1) return End();                        // Поток кончился - завершаем

		while (c>-1)
		{
			var ch				= (char)c;
			if (ch!='<')
				_buf.Append(ch);						// Какой-то символ - добавим к строке
			else if (_isTextEnded())					// Начало тэга или комментария или DOCTYPE? - закончим с текстом, предполагается что _isTextEnded дополнит _buf прочитанными символами, включая '<'
				break;

			// Продолжим читать текст...
			c					= _reader.Read();
		}

		return _contentOrNext(HTMLToken.Text);
	}

	private bool _isTextEnded()
	{
		// У нас уже прочитали '<', далее варианты: TAG, /TAG, !--, !DOCTYPE
		var c					= _reader.Read();
		if (c==-1)
		{
			// Конец потока - конец текста
			_buf.Append('<');
			return true;
		}

		var ch					= (char)c;
		if (char.IsLetter(ch))
		{
			// Далее читаем имя тега, но сначала закончим с текстом
			_tagStart			= ch.ToString();
			_readNext			= _readTag;
			return true;
		}

		if (ch!='!' && ch!='/')
		{
			// У нас не комментарий или DOCTYPE - продолжим текст
			_buf.Append('<')
				.Append(ch);
			return false;
		}

		// Читаем что там после '<!' или '</'
		c						= _reader.Read();
		if (c==-1)
		{
			// Конец потока - конец текста
			_buf.Append('<')
				.Append(ch);
			return true;
		}

		var isTagEnd			= ch=='/';
		ch						= (char)c;
		if (char.IsLetter(ch))
		{
			// Далее читаем имя тега, скорее всего это /TAG или !DOCTYPE, но сначала закончим с текстом
			if (isTagEnd)
			{
				_tagStart		= ch.ToString();
				_readNext		= _readTagEnd;
			} else
			{
				_tagStart		= string.Concat('!', ch);
				_readNext		= _readTag;
			}
			return true;
		}

		if (ch!='-')
		{
			// У нас не комментарий - продолжим текст
			_buf.Append("<!")
				.Append(ch);
			return false;
		}
		
		// Читаем что там после '<!-'
		c						= _reader.Read();
		if (c==-1)
		{
			// Конец потока - конец текста
			_buf.Append("<!-");
			return true;
		}

		ch						= (char)c;
		if (ch=='-')
		{
			// У нас комментарий
			_readNext			= _readComment;
			return true;
		}

		// Продолжим текст
		_buf.Append("<!-").Append(ch);
		return false;
	}

	private bool _readTag()
	{
		_readNext				= _readText;

		var lastChar			= _tryReadName((ch) => ch=='>' || ch=='/' || ch=='"' || ch=='<', "Tag not completed");

		Token					= HTMLToken.Tag;
		TagName					= _buf.ToString();

		Attributes              = _attrs;
		_tryReadAttributes(lastChar);

		return true;
	}

	private void _tryReadAttributes(char lastChar)
	{
		do
		{
			if (lastChar=='/')                                      // Должен быть конец тега
			{
				var c           = _reader.Read();
				if (c==-1) throw new FormatException($"Tag not completed at {GetInfo()}"); // Поток ВНЕЗАПНО кончился

				lastChar        = (char)c;
				if (lastChar!='>') throw new FormatException($"Invalid self-closed tag - expected '>', readed '{lastChar}' at {GetInfo()}"); // Поток ВНЕЗАПНО кончился

				_readNext       = _readText;
				return;
			} else if (lastChar=='>')                               // Атрибутов больше не ждем
			{
				_readNext       = Token==HTMLToken.Tag && TagName=="script" ? _readScript : _readText;
				return;
			} else if (lastChar=='<')
			{
				throw new FormatException($"Invalid start tag - readed '{lastChar}' at {GetInfo()}");
			} else if (lastChar=='"' || lastChar=='\'')             // Атрибут без имени в двойных или одиночных кавычках
			{
				_buf.Clear();
				lastChar		= _readString(lastChar);
				_attrs[""]      = _buf.ToString();
				continue;
			} else                                                  // Обычный атрибут
			{
				_buf.Clear();
				_buf.Append(lastChar);
				lastChar		= _readAttribute();
				continue;
			}

			lastChar			= (char)_reader.Read();
		} while (-1<lastChar);

		throw new FormatException($"Tag not completed at {GetInfo()}"); // Поток ВНЕЗАПНО кончился
	}

	private static readonly HashSet<string> _SINGLE_TAGS	= [
		"IMG", "BR", "HR", "META", "LINK", "COL", "BASE", "AREA", "INPUT", "SOURCE", "TRACK", "PARAM", "KEYGEN", "WBR", "EMBED"
	];

	private bool _isSingle(string tag)
	{
		return !string.IsNullOrEmpty(tag)
			&& (tag[0]=='!' || _SINGLE_TAGS.Contains(tag.ToUpper()));
	}

	private char _readAttribute()
	{
		var lastChar			= _tryReadName((ch) => ch=='>' || ch=='/' || ch=='"' || ch=='\'' || ch=='<' || ch=='=', "Tag attribute not completed");
		var name				= _buf.ToString();

		if (lastChar=='=')
		{
			lastChar			= _skipWhitespace("Tag attribute value not completed");
			if (lastChar=='"' || lastChar=='\'')
			{
				_buf.Clear();
				lastChar		= _readString(lastChar);
				_attrs[name]    = _buf.ToString();
			} else
			{
				// TODO
			}
		} else
		{
			_attrs[name]        = null;
		}

		return lastChar;
	}

	private char _readString(char stopChar)
	{
		var c					= _reader.Read();
		if (c==-1) throw new FormatException($"String not completed at {GetInfo()}"); // Поток ВНЕЗАПНО кончился

		_buf.Clear();
		while (c>-1)
		{
			var ch				= (char)c;
			//if (ch=='<')
			//{
			//	throw new FormatException($"String not completed, but found '<' at {GetInfo()}");
			//}

			if (ch==stopChar)
			{
				//if (_buf.Length>0)
				//	_data		= WebUtility.HtmlDecode(_buf.ToString());

				return _skipWhitespace("Tag not completed");
			}

			_buf.Append(ch);									// Какой-то символ - добавим к строке				
			c					= _reader.Read();               // Продолжим читать текст...
		}

		throw new FormatException($"String not completed at {GetInfo()}"); // Поток ВНЕЗАПНО кончился
	}

	private bool _readTagEnd()
	{
		var lastChar			= _tryReadName((ch) => ch=='>', "Tag end not completed");
		if (lastChar!='>') throw new FormatException($"Invalid tag closing - expected '>', readed '{lastChar}' at {GetInfo()}"); // Поток ВНЕЗАПНО кончился

		var nodeName			= _buf.ToString();
		if (_isSingle(nodeName))
		{
			// Когда у нас закрывают тег, который этого не предусматривает
			// просто пропускаем непотребство и читаем дальше
			return _readText();
		}

		Token					= HTMLToken.TagEnd;
		TagName					= nodeName;
		_readNext				= _readText;
		return true;
	}

	private static readonly string _endComment  = "-->";

	private bool _readComment()
	{
		var c					= _reader.Read();
		if (c==-1) throw new FormatException($"Comment not completed at {GetInfo()}"); // Поток ВНЕЗАПНО кончился

		var end					= 0;
		while (c>-1)
		{
			var ch				= (char)c;
			if (ch==_endComment[end])
			{
				end++;
				if (_endComment.Length<=end) break;             // Комментарий кончился
			} else
			{
				if (0<end)
				{
					_buf.Append(_endComment.AsSpan(0, end));
					end         = 0;                            // Вернемся к началу поиска окончания комментария
				}
				_buf.Append(ch);                                // Какой-то символ - добавим к строке				
			}

			c					= _reader.Read();               // Продолжим читать текст...
		}

		_readNext				= _readText;
		return _contentOrNext(HTMLToken.Comment);
	}

	private static readonly string _endScript  = "</script>";
	private bool _readScript()
	{
		var c                   = _reader.Read();
		if (c==-1) throw new FormatException($"Script not completed at {GetInfo()}"); // Поток ВНЕЗАПНО кончился

		var end                 = 0;
		while (c>-1)
		{
			var ch              = (char)c;
			if (ch==_endScript[end])
			{
				end++;
				if (_endScript.Length<=end) break;             // Комментарий кончился
			} else
			{
				if (0<end)
				{
					_buf.Append(_endScript.AsSpan(0, end));
					end         = 0;                            // Вернемся к началу поиска окончания комментария
				}
				_buf.Append(ch);                                // Какой-то символ - добавим к строке				
			}

			c                   = _reader.Read();               // Продолжим читать текст...
		}

		Token                   = HTMLToken.Text;
		TagName                 = null;
		_readNext               = _readEndScript;
		return _contentOrNext(HTMLToken.Text);
	}

	private bool _readEndScript()
	{
		Token                   = HTMLToken.TagEnd;
		TagName                 = "script";
		_readNext               = _readText;
		return true;
	}

	private char _tryReadName(Func<char, bool> stopFx, string invalidFormatMsg)
	{
		var c					= _reader.Read();
		if (c==-1) throw new FormatException($"{invalidFormatMsg} at {GetInfo()}"); // Поток ВНЕЗАПНО кончился

		if (_tagStart!=null) _buf.Append(_tagStart);			// Добавим начало тега
		_tagStart				= null;

		var buildName			= true;

		while (c>-1)
		{
			var ch				= (char)c;
			if (stopFx(ch)) return ch;							// Фсе, готово

			if (buildName)
			{
				if (!_isNameChar(ch)) buildName = false;		// Все еще читаем имя?
			} else if (_isNameChar(ch))
			{
				return ch;										// Начало следующего имени
			}

			if (buildName) _buf.Append(ch);						// Какой-то символ - добавим к строке				
			c					= _reader.Read();               // Продолжим читать текст...
		}

		throw new FormatException($"{invalidFormatMsg} at {GetInfo()}"); // Поток ВНЕЗАПНО кончился
	}

	private static bool _isNameChar(char ch)
	{
		return ch=='-' || ch==':' || ch=='_' || ch=='@' || ch=='.' || char.IsLetterOrDigit(ch);
	}

	private char _skipWhitespace(string invalidFormatMsg)
	{
		var c					= _reader.Read();
		while (c>-1)
		{
			var ch				= (char)c;
			if (!char.IsWhiteSpace(ch)) return ch;
			c					= _reader.Read();
		}
		throw new FormatException($"{invalidFormatMsg} at {GetInfo()}"); // Поток ВНЕЗАПНО кончился
	}

	private bool _contentOrNext(HTMLToken token)
	{
		// Если что-то в буфере есть - вернем содержимое как содержимое, иначе читаем дальше
		for (int i = 0; i < _buf.Length; i++)
		{
			if (!char.IsWhiteSpace(_buf[i]))
			{
				// У нас в буфере есть не пробельный символ - говорим что нашли текст
				//if (_buf.Length>0)
				//{
				//	_data		= WebUtility.HtmlDecode(_buf.ToString());
				//	_buf.Clear();
				//}

				Token			= token;
				TagName			= null;
				return true;
			}
		}

		// Уберем все пробельные символы
		_buf.Clear();

		// Как бы пустой буфер - текста не будет, читаем дальше или заканчиваем
		return _readNext!=null ? _readNext() : End();
	}

	private string _readTagContent(string tagName)
	{
		if (_isSingle(tagName)) return string.Empty;

		_readNext               = _readText;

		_buf.Clear();
		_readRecursive($"<{tagName}", $"</{tagName}>");
		return _buf.ToString();
	}

	private void _readRecursive(string start, string end)
	{
		var c                   = _reader.Read();
		if (c==-1) throw new FormatException($"Content not completed at {GetInfo()}"); // Поток ВНЕЗАПНО кончился

		var startLen            = start.Length;
		var endLen              = end.Length;
		var startPos            = 0;
		var endPos              = 0;
		while (c>-1)
		{
			var ch              = (char)c;

			if (ch==start[startPos])
			{
				startPos++;
				if (startLen<=startPos)
				{
					startPos    = 0;
					_buf.Append(ch);
					c           = _reader.Read();
					ch          = (char)c;
					if (char.IsWhiteSpace(ch) || ch=='>')
					{
						_buf.Append(ch);
						_readRecursive(start, end);
						_buf.Append(end);
						c       = _reader.Read();
					}
					continue;
				}
			} else if (0<startPos)
			{
				startPos        = 0;
			}

			if (ch==end[endPos])
			{
				endPos++;
				if (endLen<=endPos) break;
			} else
			{
				if (0<endPos)
				{
					_buf.Append(end.AsSpan(0, endPos));
					endPos      = 0;
				}
				_buf.Append(ch);
			}

			c                   = _reader.Read();
		}
	}

	#endregion
}