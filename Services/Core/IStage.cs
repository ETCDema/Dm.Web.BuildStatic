namespace Dm.Web.BuildStatic.Services.Core;

/// <summary>
/// Промежуточный этап конвейера
/// </summary>
internal interface IStage: IPipelineStage
{
	/// <summary>Тип параметра - результата предыдущего этапа</summary>
	Type ParamType				{ get; }

	/// <summary>Тип возвращаемого результата</summary>
	Type ResultType				{ get; }

	/// <summary>
	/// Выполнить действия этапа
	/// </summary>
	/// <param name="param">Результат предыдущего этапа</param>
	/// <param name="next">Метод выполнения следующего этапа конвейера</param>
	void Run(object param, Action<object> next);
}
