using System.Text.Json;

using Dm.Web.BuildStatic.Stages.Core;

namespace Dm.Web.BuildStatic.Services
{
	internal class Pipeline
	{
		private IStartStage? _start;
		private readonly List<IStage> _stages   = [];
		private readonly Type? _paramType;
		private IFinalStage? _final;
		private Type? _resultType;

		private Action<object>? _run;

		public Pipeline(string id, Type? paramType, IEnumerable<JsonProperty> cfg, StageProvider builders)
		{
			ID					= id;
			_paramType			= paramType;
			_resultType			= paramType;

			foreach (var prop in cfg)
			{
				Add(builders.BuildStage(prop.Name, prop.Value));
			}

			Build();
		}

		public string ID		{ get; }

		public Type? ResultType { get { if (_run==null) throw new Exception("Configure pipeline before use."); return _resultType; } }

		public void Run(object p)
		{
			if (_run==null) throw new Exception("Configure pipeline before run.");
			_run(p);
		}

		/// <summary>
		/// Добавить этап, в конвейере может быть один начальный и один конечный этап а так же несколько промежуточных этапов.
		/// </summary>
		/// <param name="stage"></param>
		/// <exception cref="Exception"></exception>
		protected void Add(IPipelineStage stage)
		{
			var needStartStage  = _paramType==null;

			if (needStartStage && _start==null)
			{
				if (stage is not IStartStage start) throw new Exception("Start stage is empty - add start pipeline stage first");

				_start          = start;
				_resultType     = start.ResultType;
			} else if (stage is IFinalStage final)
			{
				_tryFinalAsStage();
				if (_resultType==null) throw new Exception("Pipeline finalized");
				if (!final.ParamType.IsAssignableFrom(_resultType)) throw new Exception($"Prev result type {_resultType.FullName} can't be assign to parameter type {final.ParamType.FullName}");

				_final          = final;
				_resultType     = null;
			} else if (stage is IStage middle)
			{
				_tryFinalAsStage();
				if (_resultType==null) throw new Exception("Pipeline finalized");
				if (!middle.ParamType.IsAssignableFrom(_resultType)) throw new Exception($"Prev result type {_resultType.FullName} can't be assign to parameter type {middle.ParamType.FullName}");

				_stages.Add(middle);
				_resultType     = middle.ResultType;
			} else if (stage is IStartStage)
			{
				throw new Exception(needStartStage ? "Pipeline has start stage" : "Pipeline not supported start stage");
			} else
			{
				throw new Exception(needStartStage ? "Stage mast be IStartStage/IStage/IFinalStage" : "Stage mast be IStage/IFinalStage");
			}
		}

		/// <summary>
		/// Выполнить этапы конвейера
		/// </summary>
		/// <exception cref="Exception"></exception>
		protected void Build()
		{
			var needStartStage  = _paramType==null;
			var start           = _start
								?? (needStartStage ? throw new Exception("No start stage at pipeline") : null);

			// Строим методы вызовов следующих этапов с конца
			var last            = _stages.Count-1;
			Action<object> next = _final!=null                       ? _final.Finalize
								: last<0 && start is IFinalStage fin ? fin.Finalize
								: throw new Exception("No final stage at pipeline");

			// Промежуточные этапы
			for (int i = last; 0<=i; i--)
			{
				next            = _buildNextFx(_stages[i], next);
			}

			// Метод выполнения
			_run				= start==null
								? next
								: (p) => start.Run(next);
		}

		private void _tryFinalAsStage()
		{
			if (_final==null) return;
			if (_final is not IStage stage) return;

			_stages.Add(stage);
			_resultType         = stage.ResultType;
			_final              = null!;
		}

		private static Action<object> _buildNextFx(IStage stage, Action<object> next)
		{
			return (p) => stage.Run(p, next);
		}

	}
}
