using System.Collections;
using System.Text.Json;

using Dm.Web.BuildStatic.Services.Core;

namespace Dm.Web.BuildStatic.Services.Stages
{
	/// <summary>
	/// Перебрать элементы в коллеции с предыдущего этапа и для каждого вызвать следующий этап
	/// </summary>
	/// <param name="itemType">Тип элемента коллекции</param>
	internal class ForEach(Type itemType): IStage
	{
		/// <summary>
		/// Создать этап. Параметр конфигурации - тип элемента коллекции.
		/// </summary>
		public class Builder : IStageBuilder
		{
			/// <inheritdoc cref="Builder"/>
			/// <param name="cfg">Строка - тип элемента коллекции, можно указывать как простые типы (string, int и т.п.) так и C# типы</param>
			/// <param name="dst">Создаваемый конвейер</param>
			public void Build(JsonElement cfg, Pipeline dst)
			{
				var typeName    = cfg.GetString()!;
				var type        = typeName=="string" ? typeof(string)
								: Type.GetType(typeName, true)!;
				dst.Add(new ForEach(type));
			}
		}

		/// <inheritdoc/>
		public Type ParamType	{ get; } = typeof(IEnumerable<>).MakeGenericType(itemType);

		/// <inheritdoc/>
		public Type ResultType	{ get; } = itemType;

		/// <param name="param">Коллекция элементов с предыдущего этапа</param>
		/// <param name="next">Следующий этап, вызывается для каждого элемента коллекции</param>
		/// <inheritdoc/>
		public void Run(object param, Action<object> next)
		{
			var src             = param as IEnumerable ?? throw new ArgumentNullException(nameof(param));
			foreach (var item in src)
			{
				next(item);
			}
		}
	}
}
