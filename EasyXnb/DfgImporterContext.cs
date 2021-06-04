using System;
using Microsoft.Xna.Framework.Content.Pipeline;

namespace DynamicFontGenerator
{
	public sealed class DfgImporterContext : ContentImporterContext
	{
		public override ContentBuildLogger Logger
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public override string OutputDirectory
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public override string IntermediateDirectory
		{
			get
			{
				return GeneratorGame.intermedDirectorySetting;
			}
		}

		public override void AddDependency(string filename)
		{
			throw new NotImplementedException();
		}
	}
}
