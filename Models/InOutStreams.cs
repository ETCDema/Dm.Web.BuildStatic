namespace Dm.Web.BuildStatic.Models
{
	/// <summary>
	/// Потоки для чтения и записи
	/// </summary>
	internal class InOutStreams
	{
		/// <summary>Поток для чтения</summary>
		public Stream In		{ get; init; } = default!;

		/// <summary>Отображаемое имя потока</summary>
		public string InName	{ get; init; } = default!;

		/// <summary>Поток для записи</summary>
		public Stream Out		{ get; init; } = default!;

		/// <summary>Отображаемое имя потока</summary>
		public string OutName	{ get; init; } = default!;
	}
}
